using Unity.Netcode;
using UnityEngine;

public enum BodyPartType { Head, Chest, Arms, Hands, Legs, Feet }

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    public BodyPartType bodyPart;
    public float damageMultiplier = 1f;

    private HealthSystem healthSystem;
    private NetworkObject parentNetworkObject;

    private void Awake()
    {
        healthSystem = GetComponentInParent<HealthSystem>();
        parentNetworkObject = GetComponentInParent<NetworkObject>();
    }

    public HealthSystem GetHealthSystem() => healthSystem;

    // Helper to get the ID of the player this hitbox belongs to
    public ulong OwnerNetworkId
    {
        get
        {
            if (parentNetworkObject != null) return parentNetworkObject.NetworkObjectId;
            return 0;
        }
    }
}