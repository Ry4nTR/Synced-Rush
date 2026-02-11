using System.Collections;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class DeathCamController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera spectatorVcam;
    [SerializeField] private int boostAbovePlayer = 100;
    [SerializeField] private bool forceHardCut = true;

    private CinemachineCamera _localPlayerVcam;
    private int _spectatorBasePriority;
    private Coroutine _followRoutine;

    private Vector3 _deathPos;
    private Quaternion _deathRot;
    private bool _hasDeathPose;

    private float _savedBlendTime = -1f;

    private void Awake()
    {
        if (spectatorVcam != null)
            _spectatorBasePriority = spectatorVcam.Priority;
    }

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

        CacheLocalPlayerVcamIfNeeded();

        Transform killerRoot = ResolveKillerCameraRoot(killerClientId);
        if (killerRoot == null) return;

        StopKillcam();

        spectatorVcam.Follow = null;
        spectatorVcam.LookAt = null;

        // set transform BEFORE priority change (prevents slide)
        if (_hasDeathPose)
            spectatorVcam.transform.SetPositionAndRotation(_deathPos, _deathRot);

        if (forceHardCut) SetHardCut(true);

        int playerPri = _localPlayerVcam != null ? _localPlayerVcam.Priority : 0;
        spectatorVcam.Priority = playerPri + boostAbovePlayer;

        _followRoutine = StartCoroutine(FollowRoutine(killerRoot, seconds));
    }

    public void StopKillcam()
    {
        if (_followRoutine != null)
        {
            StopCoroutine(_followRoutine);
            _followRoutine = null;
        }

        if (spectatorVcam != null)
        {
            spectatorVcam.Priority = _spectatorBasePriority;
            spectatorVcam.Follow = null;
            spectatorVcam.LookAt = null;
        }

        if (forceHardCut) SetHardCut(false);

        _hasDeathPose = false;
    }

    private IEnumerator FollowRoutine(Transform target, float seconds)
    {
        float t = 0f;
        while (t < seconds && target != null && spectatorVcam != null)
        {
            spectatorVcam.transform.SetPositionAndRotation(target.position, target.rotation);
            t += Time.deltaTime;
            yield return null;
        }
        _followRoutine = null;
    }

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
            var look = client.PlayerObject.GetComponentInChildren<LookController>(true);
            return look != null ? look.CameraTransform : null;
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
}
