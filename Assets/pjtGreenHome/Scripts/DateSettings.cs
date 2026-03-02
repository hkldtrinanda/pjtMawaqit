using System;
using System.Globalization;
using UnityEngine;
using PrayerSystem.Models;

namespace PrayerSystem
{
    /// <summary>
    /// DateSettings — Singleton ringan (tidak perlu MonoBehaviour).
    /// Menyimpan preferensi kalender user (Hijriah / Masehi)
    /// dan menghasilkan string tanggal yang sudah diformat.
    ///
    /// Konversi Hijriah menggunakan UmAlQuraCalendar bawaan .NET —
    /// tidak perlu package tambahan, akurat untuk wilayah global.
    /// </summary>
    public class DateSettings
    {
        // ─────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────
        private static DateSettings _instance;
        public  static DateSettings Instance => _instance ??= new DateSettings();

        // ─────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────
        /// <summary>Dipanggil saat user toggle kalender. Parameter: string tanggal ter-format.</summary>
        public event Action<string, CalendarMode> OnDateChanged;

        // ─────────────────────────────────────────────
        // Config
        // ─────────────────────────────────────────────
        private const string PREFS_KEY = "calendar_mode"; // "Hijriah" | "Masehi"

        // Nama bulan Hijriah dalam Bahasa Indonesia
        private static readonly string[] HIJRI_MONTHS =
        {
            "Muharram", "Safar", "Rabi'ul Awwal", "Rabi'ul Akhir",
            "Jumadil Awwal", "Jumadil Akhir", "Rajab", "Sya'ban",
            "Ramadan", "Syawal", "Dzulqa'dah", "Dzulhijjah"
        };

        // Nama hari dalam Bahasa Indonesia
        private static readonly string[] DAY_NAMES =
        {
            "Ahad", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"
        };

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────
        public CalendarMode Mode { get; private set; }

        // ─────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────
        private DateSettings()
        {
            // Load preferensi tersimpan — default Hijriah
            string saved = PlayerPrefs.GetString(PREFS_KEY, CalendarMode.Hijriah.ToString());
            Mode = Enum.TryParse(saved, out CalendarMode parsed) ? parsed : CalendarMode.Hijriah;
            Debug.Log($"[DateSettings] Mode loaded: {Mode}");
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>Toggle antara Hijriah ↔ Masehi dan simpan ke PlayerPrefs.</summary>
        public void Toggle()
        {
            Mode = Mode == CalendarMode.Hijriah ? CalendarMode.Masehi : CalendarMode.Hijriah;
            PlayerPrefs.SetString(PREFS_KEY, Mode.ToString());
            PlayerPrefs.Save();

            Debug.Log($"[DateSettings] Mode changed to: {Mode}");
            OnDateChanged?.Invoke(GetFormattedDate(), Mode);
        }

        /// <summary>Set mode secara eksplisit.</summary>
        public void SetMode(CalendarMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            PlayerPrefs.SetString(PREFS_KEY, Mode.ToString());
            PlayerPrefs.Save();
            OnDateChanged?.Invoke(GetFormattedDate(), Mode);
        }

        /// <summary>
        /// Kembalikan tanggal hari ini dalam format yang sesuai mode aktif.
        /// Hijriah  → "Ahad, 25 Rajab 1446 H"
        /// Masehi   → "Ahad, 23 Februari 2026"
        /// </summary>
        public string GetFormattedDate(DateTime? dateOverride = null)
        {
            DateTime date = dateOverride ?? DateTime.Now;
            return Mode == CalendarMode.Hijriah
                ? FormatHijriah(date)
                : FormatMasehi(date);
        }

        // ─────────────────────────────────────────────
        // Formatting
        // ─────────────────────────────────────────────

        private string FormatHijriah(DateTime date)
        {
            try
            {
                var hijri     = new UmAlQuraCalendar();
                int day       = hijri.GetDayOfMonth(date);
                int month     = hijri.GetMonth(date);
                int year      = hijri.GetYear(date);
                string dayName = DAY_NAMES[(int)date.DayOfWeek];
                string monthName = HIJRI_MONTHS[month - 1];

                return $"{dayName}, {day} {monthName} {year} H";
            }
            catch (Exception ex)
            {
                // UmAlQuraCalendar punya range terbatas (~1900-2077)
                // Fallback ke Masehi jika error
                Debug.LogWarning($"[DateSettings] Hijri conversion error: {ex.Message}. Fallback ke Masehi.");
                return FormatMasehi(date);
            }
        }

        private string FormatMasehi(DateTime date)
        {
            // Format: "Ahad, 23 Februari 2026"
            string dayName = DAY_NAMES[(int)date.DayOfWeek];
            string monthName = date.ToString("MMMM", new CultureInfo("id-ID"));
            return $"{dayName}, {date.Day} {monthName} {date.Year}";
        }

        // ─────────────────────────────────────────────
        // Utility — Keduanya sekaligus (untuk dual display)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Kembalikan kedua format sekaligus.
        /// Berguna jika ingin tampilkan: "25 Rajab 1446 H / 23 Feb 2026"
        /// </summary>
        public (string hijriah, string masehi) GetBothFormats(DateTime? dateOverride = null)
        {
            DateTime date = dateOverride ?? DateTime.Now;
            return (FormatHijriah(date), FormatMasehi(date));
        }
    }

    // ─────────────────────────────────────────────────────
    // Enum
    // ─────────────────────────────────────────────────────
    public enum CalendarMode { Hijriah, Masehi }
}
