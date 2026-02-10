using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageIndicatorController : MonoBehaviour
{

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 2f;

    private Image _indicatorImage;
    private Material _indicatorMat;
    private Coroutine _fadeCoroutine;

    private Vector3 _enemyPosOnHit = Vector3.zero;

    void Start()
    {
        _indicatorImage = GetComponent<Image>();
        if (_indicatorImage != null)
        {
            SetAlpha(0f);
            _indicatorMat = _indicatorImage.material;
        }
        else
            Debug.LogError("Indicator image null!");
    }

    // Public API
    public void OnTakeDamage(Vector3 playerPosition, Vector3 enemyPosition)
    {
        _enemyPosOnHit = enemyPosition;

        float shaderAngle = GetAngle(playerPosition, enemyPosition);

        _indicatorMat.SetFloat("_HitAngle", shaderAngle);
        SetAlpha(1f);

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeEffect());
    }

    private float GetAngle(Vector3 playerPosition, Vector3 enemyPosition)
    {
        Vector3 directionToEnemy = enemyPosition - playerPosition;
        directionToEnemy.y = 0; // Non serve l'altezza

        float angle = Vector3.SignedAngle(playerPosition, directionToEnemy, Vector3.up);

        // Lo shader si aspetta 0.5 come "davanti", quindi aggiungiamo l'offset
        return 1.0f - Mathf.Repeat(angle / 360f + 0.5f, 1.0f);
    }

    private void SetAlpha(float alphaValue)
    {
        Color tempColor = _indicatorImage.color;

        tempColor.a = alphaValue;

        _indicatorImage.color = tempColor;
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
