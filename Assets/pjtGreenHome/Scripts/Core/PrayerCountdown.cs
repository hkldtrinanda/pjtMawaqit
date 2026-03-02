using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrayerSystem.Models;

namespace PrayerSystem
{
    /// <summary>
    /// PrayerCountdown — Update loop setiap detik.
    /// Menentukan sholat berikutnya, menghitung sisa waktu,
    /// dan memicu event ke UI.
    /// Attach di GameObject yang sama dengan PrayerTimeManager,
    /// atau GameObject terpisah.
    /// </summary>
    public class PrayerCountdown : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Events (UI subscribe ke sini)
        // ─────────────────────────────────────────────
        /// <summary>Dipanggil setiap detik. Parameter: (sisa waktu, nama sholat berikutnya)</summary>
        public event Action<TimeSpan, string> OnCountdownTick;

        /// <summary>Dipanggil saat sholat berikutnya berubah (UI perlu re-highlight grid)</summary>
        public event Action<string> OnNextPrayerChanged;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────
        private List<PrayerInfo> _prayers;
        private string _currentNextPrayerName = "";
        private Coroutine _tickRoutine;
        private string _lastCheckedDate = "";

        // ─────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────
        private void OnEnable()
        {
            PrayerTimeManager.Instance.OnPrayerTimesUpdated += OnPrayerTimesUpdated;
        }

        private void OnDisable()
        {
            if (PrayerTimeManager.Instance != null)
                PrayerTimeManager.Instance.OnPrayerTimesUpdated -= OnPrayerTimesUpdated;

            if (_tickRoutine != null)
                StopCoroutine(_tickRoutine);
        }

        // ─────────────────────────────────────────────
        // Callback dari Manager
        // ─────────────────────────────────────────────
        private void OnPrayerTimesUpdated(List<PrayerInfo> prayers)
        {
            _prayers = prayers;

            if (_tickRoutine != null) StopCoroutine(_tickRoutine);
            _tickRoutine = StartCoroutine(TickRoutine());
        }

        // ─────────────────────────────────────────────
        // Tick Loop — setiap 1 detik
        // ─────────────────────────────────────────────
        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(1f);

            while (true)
            {
                Tick();

                // Deteksi pergantian hari → minta refresh jadwal
                string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
                if (_lastCheckedDate != "" && _lastCheckedDate != todayStr)
                {
                    Debug.Log("[PrayerCountdown] Hari berganti — meminta refresh jadwal.");
                    PrayerTimeManager.Instance.RequestRefresh();
                }
                _lastCheckedDate = todayStr;

                yield return wait;
            }
        }

        private void Tick()
        {
            if (_prayers == null || _prayers.Count == 0) return;

            DateTime now = DateTime.Now;

            // Reset semua flag
            foreach (var p in _prayers) p.isNext = false;

            PrayerInfo nextPrayer = null;
            DateTime   nextTime   = DateTime.MinValue;

            // Cari sholat berikutnya di hari ini
            foreach (var p in _prayers)
            {
                if (p.time > now)
                {
                    if (nextPrayer == null || p.time < nextTime)
                    {
                        nextPrayer = p;
                        nextTime   = p.time;
                    }
                }
            }

            TimeSpan remaining;
            string   nextName;

            if (nextPrayer != null)
            {
                // Ada sholat berikutnya hari ini
                nextPrayer.isNext = true;
                remaining = nextTime - now;
                nextName  = nextPrayer.displayName;
            }
            else
            {
                // Semua sholat hari ini sudah lewat → hitung ke Subuh besok
                PrayerInfo fajr       = _prayers[0]; // index 0 = Fajr
                DateTime   fajrTomorrow = fajr.time.AddDays(1);
                remaining = fajrTomorrow - now;
                nextName  = $"{fajr.displayName} (Besok)";

                // Secara visual, highlight Subuh
                fajr.isNext = true;
            }

            // Notifikasi perubahan sholat berikutnya
            if (nextName != _currentNextPrayerName)
            {
                _currentNextPrayerName = nextName;
                OnNextPrayerChanged?.Invoke(nextName);
            }

            OnCountdownTick?.Invoke(remaining, nextName);
        }

        // ─────────────────────────────────────────────
        // Public utility
        // ─────────────────────────────────────────────
        /// <summary>Format TimeSpan ke HH:mm:ss</summary>
        public static string FormatCountdown(TimeSpan ts)
        {
            if (ts.TotalSeconds <= 0) return "00:00:00";
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }
}
