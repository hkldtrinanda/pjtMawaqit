using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PrayerSystem.UI
{
    /// <summary>
    /// QiblaUIController — Panel arah Qibla.
    ///
    /// Hierarchy yang disarankan:
    ///   PanelQibla (QiblaUIController)
    ///   ├── ImgNeedle          ← Image panah/jarum — di-rotate setiap frame
    ///   ├── TxtDegrees         ← "291.5°"
    ///   ├── TxtDirection       ← "Barat Laut"
    ///   ├── TxtInfo            ← "Qibla berjarak 7.832 km"  (opsional)
    ///   └── OverlayCalibration ← Panel peringatan kalibrasi (opsional)
    /// </summary>
    public class QiblaUIController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────
        [Header("Needle")]
        [SerializeField] private RectTransform _needle;       // Image panah yang diputar
        [SerializeField] private bool _invertNeedle = false;  // flip jika sprite menunjuk ke bawah

        [Header("Labels")]
        [SerializeField] private TMP_Text _degreesLabel;      // "291.5°"
        [SerializeField] private TMP_Text _directionLabel;    // "Barat Laut"
        [SerializeField] private TMP_Text _infoLabel;         // "Qibla 291.5° dari Utara" (opsional)

        [Header("Calibration Warning")]
        [SerializeField] private GameObject _calibrationOverlay;  // tampil saat akurasi rendah
        [SerializeField] private float      _accuracyThreshold = 30f; // derajat — di atas ini tampilkan warning

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private bool _hasStarted = false;

        private void Start()
        {
            // Subscribe di Start() — dijamin semua Awake() sudah selesai
            SubscribeAndResume();
            _hasStarted = true;
        }

        private void OnEnable()
        {
            // OnEnable sebelum Start (frame pertama) — skip, Start yang handle
            if (!_hasStarted) return;
            SubscribeAndResume();
        }

        private void OnDisable()
        {
            UnsubscribeAndStop();
        }

        private void SubscribeAndResume()
        {
            if (QiblaService.Instance == null)
            {
                Debug.LogError("[QiblaUIController] QiblaService.Instance null! " +
                               "Pastikan QiblaService ada di scene.");
                return;
            }

            QiblaService.Instance.OnCompassUpdated      += OnCompassUpdated;
            QiblaService.Instance.OnCompassAvailability += OnCompassAvailability;

            // Resume kompas saat panel dibuka
            QiblaService.Instance.ResumeCompass();

            // Jika sudah ada data langsung tampilkan
            if (QiblaService.Instance.IsCompassActive)
                UpdateUI(QiblaService.Instance.NeedleAngle,
                         QiblaService.Instance.QiblaBearing,
                         QiblaService.Instance.CompassHeading);
        }

        private void UnsubscribeAndStop()
        {
            if (QiblaService.Instance == null) return;

            QiblaService.Instance.OnCompassUpdated      -= OnCompassUpdated;
            QiblaService.Instance.OnCompassAvailability -= OnCompassAvailability;

            // Stop kompas saat panel tutup — hemat baterai
            QiblaService.Instance.StopCompass();
        }

        // ─────────────────────────────────────────────
        // Event Callbacks
        // ─────────────────────────────────────────────
        private void OnCompassUpdated(float needleAngle, float qiblaBearing, float compassHeading)
        {
            UpdateUI(needleAngle, qiblaBearing, compassHeading);
        }

        private void OnCompassAvailability(bool available)
        {
            // Tampilkan warning jika kompas tidak tersedia
            if (!available && _calibrationOverlay)
                _calibrationOverlay.SetActive(true);
        }

        // ─────────────────────────────────────────────
        // UI Update
        // ─────────────────────────────────────────────
        private void UpdateUI(float needleAngle, float qiblaBearing, float compassHeading)
        {
            // ── Rotasi Jarum ──────────────────────────────────────────────────
            if (_needle)
            {
                float rotation = _invertNeedle ? -needleAngle : needleAngle;
                // Rotasi di Z axis (2D UI)
                _needle.localRotation = Quaternion.Euler(0f, 0f, rotation);
            }

            // ── Label Derajat ─────────────────────────────────────────────────
            // Tampilkan bearing Qibla dari North (bukan needleAngle)
            if (_degreesLabel)
                _degreesLabel.text = $"{qiblaBearing:F1}°";

            // ── Label Arah ────────────────────────────────────────────────────
            if (_directionLabel)
                _directionLabel.text = QiblaService.GetDirectionLabel(qiblaBearing);

            // ── Info ──────────────────────────────────────────────────────────
            if (_infoLabel)
                _infoLabel.text = $"Qibla {qiblaBearing:F1}° dari Utara";

            // ── Cek akurasi kompas ────────────────────────────────────────────
            // Input.compass.headingAccuracy = estimasi error dalam derajat
            // Semakin kecil semakin akurat (0 = sangat akurat)
            if (_calibrationOverlay)
            {
                float accuracy = Input.compass.headingAccuracy;
                // accuracy = 0 artinya tidak diketahui (beberapa device)
                bool needsCalibration = accuracy > _accuracyThreshold && accuracy != 0;
                _calibrationOverlay.SetActive(needsCalibration);
            }
        }
    }
}