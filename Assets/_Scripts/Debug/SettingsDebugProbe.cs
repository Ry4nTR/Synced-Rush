using System.IO;
using UnityEngine;

namespace SyncedRush.Generics
{
    public class SettingsDebugProbe : MonoBehaviour
    {
        [SerializeField] private bool logOnStart = true;
        [SerializeField] private bool logOnSettingsChanged = true;
        [SerializeField] private bool logFileContentsOnChange = false;

        string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        void Start()
        {
            if (logOnStart) Dump("START");

            var sm = SettingsManager.Instance;
            if (sm != null && logOnSettingsChanged)
                sm.OnSettingsChanged += HandleChanged;
        }

        void OnDestroy()
        {
            var sm = SettingsManager.Instance;
            if (sm != null && logOnSettingsChanged)
                sm.OnSettingsChanged -= HandleChanged;
        }

        void HandleChanged()
        {
            Dump("CHANGED");
        }

        void Dump(string tag)
        {
            var sm = SettingsManager.Instance;
            if (sm == null)
            {
                Debug.LogError("[SettingsDebugProbe] SettingsManager.Instance is null.");
                return;
            }

            bool exists = File.Exists(FilePath);
            long bytes = exists ? new FileInfo(FilePath).Length : 0;

            Debug.Log(
                $"[SettingsDebugProbe:{tag}] file={FilePath} exists={exists} bytes={bytes}\n" +
                $"sens={sm.Data.sensitivity} invertY={sm.Data.invertY}\n" +
                $"worldFov={sm.Data.worldFov} viewmodelFov={sm.Data.viewmodelFov}\n" +
                $"masterVolume(norm 0..1)={sm.Data.masterVolume}\n" +
                $"crosshair color={sm.Data.crosshair.color} opacity={sm.Data.crosshair.opacity}"
            );

            if (logFileContentsOnChange && exists)
            {
                string json = File.ReadAllText(FilePath);
                Debug.Log($"[SettingsDebugProbe:{tag}] JSON:\n{json}");
            }
        }
    }
}