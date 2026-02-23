using SyncedRush.Generics;
using Unity.Cinemachine;
using UnityEngine;

public class FovApplier : MonoBehaviour
{
    [Header("World (Cinemachine)")]
    [SerializeField] private CinemachineCamera worldCam;

    [Header("Viewmodel (Overlay Camera)")]
    [SerializeField] private Camera viewmodelCamera;

    private void OnEnable()
    {
        Apply();
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged += Apply;
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged -= Apply;
    }

    private void Apply()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        if (worldCam != null)
            worldCam.Lens.FieldOfView = sm.WorldFOV;

        if (viewmodelCamera != null)
            viewmodelCamera.fieldOfView = sm.ViewmodelFOV;
    }
}