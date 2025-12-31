using UnityEngine;
using Unity.Netcode;

public class WeaponActionHandler : NetworkBehaviour
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

        if (inputHandler.fire)
        {
            //Debug.Log("[WeaponActionHandler] FIRE input detected");
            weaponController.RequestFire();
        }

        if (inputHandler.aim != lastAim)
        {
            //Debug.Log($"[WeaponActionHandler] AIM = {inputHandler.aim}");
            weaponController.SetAiming(inputHandler.aim);
            lastAim = inputHandler.aim;
        }

        if (inputHandler.reload && !lastReload)
        {
            //Debug.Log("[WeaponActionHandler] RELOAD requested");
            weaponController.Reload();
        }

        lastReload = inputHandler.reload;
    }

    private void TryAcquireWeapon()
    {
        weaponController = GetComponentInChildren<WeaponController>(true);
    }
}
