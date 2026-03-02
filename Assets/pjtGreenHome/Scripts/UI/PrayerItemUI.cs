using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrayerSystem.Models;

namespace PrayerSystem.UI
{
    /// <summary>
    /// PrayerItemUI — Script untuk 1 baris item di grid vertikal.
    /// Attach pada prefab yang memiliki:
    ///   - TMP_Text (nameLabel)
    ///   - TMP_Text (timeLabel)
    ///   - Image (background / highlightImage)
    /// </summary>
    public class PrayerItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _timeLabel;
        [SerializeField] private Image    _backgroundImage;

        [Header("Visual States")]
        [SerializeField] private Color _normalBgColor    = new Color(0.15f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color _highlightBgColor = new Color(0.18f, 0.55f, 0.34f, 1f);
        [SerializeField] private Color _normalTextColor    = Color.white;
        [SerializeField] private Color _highlightTextColor = Color.white;

        [Header("Optional Outline")]
        [SerializeField] private Outline _outline;        // boleh null
        [SerializeField] private Color   _outlineColor    = new Color(0.4f, 0.9f, 0.6f, 1f);

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────
        public void Bind(PrayerInfo prayer)
        {
            _nameLabel.text = prayer.displayName;
            _timeLabel.text = prayer.time.ToString("HH:mm");

            SetHighlight(prayer.isNext);
        }

        public void SetHighlight(bool isNext)
        {
            if (_backgroundImage)
                _backgroundImage.color = isNext ? _highlightBgColor : _normalBgColor;

            if (_nameLabel)
                _nameLabel.color = isNext ? _highlightTextColor : _normalTextColor;
            if (_timeLabel)
                _timeLabel.color = isNext ? _highlightTextColor : _normalTextColor;

            if (_outline)
            {
                _outline.enabled     = isNext;
                _outline.effectColor = _outlineColor;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find komponen jika belum di-assign di Inspector
            if (!_nameLabel)  _nameLabel  = transform.Find("NameLabel")?.GetComponent<TMP_Text>();
            if (!_timeLabel)  _timeLabel  = transform.Find("TimeLabel")?.GetComponent<TMP_Text>();
            if (!_backgroundImage) _backgroundImage = GetComponent<Image>();
            if (!_outline) _outline = GetComponent<Outline>();
        }
#endif
    }
}
