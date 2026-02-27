using SyncedRush.Gamemode;
using Unity.Netcode;
using UnityEngine;

public class WeaponNetworkHandler : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private WeaponLoadoutState loadoutState;

    private ClientSystems _clientSystems;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
            _clientSystems = FindFirstObjectByType<ClientSystems>();
    }

    // ========================================================================
    // CLIENT: SHOOTING LOGIC
    // ========================================================================
    public void NotifyShot(Vector3 origin, Vector3 direction)
    {
        if (!IsOwner) return;

        WeaponData data = GetCurrentWeaponData();
        if (data == null) return;

        origin += direction * 0.05f;

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

            // self filter
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

        HandleMiss(origin, direction, data.range);
    }

    private void HandleHit(RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        if (hit.collider.TryGetComponent(out Hitbox hitbox))
        {
            ReportHitServerRpc(hitbox.OwnerNetworkId, origin, direction);
        }
        else
        {
            NotifyMissServerRpc(hit.point, origin);
        }
    }

    private void HandleMiss(Vector3 origin, Vector3 direction, float range)
    {
        Vector3 endPoint = origin + (direction * range);
        NotifyMissServerRpc(endPoint, origin);
    }

    // ========================================================================
    // SERVER: VALIDATION & DAMAGE (authoritative headshot + kill)
    // ========================================================================
    [ServerRpc]
    private void ReportHitServerRpc(ulong targetNetId, Vector3 origin, Vector3 direction)
    {
        var rm = SessionServices.Current != null ? SessionServices.Current.RoundManager : FindFirstObjectByType<RoundManager>();
        if (rm == null || rm.CurrentFlowState.Value != MatchFlowState.InRound)
            return;

        if (targetNetId == NetworkObjectId)
            return;

        WeaponData data = GetCurrentWeaponData();
        if (data == null) return;

        if (!GetTarget(targetNetId, out HealthSystem targetHealth)) return;

        // Server performs the authoritative raycast to see what was ACTUALLY hit
        if (!TryResolveAuthoritativeHit(origin, direction, data, out RaycastHit hit, out Hitbox hitbox))
            return;

        // Ensure the hitbox belongs to the claimed target
        if (hitbox.OwnerNetworkId != targetNetId)
            return;

        // Basic anti-cheat validation (range + wall)
        if (!IsValidHit(origin, hit.point, direction, data))
            return;

        bool isHeadshot = hitbox.bodyPart == BodyPartType.Head;

        float distance = Vector3.Distance(origin, hit.point);
        float damage = data.CalculateDamageByDistance(distance);

        // Apply per-hitbox multiplier (this is why hitbox exists)
        damage *= Mathf.Max(0f, hitbox.damageMultiplier);

        // Determine kill BEFORE applying damage (clean & deterministic)
        float preHealth = targetHealth.currentHealth.Value;
        bool willKill = preHealth > 0f && (preHealth - damage) <= 0f;

        targetHealth.TakeDamage(damage, OwnerClientId);

        // Shooter-only hitmarker
        var shooterOnly = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        HitmarkerClientRpc(willKill, isHeadshot, shooterOnly);

        // Optional: replicate world VFX for others
        // NotifyHitClientRpc(hit.point, hit.normal, OwnerClientId);
    }

    private bool TryResolveAuthoritativeHit(
        Vector3 origin,
        Vector3 direction,
        WeaponData data,
        out RaycastHit bestHit,
        out Hitbox bestHitbox)
    {
        bestHit = default;
        bestHitbox = null;

        var hits = Physics.RaycastAll(origin, direction, data.range, data.layerMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            if (!h.collider.TryGetComponent(out Hitbox hb))
                continue; // for hitmarker we only accept hitboxes as "valid player hits"

            // ignore self
            if (hb.OwnerNetworkId == NetworkObjectId)
                continue;

            bestHit = h;
            bestHitbox = hb;
            return true;
        }

        return false;
    }

    [ServerRpc]
    private void NotifyMissServerRpc(Vector3 endPoint, Vector3 origin)
    {
        NotifyMissClientRpc(endPoint, origin, OwnerClientId);
    }

    private bool IsValidHit(Vector3 origin, Vector3 hitPoint, Vector3 direction, WeaponData data)
    {
        float distance = Vector3.Distance(origin, hitPoint);

        if (distance > data.range + 2.0f)
            return false;

        int obstructionMask = LayerMask.GetMask("Default", "Ground", "Walls");
        if (Physics.Raycast(origin, direction, out RaycastHit wallHit, distance - 0.5f, obstructionMask))
            return false;

        return true;
    }

    // ========================================================================
    // CLIENT: shooter-only UI
    // ========================================================================
    [ClientRpc]
    private void HitmarkerClientRpc(bool isKill, bool isHeadshot, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        if (_clientSystems == null || _clientSystems.UI == null) return;

        _clientSystems.UI.PlayHitmarker(isKill, isHeadshot);
    }

    [ClientRpc]
    private void NotifyMissClientRpc(Vector3 endPoint, Vector3 origin, ulong shooterId)
    {
        if (NetworkManager.Singleton.LocalClientId == shooterId) return;
        // tracer for others...
    }

    // ========================================================================
    // HELPERS
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
            return obj.TryGetComponent(out health);
        return false;
    }
}
