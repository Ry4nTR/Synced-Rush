using UnityEngine;

public class ClientSystems : MonoBehaviour
{
    [field: SerializeField] public GameplayUIManager UI { get; private set; }
    [field: SerializeField] public DeathCamController DeathCam { get; private set; }

    private void Awake()
    {
        var all = FindObjectsByType<ClientSystems>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Debug.LogWarning("[ClientSystems] Duplicate detected, destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        if (UI == null) UI = GetComponentInChildren<GameplayUIManager>(true);
        if (DeathCam == null) DeathCam = GetComponentInChildren<DeathCamController>(true);
    }

    private void OnEnable()
    {
        RoundManager.OnRoundEndPresentation += HandleRoundEnd;
        RoundManager.OnPreRoundStarted += HandlePreRound;
        RoundManager.OnMatchEnded += HandleMatchEnded;
        RoundManager.OnKillcamRequested += HandleKillcamRequested;
    }

    private void OnDisable()
    {
        RoundManager.OnRoundEndPresentation -= HandleRoundEnd;
        RoundManager.OnPreRoundStarted -= HandlePreRound;
        RoundManager.OnMatchEnded -= HandleMatchEnded;
        RoundManager.OnKillcamRequested -= HandleKillcamRequested;
    }


    private void HandleKillcamRequested(ulong killerId, float seconds)
        => DeathCam?.PlayKillcamByKiller(killerId, seconds);

    private void HandleRoundEnd(int a, int b, bool matchOver, float seconds)
    {
        DeathCam?.StopKillcam(); // hard transition: scoreboard should never be on spectator cam
        UI?.PlayRoundEndPresentation(a, b, matchOver, seconds);
    }

    private void HandlePreRound(float duration)
    {
        DeathCam?.StopKillcam(keepHardCutUntilRespawn: true);
        DeathCam?.RestoreBlendAfterKillcam();

        // your existing UI logic...
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
