using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PrayerSystem.UI
{
    /// <summary>
    /// DateSettingsUI — Tombol toggle kalender Hijriah / Masehi.
    ///
    /// Attach script ini pada GameObject toggle/button.
    /// Bisa diletakkan di header berdampingan dengan _dateLabel,
    /// atau di panel Settings terpisah.
    ///
    /// Hierarchy yang disarankan (di Header):
    ///   HeaderDateRow
    ///   ├── TxtDate          ← TMP_Text (assign ke PrayerUIController._dateLabel)
    ///   └── BtnCalendarToggle (DateSettingsUI)
    ///       ├── TxtToggleLabel  ← TMP_Text — menampilkan "Hijriah" / "Masehi"
    ///       └── ImgIcon         ← Image (opsional, icon bulan/kalender)
    /// </summary>
    public class DateSettingsUI : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────
        [Header("Button References")]
        [SerializeField] private Button   _toggleButton;
        [SerializeField] private TMP_Text _toggleLabel;       // "☽ Hijriah" / "📅 Masehi"

        [Header("Optional — Mode Indicator Colors")]
        [SerializeField] private Image _buttonBackground;
        [SerializeField] private Color _colorHijriah = new Color(0.18f, 0.45f, 0.75f, 1f); // biru islami
        [SerializeField] private Color _colorMasehi  = new Color(0.25f, 0.25f, 0.32f, 1f); // abu-abu

        [Header("Optional — Dual Date Display")]
        [Tooltip("Jika di-assign, label ini menampilkan tanggal mode LAIN sebagai subtitle kecil.\nContoh: mode Hijriah aktif → label ini tampilkan tanggal Masehi.")]
        [SerializeField] private TMP_Text _subtitleDateLabel;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(OnToggleClicked);
        }

        private void Start()
        {
            // Reflect state awal
            RefreshButtonVisual();

            // Subscribe ke perubahan
            DateSettings.Instance.OnDateChanged += OnDateChanged;
        }

        private void OnDestroy()
        {
            DateSettings.Instance.OnDateChanged -= OnDateChanged;

            if (_toggleButton != null)
                _toggleButton.onClick.RemoveListener(OnToggleClicked);
        }

        // ─────────────────────────────────────────────
        // Callbacks
        // ─────────────────────────────────────────────
        private void OnToggleClicked()
        {
            DateSettings.Instance.Toggle();
        }

        private void OnDateChanged(string formattedDate, CalendarMode mode)
        {
            RefreshButtonVisual();
        }

        // ─────────────────────────────────────────────
        // Visual Update
        // ─────────────────────────────────────────────
        private void RefreshButtonVisual()
        {
            CalendarMode mode = DateSettings.Instance.Mode;

            // Label tombol menunjukkan mode AKTIF saat ini
            if (_toggleLabel)
                _toggleLabel.text = mode == CalendarMode.Hijriah ? "☽ Hijriah" : "📅 Masehi";

            // Warna background tombol
            if (_buttonBackground)
                _buttonBackground.color = mode == CalendarMode.Hijriah ? _colorHijriah : _colorMasehi;

            // Subtitle — tampilkan kalender lainnya sebagai referensi
            if (_subtitleDateLabel)
            {
                var (hijriah, masehi) = DateSettings.Instance.GetBothFormats();
                // Tampilkan yang BUKAN mode aktif
                _subtitleDateLabel.text = mode == CalendarMode.Hijriah ? masehi : hijriah;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_toggleButton) _toggleButton = GetComponent<Button>();
            if (!_toggleLabel)  _toggleLabel  = GetComponentInChildren<TMP_Text>();
        }
#endif
    }
}
