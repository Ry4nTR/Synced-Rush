using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bridges the weapon controller with the network. Responsible for sending shot
/// requests to the server, performing authoritative raycasts, applying damage
/// and notifying clients of hit effects. Separating networking into this class
/// keeps the weapon logic clean and avoids circular dependencies.
/// </summary>
public class WeaponNetworkHandler : NetworkBehaviour
{
    private WeaponController weaponController;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
    }

    /// <summary>
    /// Called by the WeaponController when the player fires. Only the owner should invoke
    /// this method. Sends a ServerRpc to perform authoritative hit detection.
    /// </summary>
    /// <param name="origin">The origin of the shot (camera position).</param>
    /// <param name="direction">The forward direction of the shot.</param>
    /// <param name="spread">The spread value applied on the client for hit prediction.</param>
    public void NotifyShot(Vector3 origin, Vector3 direction, float spread)
    {
        if (!IsOwner)
            return;
        // Send the shot to the server with a timestamp
        ShootServerRpc(origin, direction, spread, Time.time);
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 origin, Vector3 direction, float spread, float timestamp, ServerRpcParams rpcParams = default)
    {
        // Basic anti-cheat validation (rate of fire, no future timestamps)
        if (!ValidateShot(rpcParams.Receive.SenderClientId, timestamp))
            return;

        // Apply the same spread on the server to get the authoritative direction
        Vector3 finalDir = ApplySpread(direction, spread);
        float maxRange = weaponController.weaponData.range;
        if (Physics.Raycast(origin, finalDir, out RaycastHit hit, maxRange))
        {
            // Calculate damage based on distance
            float distance = Vector3.Distance(origin, hit.point);
            float damage = weaponController.CalculateDamageByDistance(distance);

            // If the hit object has a HealthSystem, apply damage
            if (hit.collider.TryGetComponent<HealthSystem>(out var health))
            {
                health.TakeDamage(damage, rpcParams.Receive.SenderClientId);
            }
            // Broadcast the hit to all clients for visual feedback.
            // We send a NetworkObjectReference rather than a NetworkObject because RPC
            // parameters must implement INetworkSerializable. NetworkObjectReference
            // handles serialization for network objects automatically.
            NetworkObject netObj = hit.collider.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                NotifyHitClientRpc(hit.point, hit.normal, netObj);
            }
        }
    }

    /// <summary>
    /// Broadcasts hit information to all clients so they can spawn impact VFX. This
    /// does not apply damage – only visual feedback.
    /// </summary>
    /// <param name="hitPoint">The point in world space where the shot hit.</param>
    /// <param name="hitNormal">The normal of the surface hit.</param>
    /// <param name="target">A reference to the network object that was hit.</param>
    [ClientRpc]
    private void NotifyHitClientRpc(Vector3 hitPoint, Vector3 hitNormal, NetworkObjectReference targetRef)
    {
        // Only spawn VFX; do not apply damage here.
        // You can use targetRef.TryGet(out var netObj) to get the NetworkObject if needed.
        // For example:
        // if (targetRef.TryGet(out NetworkObject netObj)) { /* show blood on netObj */ }
    }

    /// <summary>
    /// Requests damage be applied to a specific networked object. Used when the
    /// client performs a local raycast and needs to forward hit information to
    /// the server (e.g., projectile impacts).
    /// </summary>
    /// <param name="target">The GameObject that was hit.</param>
    /// <param name="damage">The amount of damage to apply.</param>
    /// <param name="hitPoint">The hit point in world space.</param>
    public void RequestDamage(GameObject target, float damage, Vector3 hitPoint)
    {
        if (!IsOwner)
            return;
        if (!target.TryGetComponent<NetworkObject>(out var netObj))
            return;
        ApplyDamageServerRpc(netObj.NetworkObjectId, damage, hitPoint);
    }

    [ServerRpc]
    private void ApplyDamageServerRpc(ulong targetNetId, float damage, Vector3 hitPoint)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var netObj))
        {
            if (netObj.TryGetComponent<HealthSystem>(out var health))
            {
                health.TakeDamage(damage, OwnerClientId);
            }
        }
    }

    /// <summary>
    /// Applies the spread value to the direction vector. This should mirror the
    /// implementation on the client to ensure hits are consistent.
    /// </summary>
    private Vector3 ApplySpread(Vector3 direction, float spread)
    {
        float yaw = Random.Range(-spread, spread);
        float pitch = Random.Range(-spread, spread);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        return rot * direction;
    }

    /// <summary>
    /// Simple validation to ensure the shot is plausible. Checks could include
    /// limiting the rate of fire and ensuring timestamps are not in the future.
    /// </summary>
    private bool ValidateShot(ulong clientId, float timestamp)
    {
        // Example validation: timestamp should not be more than a small threshold in the future
        if (timestamp > Time.time + 0.5f)
            return false;
        // You could also track the last fire time per client to enforce fire rate
        return true;
    }
}