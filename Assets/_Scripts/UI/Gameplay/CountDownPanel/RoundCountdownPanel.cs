using SyncedRush.Generics;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class RoundCountdownPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private AudioClip countdownSound;

    private Coroutine routine;
    private int _lastSoundInt = -1;

    public void StartCountdown(float seconds, Action onFinished = null)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CountdownCoroutine(seconds, onFinished));
    }

    private IEnumerator CountdownCoroutine(float seconds, Action onFinished)
    {
        float endTime = Time.time + seconds;
        _lastSoundInt = -1;

        int lastRemainingInt = -1;

        while (Time.time < endTime)
        {
            int remainingInt = Mathf.CeilToInt(endTime - Time.time);

            // Only manipulate the string and the UI Canvas if the second has changed
            if (remainingInt != lastRemainingInt)
            {
                lastRemainingInt = remainingInt;
                if (countdownText != null) countdownText.text = remainingInt.ToString();

                if (remainingInt <= 3 && remainingInt != _lastSoundInt)
                {
                    _lastSoundInt = remainingInt;
                    AudioManager.Instance.PlayUISound(countdownSound);
                }
            }

            yield return null;
        }

        if (countdownText != null) countdownText.text = string.Empty;
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
        if (countdownText != null) countdownText.text = string.Empty;
    }
}