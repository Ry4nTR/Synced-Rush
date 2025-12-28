using Unity.Netcode;
using UnityEngine;

public class WeaponNetworkHandler : NetworkBehaviour
{
    private WeaponController weaponController;

    public override void OnNetworkSpawn()
    {
        weaponController = GetComponentInChildren<WeaponController>();
    }

    /* ============================================================
     *  CLIENT → SERVER ENTRY POINT
     * ============================================================ */

    /// <summary>
    /// Called by WeaponController on the owning client.
    /// </summary>
    public void NotifyShot(Vector3 origin, Vector3 direction, float spread)
    {
        if (!IsOwner)
            return;

        ShootServerRpc(origin, direction, spread);
    }   

    /* ============================================================
     *  SERVER-AUTHORITATIVE FIRE
     * ============================================================ */

    [ServerRpc]
    private void ShootServerRpc(
    Vector3 origin,
    Vector3 direction,
    float spread,
    ServerRpcParams rpcParams = default)
    {
        WeaponData data = weaponController.weaponData;
        if (data == null)
            return;

        // 1) Server-side spread correction
        Vector3 correctedDirection = ApplySpread(direction, spread);

        // 2) Validate shot (anti-cheat / sanity checks)
        if (!ValidateShot(origin, correctedDirection))
            return;

        // 3) Server raycast (authoritative)
        if (Physics.Raycast(
            origin,
            correctedDirection,
            out RaycastHit hit,
            data.range,
            data.layerMask,
            QueryTriggerInteraction.Collide))
        {
            Debug.Log(
                $"[SERVER] Raycast HIT | Collider={hit.collider.name} " +
                $"Layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}"
            );

            if (hit.collider.TryGetComponent(out Hitbox hitbox))
            {
                HealthSystem health = hitbox.GetHealthSystem();
                if (health == null)
                    return;

                // ─────────────────────────────────────────────
                // DAMAGE CALCULATION (SERVER AUTHORITATIVE)
                // ─────────────────────────────────────────────

                float distance = Vector3.Distance(origin, hit.point);

                // Base damage with falloff
                float damage = weaponController.CalculateDamageByDistance(distance);

                // Apply hitbox multiplier (head, chest, etc.)
                damage *= hitbox.damageMultiplier;

                health.TakeDamage(damage, rpcParams.Receive.SenderClientId);

                // 4) Notify all clients of confirmed hit
                NotifyHitClientRpc(
                    hit.point,
                    hit.normal,
                    rpcParams.Receive.SenderClientId);
            }
        }
    }

    /* ============================================================
     *  SERVER-SIDE HELPERS (UNCHANGED ARCHITECTURE)
     * ============================================================ */

    private Vector3 ApplySpread(Vector3 direction, float spread)
    {
        if (spread <= 0f)
            return direction;

        float x = Random.Range(-spread, spread);
        float y = Random.Range(-spread, spread);

        Vector3 spreadOffset = new Vector3(x, y, 0f);
        return (Quaternion.Euler(spreadOffset) * direction).normalized;
    }

    private bool ValidateShot(Vector3 origin, Vector3 direction)
    {
        // Direction sanity (normalized)
        if (direction.sqrMagnitude < 0.9f)
            return false;

        // VERY LOOSE origin check (camera-based weapons)
        float maxAllowedDistance = 5f; // NOT 2.5f
        if (Vector3.Distance(origin, transform.position) > maxAllowedDistance)
            return false;

        return true;
    }


    /* ============================================================
     *  SERVER → CLIENT FEEDBACK
     * ============================================================ */

    [ClientRpc]
    private void NotifyHitClientRpc(Vector3 hitPoint, Vector3 hitNormal, ulong instigatorClientId) { }
}
