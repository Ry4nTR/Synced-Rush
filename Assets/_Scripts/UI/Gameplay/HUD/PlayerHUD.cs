using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class PlayerHUD : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text healthText;

    [Header("UI Elements")]
    [SerializeField] private Image hpBar;
    [SerializeField] private Image jetpackBar;
    [SerializeField] private Image dashBar;

    [Header("Ability Elements")]
    [SerializeField] private GameObject jetpackAbilityElements;

    [Header("UI Elements Controllers")]
    [SerializeField] private DamageIndicatorController damageIndicatorController;

    // =====================================================
    // HITMARKER
    // =====================================================
    [Header("Hitmarker - Refs")]
    [SerializeField] private Image hitmarkerImage;
    [SerializeField] private CanvasGroup hitmarkerGroup;

    [Header("Hitmarker - Timing")]
    [SerializeField] private float hitmarkerHold = 0.08f;
    [SerializeField] private float hitmarkerFade = 0.10f;

    [Header("Hitmarker - Punch")]
    [SerializeField, Range(0f, 0.25f)] private float hitmarkerScalePunch = 0.06f;

    [Header("Hitmarker - Colors (for settings later)")]
    [SerializeField] private Color hitmarkerNormalColor = Color.white;
    [SerializeField] private Color hitmarkerHeadshotColor = Color.red;
    [SerializeField] private Color hitmarkerKillColor = Color.cyan;

    [Header("Disconnect Message")]
    [Tooltip("UI text element used to display disconnect announcements. Optional.")]
    [SerializeField] private TMP_Text disconnectMessageText;

    // Coroutine handle for disconnect message
    private Coroutine disconnectCoroutine;

    private Coroutine _hitmarkerRoutine;
    private Vector3 _hitmarkerBaseScale;

    // Cached gameplay references (LOCAL PLAYER ONLY)
    private WeaponController weapon;
    private HealthSystem health;

    private void Awake()
    {
        if (hitmarkerImage != null)
            _hitmarkerBaseScale = hitmarkerImage.rectTransform.localScale;

        if (hitmarkerGroup != null)
        {
            hitmarkerGroup.alpha = 0f;
            hitmarkerGroup.interactable = false;
            hitmarkerGroup.blocksRaycasts = false;
        }
    }

    private void Update() => UpdateAmmo();

    private void OnDestroy()
    {
        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;
    }

    // =========================
    // BINDING PLAYER AND WEAPON
    // =========================
    public void BindPlayer(GameObject player)
    {
        if (health != null)
            health.currentHealth.OnValueChanged -= OnHealthChanged;

        health = player.GetComponent<HealthSystem>();

        if (health != null)
            health.currentHealth.OnValueChanged += OnHealthChanged;

        UpdateHealth();
    }

    public void BindWeapon(WeaponController weaponController)
    {
        weapon = weaponController;
        UpdateAmmo();
    }

    public void SetJetpackUIVisibility(bool value) => jetpackAbilityElements.SetActive(value);

    public void UpdateDashCharge(float currentCharge, float maxCharge)
        => dashBar.fillAmount = (maxCharge > 0f) ? (currentCharge / maxCharge) : 0f;

    public void UpdateJetpackCharge(float currentCharge, float maxCharge)
        => jetpackBar.fillAmount = (maxCharge > 0f) ? (currentCharge / maxCharge) : 0f;

    // =========================
    // DAMAGE INDICATOR
    // =========================
    public void ShowDamageIndicator(Vector3 attackerPosition, Transform localPlayerTransform)
    {
        if (damageIndicatorController == null || localPlayerTransform == null) return;
        damageIndicatorController.OnTakeDamage(localPlayerTransform, attackerPosition);
    }

    // =========================
    // HITMARKER
    // =========================
    public void PlayHitmarker(bool isKill, bool isHeadshot)
    {
        if (hitmarkerImage == null || hitmarkerGroup == null) return;

        if (_hitmarkerRoutine != null)
            StopCoroutine(_hitmarkerRoutine);

        _hitmarkerRoutine = StartCoroutine(HitmarkerRoutine(isKill, isHeadshot));
    }

    /// <summary>
    /// Shows a temporary disconnect message on the HUD. If the message text reference is not assigned, this call is ignored.
    /// </summary>
    public void ShowDisconnectMessage(string message, float duration)
    {
        if (disconnectMessageText == null) return;

        disconnectMessageText.gameObject.SetActive(true);
        disconnectMessageText.text = message;

        // Cancel any previous hide coroutine
        if (disconnectCoroutine != null)
            StopCoroutine(disconnectCoroutine);

        disconnectCoroutine = StartCoroutine(HideDisconnectAfterTime(duration));
    }

    private IEnumerator HideDisconnectAfterTime(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (disconnectMessageText != null)
            disconnectMessageText.gameObject.SetActive(false);
        disconnectCoroutine = null;
    }

    private IEnumerator HitmarkerRoutine(bool isKill, bool isHeadshot)
    {
        // Priority: KILL > HEADSHOT > NORMAL
        Color col = isKill ? hitmarkerKillColor : (isHeadshot ? hitmarkerHeadshotColor : hitmarkerNormalColor);

        hitmarkerImage.color = col;
        hitmarkerGroup.alpha = 1f;

        // Punch-in (COD-like)
        if (hitmarkerScalePunch > 0f && hitmarkerImage != null)
            hitmarkerImage.rectTransform.localScale = _hitmarkerBaseScale * (1f + hitmarkerScalePunch);

        // snap back quickly
        float snapBackTime = 0.05f;
        float t = 0f;
        while (t < snapBackTime)
        {
            t += Time.deltaTime;
            if (hitmarkerImage != null)
                hitmarkerImage.rectTransform.localScale = Vector3.Lerp(
                    hitmarkerImage.rectTransform.localScale,
                    _hitmarkerBaseScale,
                    t / snapBackTime
                );
            yield return null;
        }

        // Hold
        if (hitmarkerHold > 0f)
            yield return new WaitForSeconds(hitmarkerHold);

        // Fade
        float f = 0f;
        float fadeTime = Mathf.Max(0.001f, hitmarkerFade);
        while (f < fadeTime)
        {
            f += Time.deltaTime;
            hitmarkerGroup.alpha = Mathf.Lerp(1f, 0f, f / fadeTime);
            yield return null;
        }

        hitmarkerGroup.alpha = 0f;

        // restore scale
        if (hitmarkerImage != null)
            hitmarkerImage.rectTransform.localScale = _hitmarkerBaseScale;

        _hitmarkerRoutine = null;
    }

    // =========================
    // HUD UPDATES
    // =========================
    private void UpdateAmmo()
    {
        ammoText.text = weapon != null
            ? $"{weapon.CurrentAmmo} / {weapon.ReserveAmmo}"
            : "-- / --";
    }

    private void UpdateHealth()
    {
        healthText.text = health != null
            ? $"HP {health.currentHealth.Value:F1}"
            : "HP --";

        hpBar.fillAmount = (health != null && health.maxHealth > 0f)
            ? (health.CurrentHealth / health.maxHealth)
            : 0f;
    }

    private void OnHealthChanged(float oldValue, float newValue) => UpdateHealth();
}
