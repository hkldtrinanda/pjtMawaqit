using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrayerSystem.Models
{
    // ─────────────────────────────────────────────
    // Runtime Model
    // ─────────────────────────────────────────────
    [Serializable]
    public class MosqueInfo
    {
        public long   osmId;
        public string name;
        public string type;          // "Masjid" / "Mushola"
        public float  lat;
        public float  lon;
        public float  distanceKm;    // dihitung lokal dari koordinat user

        /// <summary>Formatted jarak — "250 m" atau "1.2 km"</summary>
        public string DistanceFormatted =>
            distanceKm < 1f
                ? $"{(int)(distanceKm * 1000)} m"
                : $"{distanceKm:F1} km";

        /// <summary>Deep-link Google Maps navigasi ke koordinat ini.</summary>
        public string MapsUrl =>
            $"https://www.google.com/maps/dir/?api=1&destination={lat},{lon}&travelmode=walking";

        /// <summary>Deep-link Google Maps search by name (fallback jika koordinat kurang akurat).</summary>
        public string MapsSearchUrl =>
            $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(name)}&center={lat},{lon}";
    }

    // ─────────────────────────────────────────────
    // Overpass API Response Models
    // ─────────────────────────────────────────────
    // Struktur JSON Overpass:
    // { "elements": [ { "type", "id", "lat", "lon", "tags": { "name", "amenity" } } ] }

    [Serializable]
    public class OverpassResponse
    {
        public List<OverpassElement> elements;
    }

    [Serializable]
    public class OverpassElement
    {
        public string type;   // "node" | "way" | "relation"
        public long   id;
        public float  lat;
        public float  lon;
        public OverpassTags tags;

        // Untuk way/relation, koordinat ada di "center"
        public OverpassCenter center;
    }

    [Serializable]
    public class OverpassTags
    {
        public string name;
        public string amenity;       // "place_of_worship"
        public string religion;      // "muslim"
        public string place_of_worship; // "mosque" / "musalla"
    }

    [Serializable]
    public class OverpassCenter
    {
        public float lat;
        public float lon;
    }
}
