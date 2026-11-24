using UnityEngine;

namespace FpsMaps.Dome
{
    /// <summary>Shared dimensions for the Dome map (metres, origin at dome centre, floor top at y = 0).</summary>
    public static class DomeLayout
    {
        public const string SceneName = "Dome";
        public const string ScenePath = "Assets/Scenes/Dome.unity";
        public const string RootFolder = "Assets/Maps/Dome";

        public const float DomeRadius = 18f;
        public const float DomeHeightScale = 0.7f;
        public const int GeodesicFrequency = 4;

        public const float DrumRadius = 17.8f;
        public const float DrumHeight = 3.5f;
        public const float DrumThickness = 1.2f;
        public const int DrumSegments = 24;

        public const float CatwalkRadius = 12.5f;
        public const float CatwalkWidth = 3f;
        public const float CatwalkTopY = 3.2f;
        public const float CatwalkThickness = 0.4f;

        public const float CoreRadius = 3f;
        public const float CoreHeight = 0.8f;

        public const float ArenaHalfSize = 40f;
        public const float PerimeterHeight = 9f;
        public const float PerimeterThickness = 1.2f;

        public const float TowerTopY = 6f;
        public const float DoorClearHeight = 3.6f;
        public const float DoorHalfAngleDeg = 16f;

        /// <summary>Door azimuths in degrees: 0 = +Z (north), 90 = +X (east).</summary>
        public static readonly float[] DoorAzimuthsDeg = { 0f, 90f, 180f, 270f };

        /// <summary>Horizontal direction for an azimuth in degrees (0 = +Z, 90 = +X).</summary>
        public static Vector3 Direction(float azimuthDeg)
        {
            float r = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        public static float AzimuthOf(Vector3 p)
        {
            return Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
        }
    }
}
