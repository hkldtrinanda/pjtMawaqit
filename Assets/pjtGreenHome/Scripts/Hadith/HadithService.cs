using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using PrayerSystem.Models;

namespace PrayerSystem
{
    /// <summary>
    /// HadithService — Singleton.
    /// Browse hadits per kitab dengan navigasi halaman (prev/next).
    ///
    /// API: https://api.hadith.gading.dev
    /// Endpoint: GET /books/{bookId}/{number}
    /// </summary>
    public class HadithService : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        public static HadithService Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────
        public event Action<HadithData>   OnHadithLoaded;
        public event Action<HadithBook>   OnBookChanged;     // saat pindah kitab
        public event Action<string>       OnHadithError;
        public event Action               OnLoadingStarted;

        // ─────────────────────────────────────────────
        // Katalog Kitab (5 imam sesuai permintaan)
        // ─────────────────────────────────────────────
        public static readonly HadithBook[] BOOKS =
        {
            new HadithBook("muslim",    "Muslim",       "Shahih Muslim",       5362),
            new HadithBook("abu-dawud", "Abu Dawud",    "Sunan Abu Dawud",     5274),
            new HadithBook("tirmidzi",  "At-Tirmidzi",  "Sunan At-Tirmidzi",   3956),
            new HadithBook("nasai",     "An-Nasa'i",    "Sunan An-Nasa'i",     5758),
            new HadithBook("bukhari",   "Bukhari",      "Shahih Bukhari",      7008),
        };

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        private const string API_BASE       = "https://api.hadith.gading.dev/books";
        private const string PREFS_BOOK_KEY = "hadith_last_book";    // bookId terakhir dibuka
        private const string PREFS_NUM_KEY  = "hadith_last_number";  // nomor hadits terakhir

        // ─────────────────────────────────────────────
        // Public State
        // ─────────────────────────────────────────────
        public HadithData CurrentHadith  { get; private set; }
        public HadithBook CurrentBook    { get; private set; }
        public int        CurrentNumber  { get; private set; } = 1;
        public bool       IsLoading      { get; private set; }

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
            // Buka kitab terakhir yang dibaca (atau default: Muslim)
            string lastBookId = PlayerPrefs.GetString(PREFS_BOOK_KEY, BOOKS[0].id);
            int    lastNumber = PlayerPrefs.GetInt(PREFS_NUM_KEY, 1);

            HadithBook book = GetBookById(lastBookId) ?? BOOKS[0];
            OpenBook(book, lastNumber);
        }

        // ─────────────────────────────────────────────
        // Public API — dipanggil UI
        // ─────────────────────────────────────────────

        /// <summary>Buka kitab tertentu, mulai dari nomor hadits yang ditentukan.</summary>
        public void OpenBook(HadithBook book, int startNumber = 1)
        {
            if (IsLoading) return;

            CurrentBook   = book;
            CurrentNumber = Mathf.Clamp(startNumber, 1, book.totalCount);

            // Simpan preferensi
            PlayerPrefs.SetString(PREFS_BOOK_KEY, book.id);
            PlayerPrefs.SetInt(PREFS_NUM_KEY, CurrentNumber);
            PlayerPrefs.Save();

            OnBookChanged?.Invoke(CurrentBook);
            StartCoroutine(FetchHadith(book.id, CurrentNumber));
        }

        /// <summary>Hadits berikutnya dalam kitab yang sama.</summary>
        public void Next()
        {
            if (IsLoading || CurrentBook == null) return;
            int next = CurrentNumber < CurrentBook.totalCount ? CurrentNumber + 1 : 1; // wrap ke awal
            NavigateTo(next);
        }

        /// <summary>Hadits sebelumnya dalam kitab yang sama.</summary>
        public void Previous()
        {
            if (IsLoading || CurrentBook == null) return;
            int prev = CurrentNumber > 1 ? CurrentNumber - 1 : CurrentBook.totalCount; // wrap ke akhir
            NavigateTo(prev);
        }

        /// <summary>Lompat ke nomor hadits tertentu.</summary>
        public void NavigateTo(int number)
        {
            if (IsLoading || CurrentBook == null) return;
            CurrentNumber = Mathf.Clamp(number, 1, CurrentBook.totalCount);
            PlayerPrefs.SetInt(PREFS_NUM_KEY, CurrentNumber);
            PlayerPrefs.Save();
            StartCoroutine(FetchHadith(CurrentBook.id, CurrentNumber));
        }

        // ─────────────────────────────────────────────
        // Fetch
        // ─────────────────────────────────────────────
        private IEnumerator FetchHadith(string bookId, int number)
        {
            IsLoading = true;
            OnLoadingStarted?.Invoke();

            string url = $"{API_BASE}/{bookId}/{number}";
            Debug.Log($"[HadithService] Fetching: {url}");

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[HadithService] Error: {req.error}");
                    OnHadithError?.Invoke($"Gagal memuat hadits. Periksa koneksi internet.");
                    IsLoading = false;
                    yield break;
                }

                ParseAndApply(req.downloadHandler.text);
            }

            IsLoading = false;
        }

        // ─────────────────────────────────────────────
        // Parse
        // ─────────────────────────────────────────────
        private void ParseAndApply(string json)
        {
            try
            {
                HadithApiResponse resp = JsonUtility.FromJson<HadithApiResponse>(json);

                if (resp?.data?.contents == null)
                {
                    OnHadithError?.Invoke("Format data hadits tidak valid.");
                    return;
                }

                CurrentHadith = new HadithData
                {
                    arabicText     = resp.data.contents.arab,
                    indonesianText = resp.data.contents.id,
                    narrator       = CurrentBook?.displayName ?? resp.data.name,
                    bookFullName   = CurrentBook?.fullName    ?? resp.data.name,
                    number         = resp.data.contents.number,
                    totalInBook    = CurrentBook?.totalCount  ?? 0,
                };

                OnHadithLoaded?.Invoke(CurrentHadith);
                Debug.Log($"[HadithService] Loaded: {CurrentHadith.narrator} No.{CurrentHadith.number}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HadithService] Parse error: {ex.Message}");
                OnHadithError?.Invoke("Gagal memproses data hadits.");
            }
        }

        // ─────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────
        public static HadithBook GetBookById(string id)
        {
            foreach (var b in BOOKS)
                if (b.id == id) return b;
            return null;
        }
    }

    // ─────────────────────────────────────────────────────
    // HadithBook — data class untuk 1 kitab
    // ─────────────────────────────────────────────────────
    [Serializable]
    public class HadithBook
    {
        public string id;           // "muslim", "bukhari", dll — untuk API
        public string displayName;  // "Imam Muslim" — label button
        public string fullName;     // "Shahih Muslim" — subtitle panel
        public int    totalCount;   // jumlah hadits (untuk progress & counter)

        public HadithBook(string id, string displayName, string fullName, int totalCount)
        {
            this.id          = id;
            this.displayName = displayName;
            this.fullName    = fullName;
            this.totalCount  = totalCount;
        }
    }
}