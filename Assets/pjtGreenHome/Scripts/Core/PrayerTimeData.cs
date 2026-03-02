using System;

namespace PrayerSystem.Models
{
    [Serializable]
    public class AladhanResponse
    {
        public int code;
        public string status;
        public AladhanData data;
    }

    [Serializable]
    public class AladhanData
    {
        public AladhanTimings timings;
    }

    [Serializable]
    public class AladhanTimings
    {
        public string Fajr;
        public string Dhuhr;
        public string Asr;
        public string Maghrib;
        public string Isha;
        public string Sunrise;
        public string Sunset;
        public string Midnight;
    }

    [Serializable]
    public class PrayerInfo
    {
        public string name;
        public string displayName;
        public DateTime time;
        public bool isNext;

        public PrayerInfo(string name, string displayName, DateTime time)
        {
            this.name = name;
            this.displayName = displayName;
            this.time = time;
            this.isNext = false;
        }
    }

    [Serializable]
    public class CachedPrayerData
    {
        public string fajr;
        public string dhuhr;
        public string asr;
        public string maghrib;
        public string isha;
        public string cachedDate;       // "yyyy-MM-dd"
        public float cachedLat;
        public float cachedLon;

        public bool IsValidForToday()
        {
            return cachedDate == DateTime.Now.ToString("yyyy-MM-dd");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Hadith Models
// ─────────────────────────────────────────────────────────────────────────────

namespace PrayerSystem.Models
{
    // Runtime data yang dipakai UI
    [Serializable]
    public class HadithData
    {
        public string arabicText;
        public string indonesianText;
        public string narrator;        // "Imam Muslim"
        public string bookFullName;    // "Shahih Muslim"
        public int    number;          // nomor hadits
        public int    totalInBook;     // total hadits dalam kitab (untuk counter)
    }

    // Cache ke PlayerPrefs
    [Serializable]
    public class CachedHadithData
    {
        public string arabicText;
        public string indonesianText;
        public string narrator;
        public int    number;
        public string cachedDate;
    }

    // ── API Response Models ──────────────────────────────────────────────────
    // Struktur: { status, message, data: { name, id, available, contents: { number, arab, id } } }

    [Serializable]
    public class HadithApiResponse
    {
        public bool   status;
        public string message;
        public HadithApiData data;
    }

    [Serializable]
    public class HadithApiData
    {
        public string name;       // "Shahih Bukhari"
        public string id;         // "bukhari"
        public int    available;  // total hadits tersedia
        public HadithContents contents;
    }

    [Serializable]
    public class HadithContents
    {
        public int    number;
        public string arab;       // teks Arabic
        public string id;         // terjemahan Indonesia
    }
}