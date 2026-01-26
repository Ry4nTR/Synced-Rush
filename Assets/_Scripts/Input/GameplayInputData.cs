using Unity.Netcode;
using UnityEngine;

public struct GameplayInputData : INetworkSerializable
{
    public int AbilityCount;
    public int JumpCount;
    public int ReloadCount;

    public Vector2 Move;
    public Vector2 Look;
    public float AimYaw;
    public float AimPitch;

    public bool Sprint;
    public bool Crouch;
    public bool Fire;
    public bool Aim;

    // Jetpack
    public bool JetHeld;       // HELD state (space currently down)
    public int JetpackCount;   // EDGE counter (increments on press)

    public int Sequence;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AbilityCount);
        serializer.SerializeValue(ref JumpCount);
        serializer.SerializeValue(ref ReloadCount);

        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref Look);
        serializer.SerializeValue(ref AimYaw);
        serializer.SerializeValue(ref AimPitch);

        serializer.SerializeValue(ref Sprint);
        serializer.SerializeValue(ref Crouch);
        serializer.SerializeValue(ref Fire);
        serializer.SerializeValue(ref Aim);

        serializer.SerializeValue(ref JetHeld);
        serializer.SerializeValue(ref JetpackCount);

        serializer.SerializeValue(ref Sequence);
    }
}
