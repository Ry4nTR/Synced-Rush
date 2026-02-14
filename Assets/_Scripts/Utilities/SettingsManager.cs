using SyncedRush.UI.Settings;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace SyncedRush.Generics
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio")]
        public AudioMixer mainMixer;

        public float FOV => PlayerPrefs.GetFloat("Settings_FOV", 60f);
        public float Sensitivity => PlayerPrefs.GetFloat("Settings_Sens", 100f);
        public CrosshairSettings CrosshairSettings
        {
            get
            {
                CrosshairSettings settings = new();

                settings.targetLineLength = PlayerPrefs.GetFloat("Settings_Crosshair_LineLength", 10f);
                settings.targetThickness = PlayerPrefs.GetFloat("Settings_Crosshair_Thickness", 2f);
                settings.targetGap = PlayerPrefs.GetFloat("Settings_Crosshair_Gap", 5f);
                settings.targetCenterDotSize = PlayerPrefs.GetFloat("Settings_Crosshair_DotSize", 2f);
                settings.smoothTime = PlayerPrefs.GetFloat("Settings_Crosshair_Smooth", 0.1f);
                settings.opacity = PlayerPrefs.GetFloat("Settings_Crosshair_Opacity", 1f);

                settings.showCenterDot = PlayerPrefs.GetInt("Settings_Crosshair_ShowDot", 1) == 1;

                float r = PlayerPrefs.GetFloat("Settings_Crosshair_ColorR", 1f);
                float g = PlayerPrefs.GetFloat("Settings_Crosshair_ColorG", 1f);
                float b = PlayerPrefs.GetFloat("Settings_Crosshair_ColorB", 1f);
                settings.crosshairColor = new Color(r, g, b, settings.opacity);

                return settings;
            }
        }

        public Resolution[] Resolutions { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Resolutions = Screen.resolutions;

            LoadAllSettings();
        }

        // --- PUBLIC API: SALVATAGGIO ---


        public void SaveFOV(float value)
        {
            PlayerPrefs.SetFloat("Settings_FOV", value);
            PlayerPrefs.Save();
        }

        public void SaveSensitivity(float value)
        {
            PlayerPrefs.SetFloat("Settings_Sens", value);
            PlayerPrefs.Save();
        }

        public void SaveAudio(float value)
        {
            PlayerPrefs.SetFloat("Settings_Audio", value);
            ApplyAudio(value);
            PlayerPrefs.Save();
        }

        public void SaveResolution(int width, int height)
        {
            Screen.SetResolution(width, height, Screen.fullScreen);
            PlayerPrefs.SetInt("Settings_ResWidth", width);
            PlayerPrefs.SetInt("Settings_ResHeight", height);
            PlayerPrefs.Save();
        }

        // ESEMPIO PER CROSSHAIR (Parametri dinamici)
        public void SaveAnyFloat(string key, float value)
        {
            PlayerPrefs.SetFloat("Settings_" + key, value);
            PlayerPrefs.Save();
        }

        public float GetAnyFloat(string key)
        {
            return PlayerPrefs.GetFloat("Settings_" + key, 0f);
        }

        // --- SETTINGS APPLY & LOAD ---

        private void ApplyAudio(float value)
        {
            float db = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20;
            mainMixer.SetFloat("MasterVol", db);
        }

        public void LoadAllSettings()
        {
            ApplyAudio(PlayerPrefs.GetFloat("Settings_Audio", 1f));

            int resIndex = PlayerPrefs.GetInt("Settings_ResIndex", -1);
            if (resIndex != -1 && resIndex < Resolutions.Length)
            {
                Resolution res = Resolutions[resIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }

            PlayerPrefs.Save();
        }
    }
}