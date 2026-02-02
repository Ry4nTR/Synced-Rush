using UnityEngine;
using Unity.Netcode;

public class WeaponActionHandler : NetworkBehaviour
{
    [SerializeField] private PlayerInputHandler inputHandler;

    private WeaponController weaponController;
    private bool lastAim;
    private float nextLogTime;
    private int lastReloadCount = -1;

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

        if (ConsumePress(ref lastReloadCount, inputHandler.ReloadCount))
        {
            weaponController.Reload();
        }
    }

    private void TryAcquireWeapon()
    {
        weaponController = GetComponentInChildren<WeaponController>(true);

        if (weaponController == null && Time.time >= nextLogTime)
        {
            //Debug.Log($"[WeaponActionHandler] No WeaponController found in children yet (player={name})", this);
            nextLogTime = Time.time + 1f;
        }
    }

    private static bool ConsumePress(ref int lastCount, int currentCount)
    {
        if (lastCount < 0)
        {
            lastCount = currentCount;
            return false;
        }

        bool pressed = currentCount > lastCount;
        if (currentCount != lastCount)
            lastCount = currentCount;

        return pressed;
    }
}
 