using SyncedRush.Generics;
using System.Collections;
using UnityEngine;

public class CrosshairApplier : MonoBehaviour
{
    [SerializeField] private CrosshairController crosshairController;

    void Awake()
    {
        if (crosshairController == null)
            crosshairController = GetComponent<CrosshairController>();
    }

    private void OnEnable()
    {
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsChanged += ApplyNow;

        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        ApplyNow();
    }

    void OnDisable()
    {
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsChanged -= ApplyNow;
    }

    void ApplyNow()
    {
        if (crosshairController == null) return;

        var sm = SettingsManager.Instance;
        if (sm == null) return;

        crosshairController.ApplySettings(sm.Data.crosshair);
    }
}