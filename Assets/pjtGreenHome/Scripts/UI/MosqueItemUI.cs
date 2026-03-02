using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrayerSystem.Models;

namespace PrayerSystem.UI
{
    /// <summary>
    /// MosqueItemUI — Script untuk 1 baris masjid di grid vertikal.
    ///
    /// Prefab hierarchy yang disarankan:
    ///   MosqueItem (Image + Button + MosqueItemUI)
    ///   ├── ImgTypeIcon      ← Image (icon masjid/mushola, opsional)
    ///   ├── TxtName          ← TMP_Text — nama masjid
    ///   ├── TxtType          ← TMP_Text — "Masjid" / "Mushola"
    ///   ├── TxtDistance      ← TMP_Text — "250 m" / "1.2 km"
    ///   └── BtnNavigate      ← Button (klik → buka Maps)
    /// </summary>
    public class MosqueItemUI : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────
        [Header("Labels")]
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _typeLabel;
        [SerializeField] private TMP_Text _distanceLabel;

        [Header("Type Badge Colors")]
        [SerializeField] private Image  _typeBadge;
        [SerializeField] private Color  _colorMasjid  = new Color(0.18f, 0.55f, 0.34f, 1f); // hijau
        [SerializeField] private Color  _colorMushola = new Color(0.18f, 0.40f, 0.72f, 1f); // biru
        [SerializeField] private Color  _colorSurau   = new Color(0.65f, 0.40f, 0.10f, 1f); // coklat

        [Header("Interaction")]
        [SerializeField] private Button _itemButton; // seluruh baris bisa diklik

        // ─────────────────────────────────────────────
        // Runtime
        // ─────────────────────────────────────────────
        private MosqueInfo _data;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (_itemButton == null)
                _itemButton = GetComponent<Button>();

            if (_itemButton != null)
                _itemButton.onClick.AddListener(OnItemClicked);
        }

        private void OnDestroy()
        {
            if (_itemButton != null)
                _itemButton.onClick.RemoveListener(OnItemClicked);
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────
        public void Bind(MosqueInfo mosque)
        {
            _data = mosque;

            if (_nameLabel)
                _nameLabel.text = mosque.name;

            if (_typeLabel)
                _typeLabel.text = mosque.type;

            if (_distanceLabel)
                _distanceLabel.text = mosque.DistanceFormatted;

            // Warna badge berdasarkan tipe
            if (_typeBadge)
            {
                _typeBadge.color = mosque.type switch
                {
                    "Mushola" => _colorMushola,
                    "Surau"   => _colorSurau,
                    _         => _colorMasjid,
                };
            }
        }

        // ─────────────────────────────────────────────
        // Open Maps
        // ─────────────────────────────────────────────
        private void OnItemClicked()
        {
            if (_data == null) return;
            OpenMaps(_data);
        }

        public static void OpenMaps(MosqueInfo mosque)
        {
            // Strategi: coba Google Maps dulu, fallback ke Apple Maps di iOS
#if UNITY_IOS
            // Apple Maps universal link + Google Maps deep link
            string appleUrl = $"maps://?daddr={mosque.lat},{mosque.lon}&dirflg=w";

            // Cek apakah Google Maps terinstall di iOS
            string googleScheme = $"comgooglemaps://?daddr={mosque.lat},{mosque.lon}&directionsmode=walking";

            // Unity di iOS: Application.OpenURL akan handle scheme otomatis
            Application.OpenURL(googleScheme);
            // Jika gagal (Google Maps tidak terinstall), fallback otomatis ke Apple Maps
            // via WebURL di bawah
#endif

            // Android & default: Google Maps web URL (universal, selalu bisa dibuka)
            // Jika Google Maps app terinstall, Android akan launch app-nya langsung
            string url = mosque.MapsUrl;
            Debug.Log($"[MosqueItemUI] Opening Maps: {url}");
            Application.OpenURL(url);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_nameLabel)     _nameLabel     = transform.Find("TxtName")?.GetComponent<TMP_Text>();
            if (!_typeLabel)     _typeLabel     = transform.Find("TxtType")?.GetComponent<TMP_Text>();
            if (!_distanceLabel) _distanceLabel = transform.Find("TxtDistance")?.GetComponent<TMP_Text>();
            if (!_itemButton)    _itemButton    = GetComponent<Button>();
        }
#endif
    }
}
