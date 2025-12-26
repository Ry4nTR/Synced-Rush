using UnityEngine;
using Unity.Netcode;

public class CharacterWeaponHandler : NetworkBehaviour
{
    [SerializeField] private PlayerInputHandler inputHandler;

    private WeaponController weaponController;
    private bool lastAim;
    private bool lastReload;

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (weaponController == null)
        {
            TryAcquireWeapon();
            return;
        }

        // 🔴 FIRE
        if (inputHandler.fire)
        {
            //Debug.Log("[CharacterWeaponHandler] FIRE input detected");
            weaponController.RequestFire();
        }

        // 🔵 AIM
        if (inputHandler.aim != lastAim)
        {
            //Debug.Log($"[CharacterWeaponHandler] AIM = {inputHandler.aim}");
            weaponController.SetAiming(inputHandler.aim);
            lastAim = inputHandler.aim;
        }

        // 🟡 RELOAD (edge-trigger)
        if (inputHandler.reload && !lastReload)
        {
            //Debug.Log("[CharacterWeaponHandler] RELOAD requested");
            weaponController.Reload();
        }

        lastReload = inputHandler.reload;
    }

    private void TryAcquireWeapon()
    {
        weaponController = GetComponentInChildren<WeaponController>(true);
    }
}
