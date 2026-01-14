using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Updates the countdown text for the pre‑round timer.  Does not handle
/// showing or hiding the panel; that is managed by the UI manager.
/// Attach this script to the countdown panel and assign the text field.
/// </summary>
public class RoundCountdownPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;

    private Coroutine routine;

    public void StartCountdown(float seconds, Action onFinished = null)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        routine = StartCoroutine(CountdownCoroutine(seconds, onFinished));
    }

    public void CancelCountdown()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (countdownText != null)
            countdownText.text = string.Empty;
    }

    private IEnumerator CountdownCoroutine(float seconds, Action onFinished)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();
            yield return null;
            remaining -= Time.deltaTime;
        }
        if (countdownText != null)
            countdownText.text = string.Empty;
        routine = null;
        onFinished?.Invoke();
    }
}