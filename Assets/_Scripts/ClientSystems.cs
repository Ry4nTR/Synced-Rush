using UnityEngine;

public class ClientSystems : MonoBehaviour
{
    [field: SerializeField] public GameplayUIManager UI { get; private set; }
    [field: SerializeField] public DeathCamController DeathCam { get; private set; }

    private void Awake()
    {
        if (UI == null) UI = GetComponentInChildren<GameplayUIManager>(true);
        if (DeathCam == null) DeathCam = GetComponentInChildren<DeathCamController>(true);
    }

    private void OnEnable()
    {
        RoundManager.OnKillcamRequested += HandleKillcamRequested;
        RoundManager.OnRoundEndPresentation += HandleRoundEnd;
        RoundManager.OnPreRoundStarted += HandlePreRound;
        RoundManager.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        RoundManager.OnKillcamRequested -= HandleKillcamRequested;
        RoundManager.OnRoundEndPresentation -= HandleRoundEnd;
        RoundManager.OnPreRoundStarted -= HandlePreRound;
        RoundManager.OnMatchEnded -= HandleMatchEnded;
    }

    private void HandleKillcamRequested(ulong killerId, float seconds)
        => DeathCam?.PlayKillcamByKiller(killerId, seconds);

    private void HandleRoundEnd(int a, int b, bool matchOver, float seconds)
        => UI?.PlayRoundEndPresentation(a, b, matchOver, seconds);

    private void HandlePreRound(float duration)
    {
        if (UI == null) return;
        UI.HideScorePanel();
        UI.StartCountdown(duration, () =>
        {
            UI.HideLoadoutPanel();
            UI.ShowHUD();
        });
    }

    private void HandleMatchEnded(int winnerTeam, int a, int b)
    {
        UI?.ShowScorePanel(a, b, true);
    }
}
