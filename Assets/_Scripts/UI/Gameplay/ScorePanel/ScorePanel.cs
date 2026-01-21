using TMPro;
using UnityEngine;

/// <summary>
/// Simple scoreboard panel to display round and match scores.  Attach this
/// script to a UI panel and assign the text fields for each team.  Call
/// <see cref="ShowScores"/> to update the displayed scores and show the panel.
/// Use <see cref="Hide"/> to hide the panel between rounds.
/// </summary>
public class ScorePanel : MonoBehaviour
{
    [Header("Score Text Fields")]
    [SerializeField] private TMP_Text teamAScoreText;
    [SerializeField] private TMP_Text teamBScoreText;
    [SerializeField] private TMP_Text headerText;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        Hide();
    }

    /// <summary>
    /// Display the given scores and optionally override the header.
    /// </summary>
    public void ShowScores(int teamAScore, int teamBScore, bool matchOver)
    {
        if (teamAScoreText != null) teamAScoreText.text = teamAScore.ToString();
        if (teamBScoreText != null) teamBScoreText.text = teamBScore.ToString();

        if (headerText != null)
        {
            headerText.text = matchOver ? "Match Over" : "Round Over";
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Hide the scoreboard panel.
    /// </summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}