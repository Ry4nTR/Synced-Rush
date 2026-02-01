using Unity.Netcode;
using UnityEngine;

public enum GrapplePhase : byte
{
    None = 0,
    Shooting = 1,
    Hooked = 2
}

public struct GrappleNetState : INetworkSerializable
{
    public GrapplePhase Phase;

    public Vector3 Origin;
    public Vector3 Direction;     // normalized
    public Vector3 TipPosition;   // while shooting
    public Vector3 HookPoint;     // latched point
    public float CurrentDistance; // along ray

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Phase);

        serializer.SerializeValue(ref Origin);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref TipPosition);
        serializer.SerializeValue(ref HookPoint);
        serializer.SerializeValue(ref CurrentDistance);
    }
}
