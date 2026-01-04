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

        if (inputHandler.Fire)
        {
            //Debug.Log("[WeaponActionHandler] FIRE input detected");
            weaponController.RequestFire();
        }

        if (inputHandler.Aim != lastAim)
        {
            //Debug.Log($"[WeaponActionHandler] AIM = {inputHandler.aim}");
            weaponController.SetAiming(inputHandler.Aim);
            lastAim = inputHandler.Aim;
        }

        if (inputHandler.Reload && !lastReload)
        {
            //Debug.Log("[WeaponActionHandler] RELOAD requested");
            weaponController.Reload();
        }

        lastReload = inputHandler.Reload;
    }

    private void TryAcquireWeapon()
    {
        weaponController = GetComponentInChildren<WeaponController>(true);
    }
}
