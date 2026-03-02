using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace PrayerSystem
{
    /// <summary>
    /// LocationService — Singleton.
    /// Menangani:
    ///   1. Runtime permission request (Android / iOS)
    ///   2. GPS acquisition dengan timeout
    ///   3. Reverse geocoding via OpenStreetMap Nominatim (gratis, no API key)
    ///   4. Event ke subscriber (UI, PrayerTimeManager)
    /// </summary>
    public class LocationService : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        public static LocationService Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────

        /// <summary>Dipanggil setiap kali status GPS berubah — UI subscribe untuk update label.</summary>
        public event Action<GpsStatus, string> OnStatusChanged;

        /// <summary>Dipanggil saat koordinat berhasil didapat.</summary>
        public event Action<float, float> OnLocationAcquired;

        /// <summary>Dipanggil saat nama kota/daerah berhasil di-resolve.</summary>
        public event Action<string, float, float> OnLocationResolved; // cityName, lat, lon

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        private const float  DEFAULT_LAT     = -6.2088f;
        private const float  DEFAULT_LON     = 106.8456f;
        private const string DEFAULT_CITY    = "Jakarta (Default)";
        private const int    GPS_TIMEOUT_SEC = 15;
        private const string NOMINATIM_URL   = "https://nominatim.openstreetmap.org/reverse";
        private const string PREFS_CITY_KEY  = "prayer_cached_city";
        private const string PREFS_LAT_KEY   = "prayer_cached_gps_lat";
        private const string PREFS_LON_KEY   = "prayer_cached_gps_lon";

        // ─────────────────────────────────────────────
        // Public State
        // ─────────────────────────────────────────────
        public float   CurrentLat  { get; private set; } = DEFAULT_LAT;
        public float   CurrentLon  { get; private set; } = DEFAULT_LON;
        public string  CityName    { get; private set; } = DEFAULT_CITY;

        /// <summary>"Cakung, Jakarta Timur" — format lengkap kelurahan + kota</summary>
        public string  LocationDetail { get; private set; } = DEFAULT_CITY;

        /// <summary>Hanya level kelurahan/desa: "Cakung"</summary>
        public string  SubDistrict  { get; private set; } = "";

        /// <summary>Hanya level kota/kabupaten: "Jakarta Timur"</summary>
        public string  CityOnly     { get; private set; } = "";
        public GpsStatus Status    { get; private set; } = GpsStatus.Idle;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load cached city name agar UI langsung tampil saat offline
            LoadCachedLocation();
        }

        private void Start()
        {
            StartCoroutine(AcquireLocationRoutine());
        }

        // ─────────────────────────────────────────────
        // Public: Paksa refresh (dipanggil DayChangeWatcher / PrayerTimeManager)
        // ─────────────────────────────────────────────
        public void RequestRefresh()
        {
            StartCoroutine(AcquireLocationRoutine());
        }

        // ─────────────────────────────────────────────
        // Main Flow
        // ─────────────────────────────────────────────
        private IEnumerator AcquireLocationRoutine()
        {
            SetStatus(GpsStatus.Detecting, "Mendeteksi lokasi...");

#if UNITY_EDITOR
            // ── Editor: simulasi GPS berhasil dengan Jakarta ──────────────────
            yield return new WaitForSeconds(0.5f); // simulasi delay kecil
            CurrentLat = DEFAULT_LAT;
            CurrentLon = DEFAULT_LON;
            SetStatus(GpsStatus.Found, $"Editor Mode ({DEFAULT_LAT:F4}, {DEFAULT_LON:F4})");
            OnLocationAcquired?.Invoke(CurrentLat, CurrentLon);
            yield return StartCoroutine(ReverseGeocode(CurrentLat, CurrentLon));
            yield break;
#endif

#pragma warning disable CS0162 // Unreachable code (expected in Editor)

            // ── Step 1: Request permission ─────────────────────────────────────
            yield return StartCoroutine(RequestPermissionRoutine());

            if (!HasLocationPermission())
            {
                UseFallback("Izin lokasi ditolak");
                yield break;
            }

            // ── Step 2: Start location service ───────────────────────────────
            if (!Input.location.isEnabledByUser)
            {
                UseFallback("GPS tidak aktif di perangkat");
                yield break;
            }

            Input.location.Start(10f, 10f);

            int timeout = GPS_TIMEOUT_SEC;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
            {
                SetStatus(GpsStatus.Detecting, $"Mendeteksi GPS... ({timeout}s)");
                yield return new WaitForSeconds(1f);
                timeout--;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Input.location.Stop();
                UseFallback($"GPS timeout (status: {Input.location.status})");
                yield break;
            }

            // ── Step 3: Koordinat berhasil ────────────────────────────────────
            CurrentLat = Input.location.lastData.latitude;
            CurrentLon = Input.location.lastData.longitude;
            Input.location.Stop();

            SetStatus(GpsStatus.Found, $"{CurrentLat:F4}, {CurrentLon:F4}");
            OnLocationAcquired?.Invoke(CurrentLat, CurrentLon);

            // ── Step 4: Reverse geocoding ─────────────────────────────────────
            yield return StartCoroutine(ReverseGeocode(CurrentLat, CurrentLon));

#pragma warning restore CS0162
        }

        // ─────────────────────────────────────────────
        // Permission (Android runtime request)
        // ─────────────────────────────────────────────
        private IEnumerator RequestPermissionRoutine()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                SetStatus(GpsStatus.Detecting, "Meminta izin lokasi...");

                bool responded  = false;
                bool granted    = false;

                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted    += _ => { granted = true;  responded = true; };
                callbacks.PermissionDenied     += _ => { granted = false; responded = true; };
                callbacks.PermissionDeniedAndDontAskAgain += _ => { granted = false; responded = true; };

                Permission.RequestUserPermission(Permission.FineLocation, callbacks);

                // Tunggu user response (max 30 detik)
                float waitTime = 0f;
                while (!responded && waitTime < 30f)
                {
                    yield return new WaitForSeconds(0.5f);
                    waitTime += 0.5f;
                }

                if (!granted)
                    Debug.LogWarning("[LocationService] Permission FineLocation ditolak.");
            }
#elif UNITY_IOS
            // iOS: permission di-request otomatis saat Input.location.Start() dipanggil
            // Pastikan NSLocationWhenInUseUsageDescription ada di Info.plist
            yield return null;
#else
            yield return null;
#endif
        }

        private bool HasLocationPermission()
        {
#if UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission(Permission.FineLocation);
#else
            return Input.location.isEnabledByUser;
#endif
        }

        // ─────────────────────────────────────────────
        // Reverse Geocoding — OpenStreetMap Nominatim
        // ─────────────────────────────────────────────
        private IEnumerator ReverseGeocode(float lat, float lon)
        {
            string url = $"{NOMINATIM_URL}?lat={lat}&lon={lon}&format=json&accept-language=id&zoom=16";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                // Nominatim mensyaratkan User-Agent yang identifiable
                req.SetRequestHeader("User-Agent", "PrayerTimesApp/1.0 (Unity)");
                req.timeout = 8;

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[LocationService] Reverse geocode gagal: {req.error}");
                    // Tampilkan koordinat saja sebagai fallback
                    CityName = $"{lat:F4}, {lon:F4}";
                    NotifyResolved();
                    yield break;
                }

                string city = ParseNominatimCity(req.downloadHandler.text, lat, lon);
                CityName = city;
                SaveCachedLocation(city, lat, lon);
                NotifyResolved();
            }
        }

        /// <summary>
        /// Parse JSON Nominatim — format output: "Cakung, Jakarta Timur"
        ///
        /// Level 1 (kiri)  — Kelurahan/Desa:
        ///   suburb → village → city_district → hamlet → neighbourhood
        ///
        /// Level 2 (kanan) — Kota/Kabupaten:
        ///   city → town → county → state
        /// </summary>
        private string ParseNominatimCity(string json, float lat, float lon)
        {
            try
            {
                NominatimResponse resp = JsonUtility.FromJson<NominatimResponse>(json);
                if (resp == null || resp.address == null)
                    return $"{lat:F4}, {lon:F4}";

                NominatimAddress addr = resp.address;

                // ── Level 1: Kelurahan / Desa (sub-district) ──────────────────
                // suburb         = kelurahan dalam kota besar (Cakung, Kemayoran, dll)
                // village        = desa di area rural/suburban
                // city_district  = kecamatan (kadang muncul di beberapa daerah)
                // hamlet         = dusun
                // neighbourhood  = nama kawasan/komplek
                string sub = !string.IsNullOrEmpty(addr.suburb)        ? addr.suburb
                           : !string.IsNullOrEmpty(addr.village)       ? addr.village
                           : !string.IsNullOrEmpty(addr.city_district) ? addr.city_district
                           : !string.IsNullOrEmpty(addr.hamlet)        ? addr.hamlet
                           : !string.IsNullOrEmpty(addr.neighbourhood) ? addr.neighbourhood
                           : "";

                // ── Level 2: Kota / Kabupaten ─────────────────────────────────
                // city   = kota madya (Jakarta Timur, Surabaya, Bandung, dll)
                // town   = kota kecil / kota di luar DKI
                // county = kabupaten
                // state  = provinsi (fallback terakhir)
                string city = !string.IsNullOrEmpty(addr.city)   ? addr.city
                            : !string.IsNullOrEmpty(addr.town)   ? addr.town
                            : !string.IsNullOrEmpty(addr.county) ? addr.county
                            : !string.IsNullOrEmpty(addr.state)  ? addr.state
                            : "";

                // ── Gabungkan ─────────────────────────────────────────────────
                string fullLocation;
                if (!string.IsNullOrEmpty(sub) && !string.IsNullOrEmpty(city))
                    fullLocation = $"{sub}, {city}";        // "Cakung, Jakarta Timur"
                else if (!string.IsNullOrEmpty(sub))
                    fullLocation = sub;
                else if (!string.IsNullOrEmpty(city))
                    fullLocation = city;
                else
                    fullLocation = $"{lat:F4}, {lon:F4}";   // fallback koordinat

                // Tambah negara jika bukan Indonesia
                if (!string.IsNullOrEmpty(addr.country_code) &&
                    addr.country_code.ToLower() != "id" &&
                    !string.IsNullOrEmpty(addr.country))
                {
                    city         = $"{city}, {addr.country}";
                    fullLocation = $"{fullLocation}, {addr.country}";
                }

                // Simpan ke property publik terpisah agar UI bisa pilih mana yang ditampilkan
                SubDistrict    = sub;
                CityOnly       = city;
                LocationDetail = fullLocation;

                Debug.Log($"[LocationService] Location resolved: {fullLocation}");
                return fullLocation;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocationService] Nominatim parse error: {ex.Message}");
                return $"{lat:F4}, {lon:F4}";
            }
        }

        private void NotifyResolved()
        {
            // Update status dengan gabungan city + koordinat
            string statusText = Status == GpsStatus.Fallback
                ? $"{CityName} ⚠️"
                : CityName;
            SetStatus(Status, statusText);
            OnLocationResolved?.Invoke(CityName, CurrentLat, CurrentLon);
        }

        // ─────────────────────────────────────────────
        // Fallback
        // ─────────────────────────────────────────────
        private void UseFallback(string reason)
        {
            Debug.LogWarning($"[LocationService] Fallback aktif — {reason}");
            CurrentLat = DEFAULT_LAT;
            CurrentLon = DEFAULT_LON;
            SetStatus(GpsStatus.Fallback, $"Jakarta (Fallback)");
            OnLocationAcquired?.Invoke(CurrentLat, CurrentLon);

            // Coba load city dari cache, kalau tidak ada pakai default
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(PREFS_CITY_KEY, "")))
            {
                CityName = PlayerPrefs.GetString(PREFS_CITY_KEY);
                NotifyResolved();
            }
            else
            {
                CityName = DEFAULT_CITY;
                NotifyResolved();
            }
        }

        // ─────────────────────────────────────────────
        // Status Helper
        // ─────────────────────────────────────────────
        private void SetStatus(GpsStatus status, string message)
        {
            Status = status;
            OnStatusChanged?.Invoke(status, message);
        }

        // ─────────────────────────────────────────────
        // Cache
        // ─────────────────────────────────────────────
        private void SaveCachedLocation(string city, float lat, float lon)
        {
            PlayerPrefs.SetString(PREFS_CITY_KEY, city);           // simpan LocationDetail
            PlayerPrefs.SetFloat(PREFS_LAT_KEY, lat);
            PlayerPrefs.SetFloat(PREFS_LON_KEY, lon);
            PlayerPrefs.Save();
        }

        private void LoadCachedLocation()
        {
            string cached = PlayerPrefs.GetString(PREFS_CITY_KEY, "");
            if (!string.IsNullOrEmpty(cached))
            {
                CityName       = cached;
                LocationDetail = cached;  // cache sudah menyimpan format lengkap
                CurrentLat     = PlayerPrefs.GetFloat(PREFS_LAT_KEY, DEFAULT_LAT);
                CurrentLon     = PlayerPrefs.GetFloat(PREFS_LON_KEY, DEFAULT_LON);
                Debug.Log($"[LocationService] Loaded cached location: {CityName}");
            }
        }
    }

    // ─────────────────────────────────────────────────────
    // Enums & Nominatim Models
    // ─────────────────────────────────────────────────────
    public enum GpsStatus { Idle, Detecting, Found, Fallback }

    [Serializable]
    public class NominatimResponse
    {
        public NominatimAddress address;
    }

    [Serializable]
    public class NominatimAddress
    {
        // ── Level Kelurahan / Desa ──────────────
        public string suburb;           // Kelurahan dalam kota besar: "Cakung"
        public string village;          // Desa di area rural
        public string city_district;    // Kecamatan
        public string hamlet;           // Dusun
        public string neighbourhood;    // Nama kawasan / komplek

        // ── Level Kota / Kabupaten ──────────────
        public string city;             // Kota madya: "Jakarta Timur"
        public string town;             // Kota kecil
        public string county;           // Kabupaten
        public string state;            // Provinsi

        // ── Negara ──────────────────────────────
        public string country;
        public string country_code;     // "id", "sg", "my", dll
    }
}