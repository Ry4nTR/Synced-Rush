using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages weapon selection, spawning and attachment in a networked context.
/// This component keeps track of which weapon the player has equipped and
/// ensures the correct first-person view model is spawned for the owning
/// client while a simplified world model is spawned for remote clients. It
/// also registers the weapon components with the ClientComponentSwitcher so
/// they can be enabled or disabled based on network authority.
/// </summary>
public class WeaponInventory : NetworkBehaviour
{
    [Header("Available Weapons")]
    [Tooltip("List of weapon data assets that this player can equip. The array index corresponds to the weapon index.")]
    public WeaponData[] weaponDatas;

    [Header("Sockets")]
    [Tooltip("Transform where the first-person weapon view model should attach (e.g. WeaponSocket_FP on the arms prefab).")]
    public Transform fpsWeaponSocket;

    [Tooltip("Transform where the third-person world model should attach (e.g. WeaponSocket on the full body prefab).")]
    public Transform thirdPersonWeaponSocket;

    [Header("Dependencies")]
    [Tooltip("Reference to the ClientComponentSwitcher on this player. Used to register weapon components so they can be toggled based on authority.")]
    public ClientComponentSwitcher componentSwitcher;

    // Network-synchronized index of the currently equipped weapon. The owner has
    // write permission to change the value; all clients have read permission.
    private NetworkVariable<int> equippedWeapon = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Locally spawned view model and world model for the current weapon
    private GameObject currentViewModel;
    private GameObject currentWorldModel;

    /// <summary>
    /// Subscribe to equipped weapon changes when the object spawns. If we own
    /// this character, equip the default weapon (index 0) on spawn.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        equippedWeapon.OnValueChanged += OnEquippedWeaponChanged;
        if (IsServer)
        {
            // set default weapon index after spawn
            int defaultIndex = (weaponDatas != null && weaponDatas.Length > 0) ? 0 : -1;
            equippedWeapon.Value = defaultIndex;
        }
        SpawnLocalRepresentation(equippedWeapon.Value);
    }

    /// <summary>
    /// Called whenever the equipped weapon changes. Spawns or destroys the
    /// appropriate view/world models depending on whether we own this player.
    /// </summary>
    private void OnEquippedWeaponChanged(int oldValue, int newValue)
    {
        SpawnLocalRepresentation(newValue);
    }

    /// <summary>
    /// Requests to equip the weapon at the specified index. This method should
    /// be called by the owning client. It validates the index locally and then
    /// sends a server RPC to update the network variable.
    /// </summary>
    /// <param name="index">The index of the weapon to equip.</param>
    public void EquipWeapon(int index)
    {
        if (!IsOwner)
        {
            // Only the owner can initiate a weapon change
            return;
        }
        if (index < 0 || weaponDatas == null || index >= weaponDatas.Length)
        {
            Debug.LogWarning($"WeaponInventory: Invalid weapon index {index}");
            return;
        }
        // Send the request to the server to update the equipped weapon
        EquipWeaponServerRpc(index);
    }

    /// <summary>
    /// Server RPC that sets the current weapon index. The server is authoritative
    /// over which weapon is equipped and will propagate the change to all clients.
    /// </summary>
    /// <param name="index">The index of the weapon to equip.</param>
    [ServerRpc]
    private void EquipWeaponServerRpc(int index)
    {
        if (index < 0 || weaponDatas == null || index >= weaponDatas.Length)
        {
            return;
        }
        equippedWeapon.Value = index;
    }

    /// <summary>
    /// Spawns the appropriate weapon representation (view model or world model) on
    /// this client based on whether we own the object. Destroys any previous
    /// representations before instantiating new ones.
    /// </summary>
    /// <param name="index">The weapon index to spawn.</param>
    private void SpawnLocalRepresentation(int index)
    {
        // clean up old models
        if (currentViewModel != null) Destroy(currentViewModel);
        if (currentWorldModel != null) Destroy(currentWorldModel);

        if (index < 0 || weaponDatas == null || index >= weaponDatas.Length)
            return;

        var data = weaponDatas[index];
        if (IsOwner)
        {
            //spawn view model
            Debug.Log($"WeaponInventory: Spawning view model for weapon '{data.weaponName}'");
            currentViewModel = Instantiate(data.viewModelPrefab, fpsWeaponSocket, false);
            var wc = currentViewModel.GetComponent<WeaponController>();
            wc?.Initialize(data);
            var ss = currentViewModel.GetComponent<ShootingSystem>();
            var wh = currentViewModel.GetComponent<WeaponNetworkHandler>();
            componentSwitcher?.RegisterWeapon(wc, ss, wh);
        }
        else
        {
            // spawn world model on non‑owners
            Debug.Log($"WeaponInventory: Spawning world model for weapon '{data.weaponName}'");
            currentWorldModel = Instantiate(data.worldModelPrefab, thirdPersonWeaponSocket, false);
        }
    }
}