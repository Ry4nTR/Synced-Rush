using Unity.Netcode;
using UnityEngine;

public class WeaponNetworkHandler : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private WeaponLoadoutState loadoutState;

    private ShootingSystem currentShootingSystem;

    // ========================================================================
    // 1. INITIALIZATION
    // ========================================================================
    public void UpdateWeaponReferences(ShootingSystem newShootingSystem)
    {
        currentShootingSystem = newShootingSystem;
    }

    // ========================================================================
    // 2. CLIENT: SHOOTING LOGIC
    // ========================================================================
    public void NotifyShot(Vector3 origin, Vector3 direction)
    {
        if (!IsOwner) return;

        WeaponData data = GetCurrentWeaponData();
        if (data == null) return;

        // Piccolo push in avanti per evitare hit immediati se l’origine è molto vicina ai collider
        origin += direction * 0.05f;

        // RaycastAll per saltare eventuali self-colliders e prendere il primo hit valido
        var hits = Physics.RaycastAll(origin, direction, data.range, data.layerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            HandleMiss(origin, direction, data.range);
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var col = h.collider;
            if (col == null) continue;

            // Self filtering
            if (col.TryGetComponent(out Hitbox hb))
            {
                if (hb.OwnerNetworkId == NetworkObjectId)
                    continue;
            }
            else
            {
                if (col.transform.IsChildOf(transform))
                    continue;
            }

            HandleHit(h, origin, direction);
            return;
        }

        // Only self hits -> miss
        HandleMiss(origin, direction, data.range);
    }

    private bool IsSelfCollider(Collider col)
    {
        if (col == null) return false;

        // WeaponNetworkHandler è sul player: tutto ciò che è child di questo transform è "self"
        return col.transform.IsChildOf(transform);
    }

    private void HandleHit(RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        // Did we hit a Player Hitbox?
        if (hit.collider.TryGetComponent(out Hitbox hitbox))
        {
            // Valid Target: Request Damage
            ReportHitServerRpc(hitbox.OwnerNetworkId, hit.point, origin, direction);
        }
        else
        {
            // Wall/Environment: Just show effects
            NotifyMissServerRpc(hit.point, origin);
        }
    }

    private void HandleMiss(Vector3 origin, Vector3 direction, float range)
    {
        // Sky/Nothing: Show tracer to max range
        Vector3 endPoint = origin + (direction * range);
        NotifyMissServerRpc(endPoint, origin);
    }


    // ========================================================================
    // 3. SERVER: VALIDATION & DAMAGE
    // ========================================================================
    [ServerRpc]
    private void ReportHitServerRpc(ulong targetNetId, Vector3 hitPoint, Vector3 origin, Vector3 direction)
    {
        // Se target è il network object che possiede questo handler, è self-hit → ignora
        if (targetNetId == NetworkObjectId)
            return;

        WeaponData data = GetCurrentWeaponData();

        if (!GetTarget(targetNetId, out HealthSystem targetHealth)) return;
        if (!IsValidHit(origin, hitPoint, direction, data)) return;

        float distance = Vector3.Distance(origin, hitPoint);
        float damage = data.CalculateDamageByDistance(distance);
        targetHealth.TakeDamage(damage, OwnerClientId);

        NotifyHitClientRpc(hitPoint, Vector3.up, OwnerClientId);
    }

    [ServerRpc]
    private void NotifyMissServerRpc(Vector3 endPoint, Vector3 origin)
    {
        NotifyMissClientRpc(endPoint, origin, OwnerClientId);
    }

    // --- Validation Helper ---
    private bool IsValidHit(Vector3 origin, Vector3 hitPoint, Vector3 direction, WeaponData data)
    {
        float distance = Vector3.Distance(origin, hitPoint);

        // A. Range Check (+ buffer for lag)
        if (distance > data.range + 2.0f)
        {
            Debug.LogWarning($"[Anti-Cheat] Range violation: {OwnerClientId}");
            return false;
        }

        // B. Wall Check (Obstruction)
        int obstructionMask = LayerMask.GetMask("Default", "Ground", "Walls");
        if (Physics.Raycast(origin, direction, out RaycastHit wallHit, distance - 0.5f, obstructionMask))
        {
            Debug.LogWarning($"[Anti-Cheat] Wall violation: {OwnerClientId}");
            return false;
        }

        return true;
    }


    // ========================================================================
    // 4. CLIENTS: VISUAL REPLICATION
    // ========================================================================
    [ClientRpc]
    private void NotifyHitClientRpc(Vector3 hitPoint, Vector3 normal, ulong shooterId)
    {
        // Don't play effects for the shooter (they already saw them instantly)
        if (NetworkManager.Singleton.LocalClientId == shooterId) return;

        //currentShootingSystem?.ShowImpactEffect(hitPoint, normal);
    }

    [ClientRpc]
    private void NotifyMissClientRpc(Vector3 endPoint, Vector3 origin, ulong shooterId)
    {
        if (NetworkManager.Singleton.LocalClientId == shooterId) return;

        //currentShootingSystem?.ShowBulletTracer(origin, endPoint);
    }

    // ========================================================================
    // 5. HELPERS
    // ========================================================================
    private WeaponData GetCurrentWeaponData()
    {
        int id = loadoutState.EquippedWeaponId.Value;
        return weaponDatabase.GetDataById(id);
    }

    private bool GetTarget(ulong netId, out HealthSystem health)
    {
        health = null;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj))
        {
            return obj.TryGetComponent(out health);
        }
        return false;
    }
}