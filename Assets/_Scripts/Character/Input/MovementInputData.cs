using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Struttura con tutti gli input di gameplay che vogliamo mandare al server.
/// Viene serializzata tramite INetworkSerializable per usarla nei ServerRpc.
/// </summary>
public struct MovementInputData : INetworkSerializable
{
    public Vector2 Move;
    public Vector2 Look;
    public bool Jump;
    public bool Sprint;
    public bool Crouch;
    public bool Fire;
    public bool Aim;
    public float Scroll;

    // opzionale: se vuoi replicare anche il debug reset pos
    public bool DebugResetPos;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref Look);
        serializer.SerializeValue(ref Jump);
        serializer.SerializeValue(ref Sprint);
        serializer.SerializeValue(ref Crouch);
        serializer.SerializeValue(ref Fire);
        serializer.SerializeValue(ref Aim);
        serializer.SerializeValue(ref Scroll);
        serializer.SerializeValue(ref DebugResetPos); // opzionale
    }
}
