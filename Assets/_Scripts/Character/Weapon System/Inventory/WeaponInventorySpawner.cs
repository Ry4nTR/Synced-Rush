using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Spawns and manages weapon models based on the equipped weapon.
/// </summary>
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
    [SerializeField] private PlayerAnimationController animationController;

    private WeaponLoadoutState loadoutState;

    private GameObject currentWeapon;
    private GameObject weaponWorldModel;

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

    //Spawning weapon
    private void SpawnWeapon(int weaponId)
    {
        if (weaponId < 0) return;

        Cleanup();

        WeaponData data = weaponDatabase.GetDataById(weaponId);
        if (data == null)
        {
            Debug.LogError($"[WeaponSpawner] Weapon ID {weaponId} not found.");
            return;
        }

        Animator animator = animationController.GetComponent<Animator>();

        animationController.Equip();
        animationController?.SetWeaponAnimations(data);

        SpawnModels(data);
    }

    //Spawning weapon models
    private void SpawnModels(WeaponData data)
    {
        if (IsOwner)
        {
            currentWeapon = Instantiate(data.weaponPrefab, fpsWeaponSocket, false);

            var wc = currentWeapon.GetComponent<WeaponController>();
            wc?.Initialize(data);

            var ss = currentWeapon.GetComponent<ShootingSystem>();

            var wh = currentWeapon.GetComponentInParent<WeaponNetworkHandler>();

            componentSwitcher?.RegisterWeapon(wc, ss, wh);

            Animator weaponAnimator = currentWeapon.GetComponent<Animator>();
            animationController.SetWeaponAnimation(data, weaponAnimator);
        }
        else
        {
            weaponWorldModel = Instantiate(data.worldModelPrefab, tpsWeaponSocket, false);
        }
    }

    //Cleanup existing weapon
    private void Cleanup()
    {
        if (currentWeapon) Destroy(currentWeapon);
        if (weaponWorldModel) Destroy(weaponWorldModel);
        currentWeapon = null;
        weaponWorldModel = null;
    }
}
