using Unity.Netcode;
using UnityEngine;

public struct ServerSnapshot : INetworkSerializable
{
    public bool Valid;

    public Vector3 Position;
    public Vector2 HorizontalVel;
    public float VerticalVel;
    public float Yaw;

    public int LastProcessedSequence;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Valid);

        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref HorizontalVel);
        serializer.SerializeValue(ref VerticalVel);
        serializer.SerializeValue(ref Yaw);

        serializer.SerializeValue(ref LastProcessedSequence);
    }
}