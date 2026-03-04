using SyncedRush.Gamemode;
using SyncedRush.Generics;
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

            // 1. Check if it's a Hitbox
            if (col.TryGetComponent(out Hitbox hb))
            {
                if (hb.OwnerNetworkId == NetworkObjectId)
                    continue;

                if (GetTarget(hb.OwnerNetworkId, out HealthSystem targetHealth) && GetTarget(NetworkObjectId, out HealthSystem myHealth))
                {
                    if (myHealth.playerTeam.Value == targetHealth.playerTeam.Value && myHealth.playerTeam.Value != Team.None)
                        continue; // Pass through teammates
                }

                HandleHit(h, origin, direction);
                return;
            }

            // 2. Ignore our own body
            if (col.transform.IsChildOf(transform))
                continue;

            // === FIX: Ignore other players' movement capsules! ===
            // This ensures bullets pass through the invisible capsule and hit the bone hitboxes inside.
            if (col.GetComponentInParent<HealthSystem>() != null)
                continue;

            // 3. Must be a wall or the ground
            HandleHit(h, origin, direction);
            return;
        }

        HandleMiss(origin, direction, data.range);
    }

    private void HandleHit(RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        if (hit.collider.TryGetComponent(out Hitbox hitbox))
        {
            ReportHitServerRpc(hitbox.OwnerNetworkId, origin, direction, hit.point, hitbox.bodyPart);
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
    // ========================================================================
    // SERVER: VALIDATION & DAMAGE
    // ========================================================================
    [ServerRpc]
    private void ReportHitServerRpc(ulong targetNetId, Vector3 origin, Vector3 direction, Vector3 hitPoint, BodyPartType bodyPart)
    {
        var rm = SessionServices.Current != null ? SessionServices.Current.RoundManager : FindFirstObjectByType<RoundManager>();
        if (rm == null || rm.CurrentFlowState.Value != MatchFlowState.InRound)
            return;

        if (targetNetId == NetworkObjectId)
            return;

        WeaponData data = GetCurrentWeaponData();
        if (data == null) return;

        if (!GetTarget(targetNetId, out HealthSystem targetHealth)) return;

        if (GetTarget(NetworkObjectId, out HealthSystem shooterHealth))
        {
            if (shooterHealth.playerTeam.Value == targetHealth.playerTeam.Value && targetHealth.playerTeam.Value != Team.None)
            {
                return; // Friendly fire blocked!
            }
        }

        // 1. VERIFY NO WALLS: Check if the bullet passed through environment geometry
        if (!IsValidHit(origin, hitPoint, direction, data))
            return;

        // 2. VERIFY PROXIMITY: Ensure the target is actually near the hit point (anti-cheat/lag tolerance)
        // A generous 3.5 units accounts for player latency and animation offsets
        if (Vector3.Distance(hitPoint, targetHealth.transform.position) > 3.5f)
            return;

        // 3. APPLY DAMAGE: Find the multiplier for the body part the client hit
        float multiplier = 1f;
        var hitboxes = targetHealth.GetComponentsInChildren<Hitbox>();
        foreach (var hb in hitboxes)
        {
            if (hb.bodyPart == bodyPart)
            {
                multiplier = hb.damageMultiplier;
                break;
            }
        }

        bool isHeadshot = bodyPart == BodyPartType.Head;

        float distance = Vector3.Distance(origin, hitPoint);
        float damage = data.CalculateDamageByDistance(distance);

        damage *= Mathf.Max(0f, multiplier);

        float preHealth = targetHealth.currentHealth.Value;
        bool willKill = preHealth > 0f && (preHealth - damage) <= 0f;

        targetHealth.TakeDamage(damage, OwnerClientId);

        var shooterOnly = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        HitmarkerClientRpc(willKill, isHeadshot, shooterOnly);
    }

    private bool TryResolveAuthoritativeHit(
            Vector3 origin,
            Vector3 direction,
            WeaponData data,
            ulong expectedTargetId,
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

            if (h.collider.TryGetComponent(out Hitbox hb))
            {
                if (hb.OwnerNetworkId == NetworkObjectId)
                    continue;

                // === FIX: Overlap resolution ===
                // Only accept the hitbox if it belongs to the exact player the client shot at.
                // If the ray hits a different player's overlapping arm, it passes right through!
                if (hb.OwnerNetworkId == expectedTargetId)
                {
                    bestHit = h;
                    bestHitbox = hb;
                    return true;
                }

                continue;
            }

            // === FIX: Ignore player capsules on the server too ===
            if (h.collider.GetComponentInParent<HealthSystem>() != null)
                continue;

            // Hit a wall, block the shot.
            return false;
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
        AudioManager.Instance.PlaySFX(SoundID.HITMARKER, Vector3.zero);
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
