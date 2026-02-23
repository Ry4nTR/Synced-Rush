using SyncedRush.UI.Settings;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace SyncedRush.Generics
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public event Action OnSettingsChanged;
        public event Action OnRebindsUpdate;

        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private AudioMixer mainMixer;

        public SettingsData Data { get; private set; } = new SettingsData();

        string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public float Sensitivity => Data.sensitivity;
        public bool InvertY => Data.invertY;

        public float WorldFOV => Data.worldFov;
        public float ViewmodelFOV => Data.viewmodelFov;

        public int ResolutionWidth => Data.resolutionWidth;
        public int ResolutionHeight => Data.resolutionHeight;

        public CrosshairConfig Crosshair => Data.crosshair;

        public Resolution[] Resolutions => Screen.resolutions;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
            ApplyAll();
        }

        // --------------------------
        // Load / Save
        // --------------------------

        public void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonUtility.FromJson<SettingsData>(json);
                    if (loaded != null) Data = loaded;
                }
                catch { /* ignore and keep defaults */ }
            }

            ApplyRebindsInternal();
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(FilePath, json);
            }
            catch { /* ignore */ }
        }

        // --------------------------
        // Generic Get/Set
        // --------------------------

        public float GetFloat(FloatSettingKey key)
        {
            return key switch
            {
                FloatSettingKey.Sensitivity => Data.sensitivity,
                FloatSettingKey.WorldFov => Data.worldFov,
                FloatSettingKey.ViewmodelFov => Data.viewmodelFov,

                // Stored normalized 0..1
                FloatSettingKey.MasterVolume => Data.masterVolume,

                FloatSettingKey.CrosshairLineLength => Data.crosshair.lineLength,
                FloatSettingKey.CrosshairThickness => Data.crosshair.thickness,
                FloatSettingKey.CrosshairGap => Data.crosshair.gap,
                FloatSettingKey.CrosshairDotSize => Data.crosshair.dotSize,
                FloatSettingKey.CrosshairSmoothTime => Data.crosshair.smoothTime,
                FloatSettingKey.CrosshairOpacity => Data.crosshair.opacity,
                _ => 0f
            };
        }

        public void SetFloat(FloatSettingKey key, float value)
        {
            bool changed = false;

            switch (key)
            {
                case FloatSettingKey.Sensitivity:
                    value = Mathf.Clamp(value, 1f, 100f);
                    changed = !Mathf.Approximately(Data.sensitivity, value);
                    Data.sensitivity = value;
                    break;

                case FloatSettingKey.WorldFov:
                    value = Mathf.Clamp(value, 60f, 120f);
                    changed = !Mathf.Approximately(Data.worldFov, value);
                    Data.worldFov = value;
                    break;

                case FloatSettingKey.ViewmodelFov:
                    value = Mathf.Clamp(value, 60f, 120f);
                    changed = !Mathf.Approximately(Data.viewmodelFov, value);
                    Data.viewmodelFov = value;
                    break;

                case FloatSettingKey.MasterVolume:
                    value = Mathf.Clamp(value, 0f, 100f);
                    changed = !Mathf.Approximately(Data.masterVolume, value);
                    Data.masterVolume = value;
                    break;

                case FloatSettingKey.CrosshairLineLength:
                    changed = !Mathf.Approximately(Data.crosshair.lineLength, value);
                    Data.crosshair.lineLength = value;
                    break;

                case FloatSettingKey.CrosshairThickness:
                    changed = !Mathf.Approximately(Data.crosshair.thickness, value);
                    Data.crosshair.thickness = value;
                    break;

                case FloatSettingKey.CrosshairGap:
                    changed = !Mathf.Approximately(Data.crosshair.gap, value);
                    Data.crosshair.gap = value;
                    break;

                case FloatSettingKey.CrosshairDotSize:
                    changed = !Mathf.Approximately(Data.crosshair.dotSize, value);
                    Data.crosshair.dotSize = value;
                    break;

                case FloatSettingKey.CrosshairSmoothTime:
                    changed = !Mathf.Approximately(Data.crosshair.smoothTime, value);
                    Data.crosshair.smoothTime = value;
                    break;

                case FloatSettingKey.CrosshairOpacity:
                    {
                        value = Mathf.Clamp(value, 0f, 100f);
                        changed = !Mathf.Approximately(Data.crosshair.opacity, value);
                        Data.crosshair.opacity = value;
                        break;
                    }
            }

            if (!changed) return;

            Save();
            ApplyForKey(key);
            OnSettingsChanged?.Invoke();
        }

        public bool GetBool(BoolSettingKey key)
        {
            return key switch
            {
                UI.Settings.BoolSettingKey.InvertY => Data.invertY,
                UI.Settings.BoolSettingKey.CrosshairShowDot => Data.crosshair.showDot,
                _ => false
            };
        }

        public void SetBool(BoolSettingKey key, bool value)
        {
            bool changed = false;

            switch (key)
            {
                case UI.Settings.BoolSettingKey.InvertY:
                    changed = Data.invertY != value;
                    Data.invertY = value;
                    break;

                case UI.Settings.BoolSettingKey.CrosshairShowDot:
                    changed = Data.crosshair.showDot != value;
                    Data.crosshair.showDot = value;
                    break;
            }

            if (!changed) return;

            Save();
            OnSettingsChanged?.Invoke();
        }

        // --------------------------
        // Crosshair color
        // --------------------------

        public Color GetCrosshairColor() => Data.crosshair.color;

        public void SetCrosshairColor(Color color)
        {
            if (Data.crosshair.color == color) return;
            Data.crosshair.color = color;
            Save();
            OnSettingsChanged?.Invoke();
        }

        // --------------------------
        // Resolution
        // --------------------------

        public void SetResolution(int width, int height, bool fullscreen = true)
        {
            if (Data.resolutionWidth == width && Data.resolutionHeight == height)
                return;

            Data.resolutionWidth = width;
            Data.resolutionHeight = height;

            Save();
            ApplyResolution(fullscreen);
            OnSettingsChanged?.Invoke();
        }

        void ApplyResolution(bool fullscreen)
        {
            if (Data.resolutionWidth > 0 && Data.resolutionHeight > 0)
                Screen.SetResolution(Data.resolutionWidth, Data.resolutionHeight, fullscreen);
        }

        // --------------------------
        // Audio
        // --------------------------

        void ApplyAudio()
        {
            if (mainMixer == null) return;

            float v = Mathf.Clamp(Data.masterVolume, 0f, 100f) / 100f;

            // Linear(0..1) -> dB
            float db = (v <= 0.0001f) ? -80f : Mathf.Log10(v) * 20f;

            // Exposed parameter name must match your mixer
            mainMixer.SetFloat("Master", db);
        }

        // --------------------------
        // Rebinds
        // --------------------------

        public void SaveRebinds()
        {
            if (inputActions == null) return;
            Data.rebindsJson = inputActions.SaveBindingOverridesAsJson();
            Save();
            OnRebindsUpdate?.Invoke();
        }

        public void ResetAllBindings()
        {
            if (inputActions == null) return;

            foreach (var map in inputActions.actionMaps)
                map.RemoveAllBindingOverrides();

            Data.rebindsJson = string.Empty;
            Save();
            OnRebindsUpdate?.Invoke();
            OnSettingsChanged?.Invoke();
        }

        public void LoadRebinds()
        {
            ApplyRebindsInternal();
            OnRebindsUpdate?.Invoke();
        }

        void ApplyRebindsInternal()
        {
            if (inputActions == null) return;
            if (string.IsNullOrEmpty(Data.rebindsJson)) return;

            try { inputActions.LoadBindingOverridesFromJson(Data.rebindsJson); }
            catch
            {
                foreach (var map in inputActions.actionMaps)
                    map.RemoveAllBindingOverrides();
                Data.rebindsJson = string.Empty;
            }
        }

        // --------------------------
        // Apply
        // --------------------------

        public void ApplyAll()
        {
            ApplyAudio();
            ApplyResolution(fullscreen: true);
            OnSettingsChanged?.Invoke();
        }

        void ApplyForKey(FloatSettingKey key)
        {
            switch (key)
            {
                case FloatSettingKey.MasterVolume:
                    ApplyAudio();
                    break;
            }
        }
    }
}