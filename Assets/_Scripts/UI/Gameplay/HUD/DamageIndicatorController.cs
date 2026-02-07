using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorController : MonoBehaviour
{
    private readonly Image indicatorImage;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 2f;

    private Material indicatorMat;
    private Coroutine fadeCoroutine;

    void Start()
    {
        if (indicatorImage != null)
        {
            indicatorMat = indicatorImage.material;
            indicatorMat.SetFloat("_Intensity", 0);
        }
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

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeEffect());
    }

    IEnumerator FadeEffect()
    {
        float elapsed = 0;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float intensity = Mathf.Lerp(1, 0, elapsed / fadeDuration);
            indicatorMat.SetFloat("_Intensity", intensity);

            yield return null;
        }

        indicatorMat.SetFloat("_Intensity", 0f);
    }
}
