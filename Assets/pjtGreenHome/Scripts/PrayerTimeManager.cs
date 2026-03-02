using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using PrayerSystem.Models;

namespace PrayerSystem
{
    /// <summary>
    /// PrayerTimeManager — Singleton.
    /// GPS sekarang dihandle oleh LocationService.
    /// Manager ini hanya subscribe OnLocationAcquired lalu fetch Aladhan API.
    /// </summary>
    public class PrayerTimeManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        public static PrayerTimeManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────
        public event Action<List<PrayerInfo>> OnPrayerTimesUpdated;
        public event Action<string>           OnErrorOccurred;

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        private const string API_BASE               = "https://api.aladhan.com/v1/timings";
        private const int    CALCULATION_METHOD     = 3;   // Muslim World League
        private const float  LOCATION_THRESHOLD_KM  = 5f;

        // PlayerPrefs keys
        private const string PREFS_KEY = "prayer_cached_data";

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────
        public List<PrayerInfo> PrayerTimes { get; private set; } = new List<PrayerInfo>();

        private float _lastFetchLat;
        private float _lastFetchLon;
        private bool  _isFetching;
        private bool  _hasSuccessfulApiFetch = false;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 1. Load cache agar UI langsung tampil
            if (TryLoadFromCache(out CachedPrayerData cached) && cached.IsValidForToday())
            {
                Debug.Log("[PrayerTimeManager] Cache loaded — menampilkan sementara.");
                BuildPrayerList(cached.fajr, cached.dhuhr, cached.asr, cached.maghrib, cached.isha);
            }

            // 2. Subscribe ke LocationService — akan trigger saat GPS selesai
            if (LocationService.Instance != null)
                LocationService.Instance.OnLocationAcquired += OnLocationAcquired;
            else
                Debug.LogError("[PrayerTimeManager] LocationService.Instance null! Pastikan ada di scene.");
        }

        private void OnDestroy()
        {
            if (LocationService.Instance != null)
                LocationService.Instance.OnLocationAcquired -= OnLocationAcquired;
        }

        // ─────────────────────────────────────────────
        // Dipanggil LocationService saat GPS siap
        // ─────────────────────────────────────────────
        private void OnLocationAcquired(float lat, float lon)
        {
            if (!_isFetching)
                StartCoroutine(FetchPrayerTimesIfNeeded(lat, lon));
        }

        // ─────────────────────────────────────────────
        // Public: Paksa refresh
        // ─────────────────────────────────────────────
        public void RequestRefresh()
        {
            // Re-trigger LocationService → GPS → OnLocationAcquired → FetchAPI
            LocationService.Instance?.RequestRefresh();
        }

        // ─────────────────────────────────────────────
        // Cek & Fetch Prayer API
        // ─────────────────────────────────────────────
        private IEnumerator FetchPrayerTimesIfNeeded(float lat, float lon)
        {
            _isFetching = true;

            bool locationChanged = _hasSuccessfulApiFetch &&
                                   GetDistanceKm(_lastFetchLat, _lastFetchLon, lat, lon) > LOCATION_THRESHOLD_KM;

            bool needsFetch = !_hasSuccessfulApiFetch || locationChanged || !IsCacheValidToday();

            if (needsFetch)
            {
                string reason = !_hasSuccessfulApiFetch ? "cold start"
                              : locationChanged         ? $"lokasi berubah {GetDistanceKm(_lastFetchLat, _lastFetchLon, lat, lon):F1}km"
                              : "cache expired";
                Debug.Log($"[PrayerTimeManager] Fetching API — alasan: {reason}");

                yield return StartCoroutine(FetchFromAPI(lat, lon));
                _lastFetchLat = lat;
                _lastFetchLon = lon;
            }
            else
            {
                Debug.Log("[PrayerTimeManager] Cache valid & lokasi stabil. Skip API call.");
            }

            _isFetching = false;
        }

        private IEnumerator FetchFromAPI(float lat, float lon)
        {
            string date = DateTime.Now.ToString("dd-MM-yyyy");
            string url  = $"{API_BASE}/{date}?latitude={lat}&longitude={lon}&method={CALCULATION_METHOD}";

            Debug.Log($"[PrayerTimeManager] Fetching: {url}");

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[PrayerTimeManager] API error: {req.error}");
                    OnErrorOccurred?.Invoke($"Gagal mengambil data: {req.error}");
                    yield break;
                }

                ParseAndApply(req.downloadHandler.text, lat, lon);
            }
        }

        // ─────────────────────────────────────────────
        // Parsing
        // ─────────────────────────────────────────────
        private void ParseAndApply(string json, float lat, float lon)
        {
            try
            {
                AladhanResponse resp = JsonUtility.FromJson<AladhanResponse>(json);

                if (resp == null || resp.data == null || resp.data.timings == null)
                {
                    OnErrorOccurred?.Invoke("Format data API tidak valid.");
                    return;
                }

                AladhanTimings t = resp.data.timings;

                string fajr    = StripSuffix(t.Fajr);
                string dhuhr   = StripSuffix(t.Dhuhr);
                string asr     = StripSuffix(t.Asr);
                string maghrib = StripSuffix(t.Maghrib);
                string isha    = StripSuffix(t.Isha);

                BuildPrayerList(fajr, dhuhr, asr, maghrib, isha);
                SaveToCache(fajr, dhuhr, asr, maghrib, isha, lat, lon);
                _hasSuccessfulApiFetch = true;

                Debug.Log("[PrayerTimeManager] Prayer times updated from API.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PrayerTimeManager] Parse exception: {ex.Message}");
                OnErrorOccurred?.Invoke("Gagal memproses data jadwal.");
            }
        }

        private void BuildPrayerList(string fajr, string dhuhr, string asr, string maghrib, string isha)
        {
            DateTime today = DateTime.Today;

            PrayerTimes = new List<PrayerInfo>
            {
                new PrayerInfo("Fajr",    "Subuh",   ParseTime(today, fajr)),
                new PrayerInfo("Dhuhr",   "Dzuhur",  ParseTime(today, dhuhr)),
                new PrayerInfo("Asr",     "Ashar",   ParseTime(today, asr)),
                new PrayerInfo("Maghrib", "Maghrib", ParseTime(today, maghrib)),
                new PrayerInfo("Isha",    "Isya",    ParseTime(today, isha)),
            };

            OnPrayerTimesUpdated?.Invoke(PrayerTimes);
        }

        // ─────────────────────────────────────────────
        // Cache (PlayerPrefs)
        // ─────────────────────────────────────────────
        private void SaveToCache(string fajr, string dhuhr, string asr, string maghrib, string isha, float lat, float lon)
        {
            var data = new CachedPrayerData
            {
                fajr       = fajr,
                dhuhr      = dhuhr,
                asr        = asr,
                maghrib    = maghrib,
                isha       = isha,
                cachedDate = DateTime.Now.ToString("yyyy-MM-dd"),
                cachedLat  = lat,
                cachedLon  = lon,
            };
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private bool TryLoadFromCache(out CachedPrayerData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return false;
            try
            {
                data = JsonUtility.FromJson<CachedPrayerData>(PlayerPrefs.GetString(PREFS_KEY));
                return data != null;
            }
            catch { return false; }
        }

        private bool IsCacheValidToday()
        {
            return TryLoadFromCache(out CachedPrayerData d) && d != null && d.IsValidForToday();
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────
        private static DateTime ParseTime(DateTime baseDate, string hhmm)
        {
            if (DateTime.TryParseExact(hhmm.Trim(), "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime result))
            {
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day,
                                    result.Hour, result.Minute, 0);
            }
            Debug.LogWarning($"[PrayerTimeManager] Failed to parse time: '{hhmm}'");
            return baseDate;
        }

        private static string StripSuffix(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "00:00";
            int idx = raw.IndexOf(' ');
            return idx > 0 ? raw.Substring(0, idx) : raw;
        }

        private static float GetDistanceKm(float lat1, float lon1, float lat2, float lon2)
        {
            const float R = 6371f;
            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                      Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                      Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
            return R * 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        }
    }
}