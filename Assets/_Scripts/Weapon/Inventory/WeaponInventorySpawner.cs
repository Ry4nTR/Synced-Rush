using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(WeaponLoadoutState))]
public class WeaponInventorySpawner : NetworkBehaviour
{
    [Header("Available Weapons")]
    [SerializeField] private WeaponDatabase weaponDatabase;

    [Header("Sockets")]
    [SerializeField] private Transform fpsWeaponSocket;
    [SerializeField] private Transform tpsWeaponSocket;

    [Header("Dependencies")]
    [SerializeField] private ClientComponentSwitcher componentSwitcher;

    private WeaponLoadoutState loadoutState;

    private GameObject currentViewModel;
    private GameObject currentWorldModel;

    private bool subscribed;

    private void Awake()
    {
        loadoutState = GetComponent<WeaponLoadoutState>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!subscribed)
        {
            loadoutState.EquippedWeaponId.OnValueChanged += OnWeaponChanged;
            subscribed = true;
        }

        // Spawn current state (likely -1 → no weapon)
        SpawnWeapon(loadoutState.EquippedWeaponId.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (subscribed)
        {
            loadoutState.EquippedWeaponId.OnValueChanged -= OnWeaponChanged;
            subscribed = false;
        }

        Cleanup();
        base.OnNetworkDespawn();
    }

    private void OnWeaponChanged(int oldId, int newId)
    {
        SpawnWeapon(newId);
    }

    private void SpawnWeapon(int weaponId)
    {
        Cleanup();

        if (weaponDatabase == null)
        {
            Debug.LogError("WeaponDatabase is missing.");
            return;
        }

        WeaponData data = null;

        if (weaponId < 0)
        {
            if (weaponDatabase.AllWeapons.Count == 0)
            {
                Debug.LogError("WeaponDatabase is empty.");
                return;
            }

            data = weaponDatabase.AllWeapons[0];
        }
        else
        {
            data = weaponDatabase.GetById(weaponId);

            if (data == null)
            {
                Debug.LogError(
                    $"[WeaponSpawner] Weapon ID {weaponId} not found in WeaponDatabase. " +
                    $"Check IDs and database configuration."
                );
                return;
            }
        }

        // --- Spawn ---
        if (IsOwner)
        {
            currentViewModel = Instantiate(data.viewModelPrefab, fpsWeaponSocket, false);

            var wc = currentViewModel.GetComponent<WeaponController>();
            wc?.Initialize(data);

            var ss = currentViewModel.GetComponent<ShootingSystem>();
            var wh = currentViewModel.GetComponent<WeaponNetworkHandler>();

            componentSwitcher?.RegisterWeapon(wc, ss, wh);
        }
        else
        {
            currentWorldModel = Instantiate(data.worldModelPrefab, tpsWeaponSocket, false);
        }
    }



    private void Cleanup()
    {
        if (currentViewModel) Destroy(currentViewModel);
        if (currentWorldModel) Destroy(currentWorldModel);
        currentViewModel = null;
        currentWorldModel = null;
    }
}
