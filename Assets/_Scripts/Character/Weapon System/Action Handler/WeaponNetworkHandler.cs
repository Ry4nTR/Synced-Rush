using Unity.Netcode;
using UnityEngine;

public class WeaponNetworkHandler : NetworkBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private WeaponDatabase weaponDatabase;

    private WeaponController weaponController;
    private WeaponLoadoutState loadoutState;

    public override void OnNetworkSpawn()
    {
        loadoutState = GetComponent<WeaponLoadoutState>();
        weaponController = GetComponentInChildren<WeaponController>();
    }

    // Reseaves a shooting request from the local player and forwards it to the server.
    public void NotifyShot(Vector3 origin, Vector3 direction, float spread)
    {
        if (!IsOwner)
            return;

        ShootServerRpc(origin, direction, spread);
    }

    // server-side shooting logic
    [ServerRpc]
    private void ShootServerRpc(Vector3 origin, Vector3 direction, float spread, ServerRpcParams rpcParams = default)
    {
        int weaponId = loadoutState.EquippedWeaponId.Value;
        WeaponData data = weaponDatabase.GetDataById(weaponId);

        // 1) Server-side spread correction
        Vector3 correctedDirection = data.ApplySpread(direction, spread);

        // 2) Validate shot (anti-cheat / sanity checks)
        if (!ValidateShot(origin, correctedDirection))
            return;

        ulong shooterClientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(shooterClientId, out var shooterClient))
            return;

        Transform shooterRoot = shooterClient.PlayerObject.transform;

        RaycastHit[] hits = Physics.RaycastAll(
        origin,
        correctedDirection,
        data.range,
        data.layerMask,
        QueryTriggerInteraction.Collide
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // IGNORE SELF (SERVER-SIDE)
            if (hit.collider.transform.IsChildOf(shooterRoot))
                continue;

            // VALID HIT
            if (hit.collider.TryGetComponent(out Hitbox hitbox))
            {
                HealthSystem health = hitbox.GetHealthSystem();
                if (health == null)
                    return;

                float damage = data.CalculateDamageByDistance(hit.distance);
                damage *= hitbox.damageMultiplier;

                health.TakeDamage(damage, shooterClientId);
                NotifyHitClientRpc(hit.point, hit.normal, shooterClientId);
                return;
            }
        }
    }

    // Shoot validation logic
    private bool ValidateShot(Vector3 origin, Vector3 direction)
    {
        // Direction sanity (normalized)
        if (direction.sqrMagnitude < 0.9f)
            return false;

        return true;
    }

   // SERVER → CLIENT FEEDBACK
    [ClientRpc]
    private void NotifyHitClientRpc(Vector3 hitPoint, Vector3 hitNormal, ulong instigatorClientId) { }
}
