using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrayerSystem.Models;

namespace PrayerSystem.UI
{
    /// <summary>
    /// HadithUIController — Panel utama hadits.
    ///
    /// Mengelola:
    ///   • 5 button kitab (HadithBookButton) di tab bar atas
    ///   • Panel konten: Arabic, Terjemahan, Perawi
    ///   • Counter: "No. 123 / 5.362"
    ///   • Navigasi Prev / Next
    ///   • Input lompat ke nomor tertentu
    ///
    /// Hierarchy yang disarankan:
    ///   PanelHadith (HadithUIController)
    ///   ├── TabBar
    ///   │   ├── BtnMuslim       ← HadithBookButton
    ///   │   ├── BtnAbuDawud     ← HadithBookButton
    ///   │   ├── BtnTirmidzi     ← HadithBookButton
    ///   │   ├── BtnNasai        ← HadithBookButton
    ///   │   └── BtnBukhari      ← HadithBookButton
    ///   ├── PanelContent
    ///   │   ├── TxtBookTitle    ← "Shahih Muslim"
    ///   │   ├── TxtCounter      ← "Hadits ke-123 dari 5.362"
    ///   │   ├── TxtArabic       ← teks Arab RTL
    ///   │   ├── TxtIndonesian   ← terjemahan
    ///   │   └── TxtNarrator     ← "Riwayat dari: Imam Muslim"
    ///   ├── NavBar
    ///   │   ├── BtnPrev         ← ◀ sebelumnya
    ///   │   ├── InputGotoNumber ← TMP_InputField (lompat ke nomor)
    ///   │   └── BtnNext         ← ▶ berikutnya
    ///   └── OverlayLoading
    /// </summary>
    public class HadithUIController : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector — Tab Bar (5 kitab)
        // ─────────────────────────────────────────────
        [Header("Tab Bar — 5 Book Buttons")]
        [SerializeField] private HadithBookButton[] _bookButtons; // assign 5 button di Inspector (urutan sama dengan HadithService.BOOKS)

        // ─────────────────────────────────────────────
        // Inspector — Konten
        // ─────────────────────────────────────────────
        [Header("Content")]
        [SerializeField] private TMP_Text _bookTitleLabel;    // "Shahih Muslim"
        [SerializeField] private TMP_Text _counterLabel;      // "Hadits ke-123 dari 5.362"
        [SerializeField] private TMP_Text _arabicLabel;       // teks Arab
        [SerializeField] private TMP_Text _indonesianLabel;   // terjemahan
        [SerializeField] private TMP_Text _narratorLabel;     // "Riwayat dari: Imam Muslim"

        // ─────────────────────────────────────────────
        // Inspector — Navigasi
        // ─────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button          _btnPrev;
        [SerializeField] private Button          _btnNext;
        [SerializeField] private TMP_InputField  _inputGotoNumber;  // opsional: ketik nomor hadits

        // ─────────────────────────────────────────────
        // Inspector — State
        // ─────────────────────────────────────────────
        [Header("State")]
        [SerializeField] private GameObject _loadingOverlay;
        [SerializeField] private TMP_Text   _errorLabel;

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void Awake()
        {
            SetLoading(true);
            HideError();
            SetupButtons();
        }

        private void Start()
        {
            if (HadithService.Instance == null)
            {
                Debug.LogError("[HadithUIController] HadithService.Instance null!");
                return;
            }

            HadithService.Instance.OnLoadingStarted += OnLoadingStarted;
            HadithService.Instance.OnHadithLoaded   += OnHadithLoaded;
            HadithService.Instance.OnBookChanged     += OnBookChanged;
            HadithService.Instance.OnHadithError     += OnHadithError;

            // Bind 5 button ke 5 kitab
            for (int i = 0; i < _bookButtons.Length && i < HadithService.BOOKS.Length; i++)
                _bookButtons[i].Bind(HadithService.BOOKS[i]);

            // Reflect state awal jika sudah ada
            if (HadithService.Instance.CurrentBook != null)
                OnBookChanged(HadithService.Instance.CurrentBook);

            if (HadithService.Instance.CurrentHadith != null)
                OnHadithLoaded(HadithService.Instance.CurrentHadith);
        }

        private void OnDestroy()
        {
            if (HadithService.Instance == null) return;
            HadithService.Instance.OnLoadingStarted -= OnLoadingStarted;
            HadithService.Instance.OnHadithLoaded   -= OnHadithLoaded;
            HadithService.Instance.OnBookChanged     -= OnBookChanged;
            HadithService.Instance.OnHadithError     -= OnHadithError;
        }

        // ─────────────────────────────────────────────
        // Setup Buttons
        // ─────────────────────────────────────────────
        private void SetupButtons()
        {
            if (_btnPrev) _btnPrev.onClick.AddListener(() => HadithService.Instance?.Previous());
            if (_btnNext) _btnNext.onClick.AddListener(() => HadithService.Instance?.Next());

            if (_inputGotoNumber)
                _inputGotoNumber.onEndEdit.AddListener(OnGotoNumberSubmit);
        }

        private void OnGotoNumberSubmit(string input)
        {
            if (int.TryParse(input, out int num))
            {
                HadithService.Instance?.NavigateTo(num);
                _inputGotoNumber.text = "";
            }
        }

        // ─────────────────────────────────────────────
        // Event Callbacks
        // ─────────────────────────────────────────────
        private void OnLoadingStarted()
        {
            SetLoading(true);
            HideError();
            SetNavEnabled(false);
        }

        private void OnBookChanged(HadithBook book)
        {
            // Update judul panel
            if (_bookTitleLabel) _bookTitleLabel.text = book.fullName;

            // Highlight button aktif
            for (int i = 0; i < _bookButtons.Length && i < HadithService.BOOKS.Length; i++)
                _bookButtons[i].SetActive(HadithService.BOOKS[i].id == book.id);
        }

        private void OnHadithLoaded(HadithData data)
        {
            SetLoading(false);
            HideError();
            SetNavEnabled(true);

            // Counter — "Hadits ke-123 dari 5.362"
            if (_counterLabel)
                _counterLabel.text = $"Hadits ke-{data.number:N0} dari {data.totalInBook:N0}";

            // Arabic (RTL) — TopRight agar mulai dari kanan atas, bukan tengah
            if (_arabicLabel)
            {
                _arabicLabel.text      = data.arabicText;
                _arabicLabel.alignment = TextAlignmentOptions.TopRight;
            }

            // Terjemahan — TopLeft agar mulai dari kiri atas, bukan tengah
            if (_indonesianLabel)
            {
                _indonesianLabel.text      = data.indonesianText;
                _indonesianLabel.alignment = TextAlignmentOptions.TopLeft;
            }

            // Riwayat dari
            if (_narratorLabel)
                _narratorLabel.text = $"Riwayat dari: {data.narrator}";

            // Update input field placeholder
            if (_inputGotoNumber)
                _inputGotoNumber.placeholder.GetComponent<TMP_Text>().text =
                    $"No. {data.number}";
        }

        private void OnHadithError(string message)
        {
            SetLoading(false);
            SetNavEnabled(true);

            if (_errorLabel)
            {
                _errorLabel.gameObject.SetActive(true);
                _errorLabel.text = message;
            }
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────
        private void SetLoading(bool show)
        {
            if (_loadingOverlay) _loadingOverlay.SetActive(show);
        }

        private void HideError()
        {
            if (_errorLabel) _errorLabel.gameObject.SetActive(false);
        }

        private void SetNavEnabled(bool enabled)
        {
            if (_btnPrev) _btnPrev.interactable = enabled;
            if (_btnNext) _btnNext.interactable = enabled;
        }
    }
}