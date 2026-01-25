using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Struttura con tutti gli input di gameplay che vogliamo mandare al server.
/// Viene serializzata tramite INetworkSerializable per usarla nei ServerRpc.
/// </summary>
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
    public bool Jetpack;

    /// <summary>
    /// Numero di sequenza di questo pacchetto di input. Viene assegnato dal client e incrementato per ogni input inviato. 
    /// Serve per la predizione client‑side e la riconciliazione server.
    /// </summary>
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
        serializer.SerializeValue(ref Jetpack);

        // Serialize the sequence number last so that older clients can safely ignore it
        serializer.SerializeValue(ref Sequence);
    }
}
