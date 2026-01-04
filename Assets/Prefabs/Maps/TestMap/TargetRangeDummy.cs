using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
public class TargetRangeDummy : NetworkBehaviour
{
    [Header("Dummy Settings")]
    [SerializeField] private float respawnTime = 3f;

    [Header("UI")]
    [SerializeField] private TextMeshPro healthText;

    private HealthSystem healthSystem;
    private Collider[] colliders;
    private Renderer[] renderers;

    private bool isDisabled;

    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        UpdateHealthVisuals(healthSystem.CurrentHealth);
        healthSystem.currentHealth.OnValueChanged += OnHealthChanged;
    }

    private void OnHealthChanged(float previous, float current)
    {
        UpdateHealthVisuals(current);

        if (current <= 0f && !isDisabled)
        {
            if (IsServer)
                StartCoroutine(RespawnRoutine());
        }
    }

    private void UpdateHealthVisuals(float currentHealth)
    {
        float normalized = Mathf.Clamp01(currentHealth / healthSystem.maxHealth);

        healthText.text = Mathf.CeilToInt(currentHealth).ToString();

        Color healthColor = Color.Lerp(Color.red, Color.green, normalized);
        healthText.color = healthColor;
    }

    private IEnumerator RespawnRoutine()
    {
        isDisabled = true;
        DisableDummy();

        yield return new WaitForSeconds(respawnTime);

        healthSystem.Respawn();
        EnableDummy();

        isDisabled = false;
    }

    private void DisableDummy()
    {
        foreach (var r in renderers)
            r.enabled = false;

        foreach (var c in colliders)
            c.enabled = false;

        if (healthText != null)
            healthText.gameObject.SetActive(false);
    }

    private void EnableDummy()
    {
        foreach (var r in renderers)
            r.enabled = true;

        foreach (var c in colliders)
            c.enabled = true;

        if (healthText != null)
            healthText.gameObject.SetActive(true);
    }
}
