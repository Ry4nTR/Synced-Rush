using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-tick data sent from client to server to reproduce the client-side simulation.
/// Contains raw inputs, view info, ability context, and networking metadata.
/// </summary>
public struct SimulationTickData : INetworkSerializable
{
    // -------------------------
    // Raw input / logical edges
    // -------------------------
    public int AbilityCount;
    public int JumpCount;
    public int ReloadCount;

    public Vector2 Move;
    public Vector2 Look;

    // -------------------------
    // View / aim
    // -------------------------
    public float AimYaw;
    public float AimPitch;

    // -------------------------
    // Ability context (not pure input)
    // -------------------------
    public Vector3 GrappleOrigin;
    public bool RequestDetach;

    // -------------------------
    // Other buttons / stateful inputs
    // -------------------------
    public bool Sprint;
    public bool Crouch;
    public bool Fire;
    public bool Aim;

    public bool JetHeld;
    public int JetpackCount;

    // -------------------------
    // Networking
    // -------------------------
    public int Sequence;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // Raw counters
        serializer.SerializeValue(ref AbilityCount);
        serializer.SerializeValue(ref JumpCount);
        serializer.SerializeValue(ref ReloadCount);

        // Movement axes + look
        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref Look);
        serializer.SerializeValue(ref AimYaw);
        serializer.SerializeValue(ref AimPitch);

        // Ability context
        serializer.SerializeValue(ref GrappleOrigin);
        serializer.SerializeValue(ref RequestDetach);

        // Buttons
        serializer.SerializeValue(ref Sprint);
        serializer.SerializeValue(ref Crouch);
        serializer.SerializeValue(ref Fire);
        serializer.SerializeValue(ref Aim);

        serializer.SerializeValue(ref JetHeld);
        serializer.SerializeValue(ref JetpackCount);

        // Networking
        serializer.SerializeValue(ref Sequence);
    }
}
