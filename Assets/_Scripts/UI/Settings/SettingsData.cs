using System;
using UnityEngine;

namespace SyncedRush.Generics
{
    [Serializable]
    public class SettingsData
    {
        public float sensitivity = 10f;
        public bool invertY = false;

        public float worldFov = 60f;
        public float viewmodelFov = 90f;

        public int resolutionWidth = 0;
        public int resolutionHeight = 0;

        public float masterVolume = 100f;

        public CrosshairConfig crosshair = new CrosshairConfig();

        public string rebindsJson = string.Empty;
    }

    [Serializable]
    public class CrosshairConfig
    {
        public Color color = Color.white;

        public float lineLength = 10f;
        public float thickness = 2f;
        public float gap = 6f;
        public float dotSize = 2f;

        public float smoothTime = 0.08f;

        public float opacity = 100f;

        public bool showDot = true;
    }
}