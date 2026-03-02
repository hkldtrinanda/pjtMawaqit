using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using PrayerSystem.Models;

namespace PrayerSystem
{
    /// <summary>
    /// MosqueService — Singleton.
    /// Fetch daftar masjid/mushola terdekat dari Overpass API (OpenStreetMap).
    /// Gratis, no API key. Radius default 3 km, max 30 hasil, sorted by jarak.
    ///
    /// Flow:
    ///   LocationService.OnLocationAcquired → FetchNearbyMosques()
    ///   → Parse → Sort by distance → OnMosquesLoaded event
    /// </summary>
    public class MosqueService : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        public static MosqueService Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────
        public event Action<List<MosqueInfo>> OnMosquesLoaded;
        public event Action<string>           OnMosqueError;
        public event Action                   OnFetchStarted;

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        [Header("Search Config")]
        [SerializeField] private float _radiusMeters  = 3000f;   // radius pencarian
        [SerializeField] private int   _maxResults    = 30;       // max item di list
        [SerializeField] private float _cacheMinutes  = 30f;      // cache valid berapa menit

        private const string OVERPASS_URL  = "https://overpass-api.de/api/interpreter";
        private const string PREFS_KEY     = "mosque_cached_json";
        private const string PREFS_DATE    = "mosque_cached_time";
        private const string PREFS_LAT     = "mosque_cached_lat";
        private const string PREFS_LON     = "mosque_cached_lon";

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────
        public List<MosqueInfo> NearbyMosques { get; private set; } = new List<MosqueInfo>();
        private bool _isFetching;

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
            // Tampilkan cache segera jika ada dan masih valid
            TryLoadFromCache();

            // Subscribe ke LocationService
            if (LocationService.Instance != null)
                LocationService.Instance.OnLocationAcquired += OnLocationAcquired;
            else
                Debug.LogError("[MosqueService] LocationService.Instance null!");
        }

        private void OnDestroy()
        {
            if (LocationService.Instance != null)
                LocationService.Instance.OnLocationAcquired -= OnLocationAcquired;
        }

        // ─────────────────────────────────────────────
        // Location Callback
        // ─────────────────────────────────────────────
        private void OnLocationAcquired(float lat, float lon)
        {
            if (_isFetching) return;

            // Cek apakah cache masih valid untuk lokasi ini
            if (IsCacheValid(lat, lon))
            {
                Debug.Log("[MosqueService] Cache valid, skip fetch.");
                return;
            }

            StartCoroutine(FetchNearbyMosques(lat, lon));
        }

        // ─────────────────────────────────────────────
        // Public: Manual refresh
        // ─────────────────────────────────────────────
        public void RequestRefresh()
        {
            if (LocationService.Instance != null && !_isFetching)
                StartCoroutine(FetchNearbyMosques(
                    LocationService.Instance.CurrentLat,
                    LocationService.Instance.CurrentLon));
        }

        // ─────────────────────────────────────────────
        // Fetch
        // ─────────────────────────────────────────────
        private IEnumerator FetchNearbyMosques(float lat, float lon)
        {
            _isFetching = true;
            OnFetchStarted?.Invoke();

            // Overpass QL query:
            // Cari node, way, relation dengan amenity=place_of_worship & religion=muslim
            // dalam radius _radiusMeters dari koordinat user
            // [out:json] = format JSON
            // [timeout:15] = server timeout
            string query = $@"[out:json][timeout:15];
(
  node[""amenity""=""place_of_worship""][""religion""=""muslim""](around:{_radiusMeters},{lat},{lon});
  way[""amenity""=""place_of_worship""][""religion""=""muslim""](around:{_radiusMeters},{lat},{lon});
  relation[""amenity""=""place_of_worship""][""religion""=""muslim""](around:{_radiusMeters},{lat},{lon});
);
out center {_maxResults * 2};";

            Debug.Log($"[MosqueService] Fetching mosques — radius:{_radiusMeters}m, lat:{lat}, lon:{lon}");

            byte[] bodyBytes = Encoding.UTF8.GetBytes("data=" + UnityWebRequest.EscapeURL(query));

            using (UnityWebRequest req = new UnityWebRequest(OVERPASS_URL, "POST"))
            {
                req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                req.timeout = 20;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MosqueService] Fetch error: {req.error}");
                    OnMosqueError?.Invoke("Gagal memuat data masjid. Coba lagi nanti.");
                    _isFetching = false;
                    yield break;
                }

                string json = req.downloadHandler.text;
                ParseAndApply(json, lat, lon);
                SaveToCache(json, lat, lon);
            }

            _isFetching = false;
        }

        // ─────────────────────────────────────────────
        // Parse
        // ─────────────────────────────────────────────
        private void ParseAndApply(string json, float userLat, float userLon)
        {
            try
            {
                OverpassResponse resp = JsonUtility.FromJson<OverpassResponse>(json);

                if (resp == null || resp.elements == null || resp.elements.Count == 0)
                {
                    Debug.LogWarning("[MosqueService] Tidak ada hasil dari Overpass.");
                    OnMosqueError?.Invoke("Tidak ada masjid/mushola ditemukan dalam radius ini.");
                    return;
                }

                var results = new List<MosqueInfo>();

                foreach (var el in resp.elements)
                {
                    // Koordinat: node punya lat/lon langsung, way/relation punya "center"
                    float elLat = el.lat != 0 ? el.lat : el.center?.lat ?? 0;
                    float elLon = el.lon != 0 ? el.lon : el.center?.lon ?? 0;

                    if (elLat == 0 && elLon == 0) continue;

                    // Nama — skip jika tidak ada nama
                    string name = el.tags?.name;
                    if (string.IsNullOrEmpty(name)) name = "Masjid/Mushola";

                    // Tipe: mushola/musalla atau masjid
                    string type = "Masjid";
                    string pow  = el.tags?.place_of_worship?.ToLower() ?? "";
                    if (pow.Contains("musalla") || pow.Contains("mushola") || pow.Contains("surau"))
                        type = "Mushola";
                    else if (name.ToLower().Contains("mushola") || name.ToLower().Contains("musolla"))
                        type = "Mushola";
                    else if (name.ToLower().Contains("surau"))
                        type = "Surau";

                    float dist = GetDistanceKm(userLat, userLon, elLat, elLon);

                    results.Add(new MosqueInfo
                    {
                        osmId       = el.id,
                        name        = name,
                        type        = type,
                        lat         = elLat,
                        lon         = elLon,
                        distanceKm  = dist,
                    });
                }

                // Sort terdekat dulu
                results.Sort((a, b) => a.distanceKm.CompareTo(b.distanceKm));

                // Trim ke max results
                if (results.Count > _maxResults)
                    results = results.GetRange(0, _maxResults);

                NearbyMosques = results;
                Debug.Log($"[MosqueService] Loaded {results.Count} mosques.");
                OnMosquesLoaded?.Invoke(NearbyMosques);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MosqueService] Parse error: {ex.Message}");
                OnMosqueError?.Invoke("Gagal memproses data masjid.");
            }
        }

        // ─────────────────────────────────────────────
        // Cache
        // ─────────────────────────────────────────────
        private void SaveToCache(string json, float lat, float lon)
        {
            PlayerPrefs.SetString(PREFS_KEY,  json);
            PlayerPrefs.SetString(PREFS_DATE, DateTime.Now.ToString("o"));
            PlayerPrefs.SetFloat(PREFS_LAT,   lat);
            PlayerPrefs.SetFloat(PREFS_LON,   lon);
            PlayerPrefs.Save();
        }

        private bool TryLoadFromCache()
        {
            if (!PlayerPrefs.HasKey(PREFS_KEY)) return false;

            try
            {
                string json   = PlayerPrefs.GetString(PREFS_KEY);
                float  lat    = PlayerPrefs.GetFloat(PREFS_LAT, 0);
                float  lon    = PlayerPrefs.GetFloat(PREFS_LON, 0);

                if (string.IsNullOrEmpty(json)) return false;

                ParseAndApply(json, lat, lon);
                return true;
            }
            catch { return false; }
        }

        private bool IsCacheValid(float lat, float lon)
        {
            if (!PlayerPrefs.HasKey(PREFS_DATE)) return false;

            try
            {
                DateTime saved = DateTime.Parse(PlayerPrefs.GetString(PREFS_DATE));
                if ((DateTime.Now - saved).TotalMinutes > _cacheMinutes) return false;

                float cachedLat = PlayerPrefs.GetFloat(PREFS_LAT, 0);
                float cachedLon = PlayerPrefs.GetFloat(PREFS_LON, 0);

                // Cache invalid jika lokasi berubah lebih dari 500m
                return GetDistanceKm(lat, lon, cachedLat, cachedLon) < 0.5f;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────
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
