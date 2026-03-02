using System;
using System.Collections;
using UnityEngine;

namespace PrayerSystem
{
    /// <summary>
    /// DayChangeWatcher — Memantau pergantian hari (00:00) dan
    /// memanggil PrayerTimeManager.RequestRefresh() secara otomatis.
    /// Juga menangani kasus app resume dari background.
    /// </summary>
    public class DayChangeWatcher : MonoBehaviour
    {
        private string    _lastDate = "";
        private Coroutine _watchRoutine;

        private void Start()
        {
            _lastDate     = DateTime.Now.ToString("yyyy-MM-dd");
            _watchRoutine = StartCoroutine(WatchRoutine());
        }

        private void OnDestroy()
        {
            if (_watchRoutine != null) StopCoroutine(_watchRoutine);
        }

        // Check setiap menit (hemat resource vs setiap detik)
        private IEnumerator WatchRoutine()
        {
            var wait = new WaitForSeconds(60f);
            while (true)
            {
                yield return wait;
                CheckDayChange();
            }
        }

        private void CheckDayChange()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today != _lastDate)
            {
                Debug.Log($"[DayChangeWatcher] Hari berganti dari {_lastDate} ke {today}. Refresh jadwal...");
                _lastDate = today;
                PrayerTimeManager.Instance.RequestRefresh();
            }
        }

        // Deteksi app resume dari background (Android/iOS)
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                Debug.Log("[DayChangeWatcher] App kembali fokus — cek perlu refresh.");
                CheckDayChange();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                Debug.Log("[DayChangeWatcher] App resume dari pause — cek perlu refresh.");
                CheckDayChange();
            }
        }
    }
}
