using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorController : MonoBehaviour
{
    private Image indicatorImage;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 2f;

    private Material indicatorMat;
    private Coroutine fadeCoroutine;

    void Start()
    {
        indicatorImage = GetComponent<Image>();
        if (indicatorImage != null)
        {
            SetAlpha(0f);
            indicatorMat = indicatorImage.material;
        }
        else
            Debug.LogError("Indicator image null!");
    }

    // Public API
    public void OnTakeDamage(Vector3 playerPosition, Vector3 enemyPosition)
    {
        Vector3 directionToEnemy = enemyPosition - playerPosition;
        directionToEnemy.y = 0; // Non serve l'altezza

        float angle = Vector3.SignedAngle(playerPosition, directionToEnemy, Vector3.up);

        // Lo shader si aspetta 0.5 come "davanti", quindi aggiungiamo l'offset
        float shaderAngle = 1.0f - Mathf.Repeat(angle / 360f + 0.5f, 1.0f);

        indicatorMat.SetFloat("_HitAngle", shaderAngle);
        SetAlpha(1f);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeEffect());
    }

    private void SetAlpha(float alphaValue)
    {
        Color tempColor = indicatorImage.color;

        tempColor.a = alphaValue;

        indicatorImage.color = tempColor;
    }

    IEnumerator FadeEffect()
    {
        float elapsed = 0;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float intensity = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            SetAlpha(intensity);

            yield return null;
        }

        SetAlpha(0f);
    }
}
