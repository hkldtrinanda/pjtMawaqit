using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrayerSystem.Models;

namespace PrayerSystem.UI
{
    /// <summary>
    /// PrayerUIController — Orchestrates seluruh tampilan:
    ///   • Grid vertikal 5 baris sholat
    ///   • Header: countdown, next prayer, tanggal
    ///   • Header: location label (nama kota + koordinat + status GPS)
    /// </summary>
    public class PrayerUIController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector — Grid
        // ─────────────────────────────────────────────
        [Header("Grid")]
        [SerializeField] private Transform  _gridContainer;
        [SerializeField] private GameObject _prayerItemPrefab;

        // ─────────────────────────────────────────────
        // Inspector — Header Prayer
        // ─────────────────────────────────────────────
        [Header("Header — Prayer")]
        [SerializeField] private TMP_Text _countdownLabel;    // "HH:mm:ss"
        [SerializeField] private TMP_Text _nextPrayerLabel;   // "Menuju Maghrib"
        [SerializeField] private TMP_Text _dateLabel;         // "Senin, 23 Februari 2026"

        // ─────────────────────────────────────────────
        // Inspector — Header Location  ← BARU
        // ─────────────────────────────────────────────
        [Header("Header — Location")]
        [SerializeField] private TMP_Text _cityNameLabel;     // "Jakarta Selatan"
        [SerializeField] private TMP_Text _coordsLabel;       // "-6.2088, 106.8456"

        [Header("GPS Status — Image")]
        [SerializeField] private Image _gpsStatusImage;   // Image yang warnanya berubah sesuai status
        [SerializeField] private Image _gpsStatusIcon;    // (Opsional) Icon/sprite berubah per status
        [SerializeField] private Sprite _spriteDetecting; // icon animasi / sinyal
        [SerializeField] private Sprite _spriteFound;     // icon centang / sinyal penuh
        [SerializeField] private Sprite _spriteFallback;  // icon warning

        [Header("GPS Status Colors")]
        [SerializeField] private Color _colorDetecting = new Color(1f, 0.85f, 0.2f);   // kuning
        [SerializeField] private Color _colorFound     = new Color(0.3f, 0.9f, 0.5f);  // hijau
        [SerializeField] private Color _colorFallback  = new Color(1f, 0.5f, 0.2f);    // oranye

        // ─────────────────────────────────────────────
        // Inspector — Loading / Error
        // ─────────────────────────────────────────────
        [Header("Error / Loading")]
        [SerializeField] private GameObject _loadingOverlay;
        [SerializeField] private TMP_Text   _errorLabel;

        // ─────────────────────────────────────────────
        // Runtime
        // ─────────────────────────────────────────────
        private readonly List<PrayerItemUI> _itemPool = new List<PrayerItemUI>();
        private PrayerCountdown _countdown;
        private bool _hasStarted;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            ValidateReferences();
            ShowLoading(true);
            UpdateDateLabel();
            _countdown = GetComponent<PrayerCountdown>();

            // Tampilkan city dari cache langsung (agar tidak kosong saat GPS belum selesai)
            if (LocationService.Instance != null)
                SetCityLabel(LocationService.Instance.CityName,
                             LocationService.Instance.CurrentLat,
                             LocationService.Instance.CurrentLon);

            SetGpsStatusLabel(GpsStatus.Detecting, "Mendeteksi lokasi...");
        }

        private void Start()
        {
            SubscribeEvents();

            // Pull data yang sudah ada (dari cache load)
            if (PrayerTimeManager.Instance?.PrayerTimes?.Count > 0)
                RefreshGrid(PrayerTimeManager.Instance.PrayerTimes);
        }

        private void OnEnable()
        {
            if (_hasStarted) SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        // ─────────────────────────────────────────────
        // Event Subscription
        // ─────────────────────────────────────────────
        private void SubscribeEvents()
        {
            if (PrayerTimeManager.Instance == null)
            {
                Debug.LogError("[PrayerUIController] PrayerTimeManager.Instance null!");
                return;
            }

            PrayerTimeManager.Instance.OnPrayerTimesUpdated += RefreshGrid;
            PrayerTimeManager.Instance.OnErrorOccurred       += ShowError;

            if (_countdown != null)
            {
                _countdown.OnCountdownTick     += UpdateCountdown;
                _countdown.OnNextPrayerChanged += UpdateNextPrayerLabel;
            }

            // ─ Location Events ─
            if (LocationService.Instance != null)
            {
                LocationService.Instance.OnStatusChanged    += OnGpsStatusChanged;
                LocationService.Instance.OnLocationResolved += OnLocationResolved;
            }

            _hasStarted = true;

            // ─ DateSettings Event ─
            DateSettings.Instance.OnDateChanged += OnDateModeChanged;
        }

        private void UnsubscribeEvents()
        {
            if (PrayerTimeManager.Instance != null)
            {
                PrayerTimeManager.Instance.OnPrayerTimesUpdated -= RefreshGrid;
                PrayerTimeManager.Instance.OnErrorOccurred       -= ShowError;
            }

            if (_countdown != null)
            {
                _countdown.OnCountdownTick     -= UpdateCountdown;
                _countdown.OnNextPrayerChanged -= UpdateNextPrayerLabel;
            }

            if (LocationService.Instance != null)
            {
                LocationService.Instance.OnStatusChanged    -= OnGpsStatusChanged;
                LocationService.Instance.OnLocationResolved -= OnLocationResolved;

            // ─ DateSettings ─
            DateSettings.Instance.OnDateChanged -= OnDateModeChanged;
            }
        }

        // ─────────────────────────────────────────────
        // Location Label Callbacks
        // ─────────────────────────────────────────────

        /// <summary>Update status GPS (Detecting / Found / Fallback) beserta warnanya.</summary>
        private void OnGpsStatusChanged(GpsStatus status, string message)
        {
            SetGpsStatusLabel(status, message);
        }

        /// <summary>Dipanggil setelah reverse geocoding selesai — update nama kota + koordinat.</summary>
        private void OnLocationResolved(string cityName, float lat, float lon)
        {
            SetCityLabel(cityName, lat, lon);
        }

        private void SetCityLabel(string cityName, float lat, float lon)
        {
            if (_cityNameLabel)
                _cityNameLabel.text = cityName;

            if (_coordsLabel)
                _coordsLabel.text = $"{lat:F4}, {lon:F4}";
        }

        private void SetGpsStatusLabel(GpsStatus status, string message)
        {
            Color targetColor;
            Sprite targetSprite;

            switch (status)
            {
                case GpsStatus.Found:
                    targetColor  = _colorFound;
                    targetSprite = _spriteFound;
                    break;
                case GpsStatus.Fallback:
                    targetColor  = _colorFallback;
                    targetSprite = _spriteFallback;
                    break;
                default: // Detecting / Idle
                    targetColor  = _colorDetecting;
                    targetSprite = _spriteDetecting;
                    break;
            }

            // Terapkan warna ke Image background status
            if (_gpsStatusImage)
                _gpsStatusImage.color = targetColor;

            // Ganti sprite icon jika di-assign
            if (_gpsStatusIcon && targetSprite != null)
                _gpsStatusIcon.sprite = targetSprite;
        }

        // ─────────────────────────────────────────────
        // Grid
        // ─────────────────────────────────────────────
        private void RefreshGrid(List<PrayerInfo> prayers)
        {
            ShowLoading(false);
            HideError();

            while (_itemPool.Count < prayers.Count)
            {
                GameObject go = Instantiate(_prayerItemPrefab, _gridContainer);
                PrayerItemUI item = go.GetComponent<PrayerItemUI>();
                if (item == null)
                {
                    Debug.LogError("[PrayerUIController] Prefab tidak memiliki PrayerItemUI!");
                    Destroy(go);
                    return;
                }
                _itemPool.Add(item);
            }

            for (int i = 0; i < prayers.Count; i++)
            {
                _itemPool[i].gameObject.SetActive(true);
                _itemPool[i].Bind(prayers[i]);
            }

            for (int i = prayers.Count; i < _itemPool.Count; i++)
                _itemPool[i].gameObject.SetActive(false);
        }

        private void RefreshHighlights(List<PrayerInfo> prayers)
        {
            for (int i = 0; i < _itemPool.Count && i < prayers.Count; i++)
                _itemPool[i].SetHighlight(prayers[i].isNext);
        }

        // ─────────────────────────────────────────────
        // Countdown Header
        // ─────────────────────────────────────────────
        private void UpdateCountdown(TimeSpan remaining, string nextPrayerName)
        {
            if (_countdownLabel)
                _countdownLabel.text = PrayerCountdown.FormatCountdown(remaining);

            if (PrayerTimeManager.Instance?.PrayerTimes != null)
                RefreshHighlights(PrayerTimeManager.Instance.PrayerTimes);
        }

        private void UpdateNextPrayerLabel(string nextName)
        {
            if (_nextPrayerLabel)
                _nextPrayerLabel.text = $"{nextName}";
        }

        // ─────────────────────────────────────────────
        // Loading / Error
        // ─────────────────────────────────────────────
        private void ShowLoading(bool show)
        {
            if (_loadingOverlay) _loadingOverlay.SetActive(show);
        }

        private void ShowError(string message)
        {
            ShowLoading(false);
            if (_errorLabel)
            {
                _errorLabel.gameObject.SetActive(true);
                _errorLabel.text = message;
            }
        }

        private void HideError()
        {
            if (_errorLabel) _errorLabel.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        // Date Label
        // ─────────────────────────────────────────────
        private void UpdateDateLabel()
        {
            if (_dateLabel)
                _dateLabel.text = DateSettings.Instance.GetFormattedDate();
        }

        /// <summary>Dipanggil saat user toggle Hijriah/Masehi.</summary>
        private void OnDateModeChanged(string formattedDate, CalendarMode mode)
        {
            if (_dateLabel)
                _dateLabel.text = formattedDate;
        }

        // ─────────────────────────────────────────────
        // Validation
        // ─────────────────────────────────────────────
        private void ValidateReferences()
        {
            if (!_gridContainer)    Debug.LogError("[PrayerUIController] _gridContainer belum di-assign!");
            if (!_prayerItemPrefab) Debug.LogError("[PrayerUIController] _prayerItemPrefab belum di-assign!");
            if (!_cityNameLabel)    Debug.LogWarning("[PrayerUIController] _cityNameLabel tidak di-assign.");
            if (!_coordsLabel)      Debug.LogWarning("[PrayerUIController] _coordsLabel tidak di-assign.");
            if (!_gpsStatusImage)   Debug.LogWarning("[PrayerUIController] _gpsStatusImage tidak di-assign.");
        }
    }
}