using System;
using UnityEngine;

namespace PrayerSystem
{
    /// <summary>
    /// QiblaService — Singleton.
    /// Sensor fusion: Gyroscope + Compass (Magnetometer).
    ///
    /// Strategi:
    ///   • Gyroscope  → smooth, fast, tidak drift dalam jangka pendek
    ///                  dipakai untuk rotasi frame-to-frame (high frequency)
    ///   • Compass    → absolute heading reference, lambat dan sedikit noisy
    ///                  dipakai untuk koreksi drift gyro (low frequency)
    ///   • Fusion     → Complementary Filter:
    ///                  heading = α × (heading + gyro_delta) + (1-α) × compass
    ///                  α = 0.98 → 98% gyro, 2% compass koreksi per frame
    ///
    /// Fallback: jika gyro tidak tersedia, pakai compass saja.
    /// </summary>
    public class QiblaService : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        public static QiblaService Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────
        /// <summary>Parameter: (needleAngle, qiblaBearing, deviceHeading)</summary>
        public event Action<float, float, float> OnCompassUpdated;
        public event Action<bool>                OnCompassAvailability;

        // ─────────────────────────────────────────────
        // Ka'bah Coordinates
        // ─────────────────────────────────────────────
        private const float KAABAH_LAT = 21.4225f;
        private const float KAABAH_LON = 39.8262f;

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        [Header("Sensor Fusion")]
        [Tooltip("Complementary filter alpha. 0.98 = 98% gyro + 2% compass correction.")]
        [Range(0.9f, 0.99f)]
        [SerializeField] private float _alpha = 0.98f;

        [Tooltip("Lerp speed untuk smooth visual jarum.")]
        [SerializeField] private float _needleSmoothSpeed = 10f;

        [Tooltip("Interval update dalam detik. 0.016 = 60fps.")]
        [SerializeField] private float _updateInterval = 0.016f;

        // ─────────────────────────────────────────────
        // Public State
        // ─────────────────────────────────────────────
        public float QiblaBearing   { get; private set; }
        public float DeviceHeading  { get; private set; }  // fused heading
        public float NeedleAngle    { get; private set; }
        public bool  IsActive       { get; private set; }
        public bool  HasGyro        { get; private set; }

        // Alias untuk backward compat dengan QiblaUIController
        public bool  IsCompassActive => IsActive;
        public float CompassHeading  => DeviceHeading;

        // ─────────────────────────────────────────────
        // Private — Sensor Fusion State
        // ─────────────────────────────────────────────
        private float _fusedHeading;        // heading hasil fusion (derajat, 0-360)
        private float _smoothedNeedle;
        private float _lastUpdateTime;
        private float _lastGyroUpdateTime;
        private bool  _locationReady;
        private bool  _fusedHeadingInitialized;

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
            if (LocationService.Instance != null)
            {
                LocationService.Instance.OnLocationAcquired += OnLocationAcquired;

                if (LocationService.Instance.CurrentLat != 0)
                    OnLocationAcquired(LocationService.Instance.CurrentLat,
                                       LocationService.Instance.CurrentLon);
            }
        }

        private void OnDestroy()
        {
            if (LocationService.Instance != null)
                LocationService.Instance.OnLocationAcquired -= OnLocationAcquired;

            StopSensors();
        }

        private void Update()
        {
            if (!IsActive || !_locationReady) return;
            if (Time.time - _lastUpdateTime < _updateInterval) return;
            _lastUpdateTime = Time.time;

            float compassHeading = ReadCompassHeading();
            float fusedHeading   = HasGyro
                ? FuseWithGyro(compassHeading)
                : compassHeading;

            DeviceHeading = fusedHeading;

            // Hitung sudut jarum → jalur terpendek ke Qibla
            float rawNeedle = NormalizeAngle(QiblaBearing - DeviceHeading);
            _smoothedNeedle = Mathf.LerpAngle(_smoothedNeedle, rawNeedle,
                                              Time.deltaTime * _needleSmoothSpeed);
            NeedleAngle = _smoothedNeedle;

            OnCompassUpdated?.Invoke(NeedleAngle, QiblaBearing, DeviceHeading);
        }

        // ─────────────────────────────────────────────
        // Sensor Fusion — Complementary Filter
        // ─────────────────────────────────────────────

        /// <summary>
        /// Complementary Filter:
        ///   fusedHeading = α × (fusedHeading + gyro_delta) + (1-α) × compass
        ///
        /// Gyro memberikan perubahan sudut yang smooth dan responsif.
        /// Compass mengoreksi drift akumulasi dari gyro secara perlahan.
        /// </summary>
        private float FuseWithGyro(float compassHeading)
        {
            // Inisialisasi dengan compass pada frame pertama
            if (!_fusedHeadingInitialized)
            {
                _fusedHeading = compassHeading;
                _fusedHeadingInitialized = true;
                _lastGyroUpdateTime = Time.time;
                return _fusedHeading;
            }

            float dt = Time.time - _lastGyroUpdateTime;
            _lastGyroUpdateTime = Time.time;

            // Gyro rotationRateUnbiased = angular velocity dalam rad/s
            // Ambil sumbu Y (yaw = rotasi horizontal HP)
            // Input.gyro.rotationRateUnbiased sudah dikompensasi bias sensor
            float gyroYawDeg = -Input.gyro.rotationRateUnbiased.y * Mathf.Rad2Deg * dt;
            // Negatif karena koordinat gyro Unity berlawanan arah jarum jam

            // Prediksi heading berdasarkan gyro
            float gyroPrediction = NormalizeAngle360(_fusedHeading + gyroYawDeg);

            // Koreksi dengan compass (complementary filter)
            // Gunakan LerpAngle untuk handle wrap 0/360
            _fusedHeading = LerpAngle360(gyroPrediction, compassHeading, 1f - _alpha);

            return _fusedHeading;
        }

        // ─────────────────────────────────────────────
        // Compass Reading
        // ─────────────────────────────────────────────
        private float ReadCompassHeading()
        {
            // trueHeading lebih akurat (mempertimbangkan magnetic declination)
            // hanya tersedia jika GPS aktif — fallback ke magneticHeading
            float heading = Input.compass.trueHeading > 0
                ? Input.compass.trueHeading
                : Input.compass.magneticHeading;

            return heading;
        }

        // ─────────────────────────────────────────────
        // Sensor Lifecycle
        // ─────────────────────────────────────────────
        private void OnLocationAcquired(float lat, float lon)
        {
            QiblaBearing   = CalculateQiblaBearing(lat, lon);
            _locationReady = true;
            Debug.Log($"[QiblaService] Qibla bearing: {QiblaBearing:F1}°");
            StartSensors();
        }

        private void StartSensors()
        {
            if (IsActive) return;

            // Compass — wajib
            Input.compass.enabled = true;
            Input.location.Start(); // untuk trueHeading

            // Gyroscope — opsional, cek ketersediaan
            HasGyro = SystemInfo.supportsGyroscope;
            if (HasGyro)
            {
                Input.gyro.enabled = true;
                Input.gyro.updateInterval = _updateInterval;
                Debug.Log("[QiblaService] Gyroscope enabled — sensor fusion aktif.");
            }
            else
            {
                Debug.Log("[QiblaService] Gyroscope tidak tersedia — compass only.");
            }

            _fusedHeadingInitialized = false;
            IsActive = true;
            OnCompassAvailability?.Invoke(true);
        }

        public void StopCompass() => StopSensors();

        private void StopSensors()
        {
            if (!IsActive) return;

            Input.compass.enabled = false;
            if (HasGyro) Input.gyro.enabled = false;

            IsActive = false;
            Debug.Log("[QiblaService] Sensors stopped.");
        }

        public void ResumeCompass()
        {
            if (_locationReady) StartSensors();
        }

        // ─────────────────────────────────────────────
        // Qibla Bearing Formula — Great Circle
        // ─────────────────────────────────────────────
        private static float CalculateQiblaBearing(float userLat, float userLon)
        {
            float lat1 = userLat    * Mathf.Deg2Rad;
            float lat2 = KAABAH_LAT * Mathf.Deg2Rad;
            float dLon = (KAABAH_LON - userLon) * Mathf.Deg2Rad;

            float y = Mathf.Sin(dLon) * Mathf.Cos(lat2);
            float x = Mathf.Cos(lat1) * Mathf.Sin(lat2)
                    - Mathf.Sin(lat1) * Mathf.Cos(lat2) * Mathf.Cos(dLon);

            return (Mathf.Atan2(y, x) * Mathf.Rad2Deg + 360f) % 360f;
        }

        // ─────────────────────────────────────────────
        // Math Helpers
        // ─────────────────────────────────────────────

        /// <summary>Normalisasi ke -180 ~ +180 (untuk LerpAngle)</summary>
        private static float NormalizeAngle(float a)
        {
            while (a >  180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }

        /// <summary>Normalisasi ke 0 ~ 360</summary>
        private static float NormalizeAngle360(float a)
        {
            return (a % 360f + 360f) % 360f;
        }

        /// <summary>Lerp sudut dalam range 0-360 via jalur terpendek</summary>
        private static float LerpAngle360(float a, float b, float t)
        {
            float diff = NormalizeAngle(b - a);
            return NormalizeAngle360(a + diff * t);
        }

        /// <summary>Deskripsi arah dalam Bahasa Indonesia</summary>
        public static string GetDirectionLabel(float bearing)
        {
            bearing = NormalizeAngle360(bearing);
            if (bearing >= 337.5f || bearing < 22.5f)  return "Utara";
            if (bearing <  67.5f)                       return "Timur Laut";
            if (bearing <  112.5f)                      return "Timur";
            if (bearing <  157.5f)                      return "Tenggara";
            if (bearing <  202.5f)                      return "Selatan";
            if (bearing <  247.5f)                      return "Barat Daya";
            if (bearing <  292.5f)                      return "Barat";
            return "Barat Laut";
        }
    }
}