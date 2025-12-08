using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawns one weapon for each player and attaches it
/// in FIRST PERSON for the owner,
/// and THIRD PERSON for all other clients.
/// </summary>
public class WeaponLoadoutManager : NetworkBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject weaponPrefab;      // Prefab containing WeaponController, Shooting, NetworkHandler
    public WeaponData weaponData;        // The SO defining stats

    private void Start()
    {
        if (IsServer)
        {
            // Subscribe to client joins
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        SpawnWeaponFor(clientId);
    }

    private void SpawnWeaponFor(ulong clientId)
    {
        // 1. SERVER spawns the weapon
        GameObject weaponInstance = Instantiate(weaponPrefab);
        NetworkObject netObj = weaponInstance.GetComponent<NetworkObject>();

        // Give ownership to the player who owns this weapon
        netObj.SpawnAsPlayerObject(clientId, true);

        // 2. Initialize the weapon controller (server + owner)
        var controller = weaponInstance.GetComponent<WeaponController>();
        controller.Initialize(weaponData);
    }
}
