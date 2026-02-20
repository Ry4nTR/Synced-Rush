using SyncedRush.Generics;
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
    [SerializeField] private AudioClip countdownSound;

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

    private IEnumerator CountdownCoroutine(float seconds, Action onFinished)
    {
        int remaining = Mathf.FloorToInt(seconds);

        while (remaining > 0)
        {
            if (countdownText != null)
                countdownText.text = remaining.ToString();

            if (remaining < 4)
                AudioManager.Instance.PlayUISound(countdownSound);

            yield return new WaitForSeconds(1f);

            remaining--;
        }

        if (countdownText != null)
            countdownText.text = string.Empty;

        routine = null;
        onFinished?.Invoke();
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
}