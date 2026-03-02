using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PrayerSystem.UI
{
    /// <summary>
    /// HadithBookButton — Script untuk 1 tombol kitab (dari 5 button yang ada).
    ///
    /// Prefab hierarchy:
    ///   BtnBook (Button + Image + HadithBookButton)
    ///   ├── TxtImamName    ← "Imam Muslim"
    ///   ├── TxtBookName    ← "Shahih Muslim"
    ///   └── TxtCount       ← "5.362 Hadits"
    /// </summary>
    public class HadithBookButton : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────
        [Header("Labels")]
        [SerializeField] private TMP_Text _imamNameLabel;   // "Imam Muslim"
        [SerializeField] private TMP_Text _bookNameLabel;   // "Shahih Muslim"
        [SerializeField] private TMP_Text _countLabel;      // "5.362 Hadits"

        [Header("Visual State")]
        [SerializeField] private Image  _background;
        [SerializeField] private Color  _colorActive   = new Color(0.18f, 0.55f, 0.34f, 1f); // hijau
        [SerializeField] private Color  _colorInactive = new Color(0.15f, 0.15f, 0.2f,  1f); // gelap

        [Header("Button")]
        [SerializeField] private Button _button;

        // ─────────────────────────────────────────────
        // Runtime
        // ─────────────────────────────────────────────
        private HadithBook _book;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────
        public void Bind(HadithBook book)
        {
            _book = book;

            if (_imamNameLabel) _imamNameLabel.text = book.displayName;
            if (_bookNameLabel) _bookNameLabel.text = book.fullName;
            if (_countLabel)    _countLabel.text    = $"{book.totalCount:N0} Hadits";
        }

        public void SetActive(bool isActive)
        {
            if (_background) _background.color = isActive ? _colorActive : _colorInactive;
        }

        // ─────────────────────────────────────────────
        // Click
        // ─────────────────────────────────────────────
        private void OnClicked()
        {
            if (_book == null) return;
            HadithService.Instance?.OpenBook(_book, 1);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_button) _button = GetComponent<Button>();
        }
#endif
    }
}
