using System;
using Unity.Netcode;
using UnityEngine;

public class SessionServices : NetworkBehaviour
{
    public static SessionServices Current { get; private set; }
    public static event Action<SessionServices> OnReady;

    [Header("Session (network spawned)")]
    [SerializeField] public NetworkLobbyState LobbyState;
    [SerializeField] public LobbyManager LobbyManager;
    [SerializeField] public RoundManager RoundManager;

    private void Awake()
    {
        if (LobbyState == null) LobbyState = GetComponent<NetworkLobbyState>();
        if (LobbyManager == null) LobbyManager = GetComponent<LobbyManager>();
        if (RoundManager == null) RoundManager = GetComponent<RoundManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (Current != null && Current != this)
            Debug.LogWarning("[SessionServices] Duplicate SessionServices spawned. Replacing Current.");

        Current = this;
        OnReady?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        if (Current == this) Current = null;
    }
}