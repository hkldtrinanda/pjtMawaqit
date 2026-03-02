using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrayerSystem.Models;

namespace PrayerSystem.UI
{
    /// <summary>
    /// MosqueListUI — Mengelola vertical grid list masjid/mushola.
    ///
    /// Hierarchy yang disarankan:
    ///   PanelMosque
    ///   ├── Header
    ///   │   ├── TxtTitle          ← "Masjid Terdekat"
    ///   │   ├── TxtSubtitle       ← "3 km · 12 ditemukan"
    ///   │   └── BtnRefresh        ← Button refresh manual
    ///   ├── ScrollView
    ///   │   └── Viewport
    ///   │       └── GridContainer ← assign ke _gridContainer
    ///   │           └── (MosqueItem prefab × N)
    ///   ├── OverlayLoading        ← Panel loading
    ///   ├── OverlayEmpty          ← Panel "tidak ada hasil"
    ///   └── TxtError              ← TMP_Text error
    /// </summary>
    public class MosqueListUI : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────
        [Header("Grid")]
        [SerializeField] private Transform  _gridContainer;
        [SerializeField] private GameObject _mosqueItemPrefab;

        [Header("Header Info")]
        [SerializeField] private TMP_Text _titleLabel;      // "Masjid Terdekat"
        [SerializeField] private TMP_Text _subtitleLabel;   // "Radius 3 km · 12 ditemukan"
        [SerializeField] private Button   _btnRefresh;

        [Header("State Overlays")]
        [SerializeField] private GameObject _loadingOverlay;
        [SerializeField] private GameObject _emptyOverlay;   // "Tidak ada masjid ditemukan"
        [SerializeField] private TMP_Text   _errorLabel;

        // ─────────────────────────────────────────────
        // Runtime
        // ─────────────────────────────────────────────
        private readonly List<MosqueItemUI> _pool = new List<MosqueItemUI>();

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            SetLoading(true);
            SetEmpty(false);
            HideError();

            if (_titleLabel) _titleLabel.text = "Masjid Terdekat";
            if (_btnRefresh) _btnRefresh.onClick.AddListener(OnRefreshClicked);
        }

        private void Start()
        {
            if (MosqueService.Instance == null)
            {
                Debug.LogError("[MosqueListUI] MosqueService.Instance null! Tambahkan ke scene.");
                return;
            }

            MosqueService.Instance.OnFetchStarted  += OnFetchStarted;
            MosqueService.Instance.OnMosquesLoaded += OnMosquesLoaded;
            MosqueService.Instance.OnMosqueError   += OnMosqueError;

            // Pull data jika sudah tersedia (dari cache)
            if (MosqueService.Instance.NearbyMosques?.Count > 0)
                OnMosquesLoaded(MosqueService.Instance.NearbyMosques);
        }

        private void OnDestroy()
        {
            if (MosqueService.Instance != null)
            {
                MosqueService.Instance.OnFetchStarted  -= OnFetchStarted;
                MosqueService.Instance.OnMosquesLoaded -= OnMosquesLoaded;
                MosqueService.Instance.OnMosqueError   -= OnMosqueError;
            }

            if (_btnRefresh) _btnRefresh.onClick.RemoveListener(OnRefreshClicked);
        }

        // ─────────────────────────────────────────────
        // Event Callbacks
        // ─────────────────────────────────────────────
        private void OnFetchStarted()
        {
            SetLoading(true);
            SetEmpty(false);
            HideError();
        }

        private void OnMosquesLoaded(List<MosqueInfo> mosques)
        {
            SetLoading(false);
            HideError();

            if (mosques == null || mosques.Count == 0)
            {
                SetEmpty(true);
                return;
            }

            SetEmpty(false);
            BuildGrid(mosques);

            // Update subtitle
            if (_subtitleLabel)
                _subtitleLabel.text = $"Radius 3 km  ·  {mosques.Count} ditemukan";
        }

        private void OnMosqueError(string message)
        {
            SetLoading(false);

            // Jika ada data cache, jangan tampilkan error — hanya log
            if (MosqueService.Instance?.NearbyMosques?.Count > 0)
            {
                Debug.LogWarning($"[MosqueListUI] {message} (menampilkan cache)");
                return;
            }

            if (_errorLabel)
            {
                _errorLabel.gameObject.SetActive(true);
                _errorLabel.text = message;
            }
        }

        // ─────────────────────────────────────────────
        // Grid Builder
        // ─────────────────────────────────────────────
        private void BuildGrid(List<MosqueInfo> mosques)
        {
            // Extend pool jika kurang
            while (_pool.Count < mosques.Count)
            {
                GameObject go = Instantiate(_mosqueItemPrefab, _gridContainer);
                MosqueItemUI item = go.GetComponent<MosqueItemUI>();
                if (item == null)
                {
                    Debug.LogError("[MosqueListUI] Prefab tidak punya MosqueItemUI!");
                    Destroy(go);
                    return;
                }
                _pool.Add(item);
            }

            // Bind data
            for (int i = 0; i < mosques.Count; i++)
            {
                _pool[i].gameObject.SetActive(true);
                _pool[i].Bind(mosques[i]);
            }

            // Sembunyikan sisa pool
            for (int i = mosques.Count; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        // Button
        // ─────────────────────────────────────────────
        private void OnRefreshClicked()
        {
            if (_btnRefresh) _btnRefresh.interactable = false;
            MosqueService.Instance?.RequestRefresh();

            // Re-enable setelah 5 detik
            Invoke(nameof(ReenableRefresh), 5f);
        }

        private void ReenableRefresh()
        {
            if (_btnRefresh) _btnRefresh.interactable = true;
        }

        // ─────────────────────────────────────────────
        // State Helpers
        // ─────────────────────────────────────────────
        private void SetLoading(bool show)
        {
            if (_loadingOverlay) _loadingOverlay.SetActive(show);
        }

        private void SetEmpty(bool show)
        {
            if (_emptyOverlay) _emptyOverlay.SetActive(show);
        }

        private void HideError()
        {
            if (_errorLabel) _errorLabel.gameObject.SetActive(false);
        }
    }
}
