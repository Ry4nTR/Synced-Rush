using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class DeathCamController : MonoBehaviour
{
    // =====================================================
    // Inspector
    // =====================================================
    [Header("Refs")]
    [SerializeField] private CinemachineCamera spectatorVcam;

    [Header("Tuning")]
    [SerializeField] private int boostAbovePlayer = 100;
    [SerializeField] private bool forceHardCut = true;

    [Header("Team Spectate - Third Person")]
    [SerializeField] private Vector3 teamSpectateOffset = new Vector3(0f, 2.2f, -4f);
    [SerializeField] private float teamFollowSmooth = 10f;

    // =====================================================
    // Runtime
    // =====================================================
    private CinemachineCamera _localPlayerVcam;
    private int _spectatorBasePriority;
    private Coroutine _followRoutine;

    private Vector3 _deathPos;
    private Quaternion _deathRot;
    private bool _hasDeathPose;

    // Team spectate (used later)
    private bool _isTeamSpectating;
    private Transform _teamSpectateTarget;

    private float _savedBlendTime = -1f;

    private void Awake()
    {
        if (spectatorVcam == null) return;

        // Force spectator to be "loser" unless killcam is active.
        _spectatorBasePriority = -1000;
        spectatorVcam.Priority = _spectatorBasePriority;
    }

    // =====================================================
    // Public API
    // =====================================================
    public void CaptureDeathPose(Camera sourceCam)
    {
        if (sourceCam == null) return;

        _deathPos = sourceCam.transform.position;
        _deathRot = sourceCam.transform.rotation;
        _hasDeathPose = true;
    }

    public void PlayKillcamByKiller(ulong killerClientId, float seconds)
    {
        if (spectatorVcam == null) return;

        // avoid multiple routines fighting each other
        if (_followRoutine != null)
        {
            StopCoroutine(_followRoutine);
            _followRoutine = null;
        }

        _followRoutine = StartCoroutine(PlayKillcamWhenReady(killerClientId, seconds));
    }

    public void PlayKillcamByKillerAtPose(ulong killerClientId, float seconds, Vector3 pos, Quaternion rot)
    {
        _deathPos = pos;
        _deathRot = rot;
        _hasDeathPose = true;

        PlayKillcamByKiller(killerClientId, seconds);
    }

    public void StopKillcam(bool keepHardCutUntilRespawn = true)
    {
        if (_followRoutine != null)
        {
            StopCoroutine(_followRoutine);
            _followRoutine = null;
        }

        _isTeamSpectating = false;
        _teamSpectateTarget = null;

        if (spectatorVcam != null)
        {
            spectatorVcam.Priority = _spectatorBasePriority; // -1000
            spectatorVcam.Follow = null;
            spectatorVcam.LookAt = null;
        }

        // IMPORTANT:
        // If we restore blend immediately, you may see a smooth “reset”/blend.
        // Keep hard cut ON, and restore it later when respawn/pre-round happens.
        if (!keepHardCutUntilRespawn && forceHardCut)
            SetHardCut(false);

        _hasDeathPose = false;
    }

    // =====================================================
    // Killcam Flow
    // =====================================================
    private IEnumerator PlayKillcamWhenReady(ulong killerClientId, float seconds)
    {
        CacheLocalPlayerVcamIfNeeded();

        // -----------------------------------------------------
        // 1) Wait briefly for death pose (or fallback)
        // -----------------------------------------------------
        const float poseTimeout = 0.25f;
        float t = 0f;

        while (!_hasDeathPose && t < poseTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fallback: try to take any enabled camera pose
        if (!_hasDeathPose)
        {
            // Prefer an enabled camera if possible
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera best = null;
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].enabled && cams[i].gameObject.activeInHierarchy)
                {
                    best = cams[i];
                    break;
                }
            }
            if (best == null && cams.Length > 0) best = cams[0];

            if (best != null)
            {
                _deathPos = best.transform.position;
                _deathRot = best.transform.rotation;
                _hasDeathPose = true;
            }
        }

        // -----------------------------------------------------
        // 2) Resolve killer POV root
        // -----------------------------------------------------
        Transform killerRoot = ResolveKillerCameraRoot(killerClientId);

        if (killerRoot == null)
        {
            Debug.LogWarning($"[DeathCam] Killer root not found for {killerClientId}", this);
            _followRoutine = null;
            yield break;
        }

        // -----------------------------------------------------
        // 3) Activate spectator cam (snap to death pose first)
        // -----------------------------------------------------
        // Important: don't call StopKillcam() here because it would clear _hasDeathPose.
        // We already stopped previous routine before starting this one.
        _isTeamSpectating = false;
        _teamSpectateTarget = null;

        spectatorVcam.Follow = null;
        spectatorVcam.LookAt = null; // we drive transform manually (POV copy)

        if (_hasDeathPose)
        {
            spectatorVcam.transform.SetPositionAndRotation(_deathPos, _deathRot);
        }

        if (forceHardCut) SetHardCut(true);

        int playerPri = _localPlayerVcam != null ? _localPlayerVcam.Priority : 0;
        spectatorVcam.Priority = playerPri + boostAbovePlayer;

        // -----------------------------------------------------
        // 4) Stationary killcam: keep victim position, rotate toward killer
        // -----------------------------------------------------
        float killT = 0f;

        // Ensure we start exactly at death pose
        if (_hasDeathPose)
            spectatorVcam.transform.position = _deathPos;

        while (killT < seconds && killerRoot != null && spectatorVcam != null)
        {
            Vector3 dir = killerRoot.position - spectatorVcam.transform.position;

            // Avoid NaNs if positions match (rare but possible)
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

                // Optional: keep roll at 0 (avoids Dutch/tilt)
                Vector3 e = lookRot.eulerAngles;
                lookRot = Quaternion.Euler(e.x, e.y, 0f);

                spectatorVcam.transform.rotation = lookRot;
            }

            killT += Time.deltaTime;
            yield return null;
        }

        // -----------------------------------------------------
        // 5) If teammate alive (2v2), stay spectating teammate POV
        //    until StopKillcam() is called by RoundEnd/MatchEnd.
        // -----------------------------------------------------
        var teammateCam = ResolveAliveTeammateCameraRoot();
        if (teammateCam != null && spectatorVcam != null)
        {
            _isTeamSpectating = true;
            _teamSpectateTarget = teammateCam;

            Vector3 velocity = Vector3.zero;

            while (_isTeamSpectating && _teamSpectateTarget != null && spectatorVcam != null)
            {
                Transform target = _teamSpectateTarget;

                // Desired third-person position
                Vector3 desiredPos =
                    target.position +
                    target.rotation * teamSpectateOffset;

                // Smooth movement
                spectatorVcam.transform.position =
                    Vector3.Lerp(
                        spectatorVcam.transform.position,
                        desiredPos,
                        Time.deltaTime * teamFollowSmooth);

                // Always look at upper body
                Vector3 lookPoint = target.position + Vector3.up * 1.6f;
                spectatorVcam.transform.rotation =
                    Quaternion.LookRotation(lookPoint - spectatorVcam.transform.position);

                yield return null;
            }

            _followRoutine = null;
            yield break;
        }

        // -----------------------------------------------------
        // 6) Otherwise end killcam normally
        // -----------------------------------------------------
        StopKillcam();
        _followRoutine = null;
    }

    // =====================================================
    // Helpers
    // =====================================================
    private void CacheLocalPlayerVcamIfNeeded()
    {
        if (_localPlayerVcam != null) return;

        var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer == null) return;

        _localPlayerVcam = localPlayer.GetComponentInChildren<CinemachineCamera>(true);
    }

    private Transform ResolveKillerCameraRoot(ulong killerClientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        if (nm.ConnectedClients.TryGetValue(killerClientId, out var client) && client.PlayerObject != null)
        {
            // ✅ Don’t rely on LookController (owner-only)
            // Prefer a dedicated spectator target if present, else use player root.
            var target = client.PlayerObject.GetComponentInChildren<LookController>(true);
            if (target != null) return target.transform;

            return client.PlayerObject.transform;
        }

        return null;
    }

    private Transform ResolveAliveTeammateCameraRoot()
    {
        var lobby = Object.FindFirstObjectByType<NetworkLobbyState>();
        if (lobby == null) return null;

        ulong localId = NetworkManager.Singleton.LocalClientId;

        int myTeam = -1;
        for (int i = 0; i < lobby.Players.Count; i++)
        {
            if (lobby.Players[i].clientId == localId)
            {
                myTeam = lobby.Players[i].teamId;
                break;
            }
        }

        if (myTeam < 0) return null;

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            var p = lobby.Players[i];
            if (p.teamId != myTeam) continue;
            if (p.clientId == localId) continue;
            if (!p.isAlive) continue;

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(p.clientId, out var client)
                && client.PlayerObject != null)
            {
                var target = client.PlayerObject.GetComponentInChildren<LookController>(true);
                if (target != null) return target.transform;

                return client.PlayerObject.transform;
            }
        }

        return null;
    }

    private void SetHardCut(bool enable)
    {
        var brains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);

        CinemachineBrain brain = null;
        for (int i = 0; i < brains.Length; i++)
        {
            if (brains[i] != null && brains[i].enabled && brains[i].gameObject.activeInHierarchy)
            {
                brain = brains[i];
                break;
            }
        }

        if (brain == null) return;

        if (enable)
        {
            if (_savedBlendTime < 0f) _savedBlendTime = brain.DefaultBlend.Time;
            var b = brain.DefaultBlend; b.Time = 0f; brain.DefaultBlend = b;
        }
        else
        {
            if (_savedBlendTime >= 0f)
            {
                var b = brain.DefaultBlend; b.Time = _savedBlendTime; brain.DefaultBlend = b;
            }
            _savedBlendTime = -1f;
        }
    }

    public void RestoreBlendAfterKillcam()
    {
        if (forceHardCut) SetHardCut(false);
    }
}
