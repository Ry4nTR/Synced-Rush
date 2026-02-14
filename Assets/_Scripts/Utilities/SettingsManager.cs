using UnityEngine;
using UnityEngine.Audio;

namespace SyncedRush.Generics
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio")]
        public AudioMixer mainMixer;

        public float FOV => PlayerPrefs.GetFloat("Settings_FOV", 60f);
        public float Sensitivity => PlayerPrefs.GetFloat("Settings_Sens", 100f);


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            //LoadAllSettings();
        }

        // Public API

        public void SaveFOV(float value)
        {
            PlayerPrefs.SetFloat("Settings_FOV", value);
        }

        public void SaveSensitivity(float value)
        {
            PlayerPrefs.SetFloat("Settings_Sens", value);
        }

        public void SaveAudio(float value)
        {
            PlayerPrefs.SetFloat("Settings_Audio", value);
            ApplyAudio(value);
        }

        // Settings apply
        private void ApplyAudio(float value)
        {
            // Formula per convertire il valore dello slider (0.0001 a 1) in Decibel
            float db = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20;
            mainMixer.SetFloat("MasterVol", db);
        }

        //private void ApplyFOV(float value)
        //{
        //    if (Camera.main != null) Camera.main.fieldOfView = value;
        //}


        //private void LoadAllSettings()
        //{
        //    ApplyAudio(PlayerPrefs.GetFloat("Settings_Audio", 0.75f));

        //    // Ricordati di chiamare PlayerPrefs.Save() quando l'utente preme "Applica" o chiude il menu
        //}
    }
}