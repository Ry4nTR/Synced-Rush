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

    /// <summary>
    /// Numero di sequenza di questo pacchetto di input. Viene assegnato
    /// dal client e incrementato per ogni input inviato. Serve per la
    /// predizione client‑side e la riconciliazione server: il server può
    /// comunicare quale sequenza ha processato e il client può scartare
    /// gli input confermati e mantenere quelli ancora da elaborare.
    /// Il valore predefinito è zero quando non inizializzato.
    /// </summary>
    public int Sequence;

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
        // Serialize the sequence number last so that older clients can safely ignore it
        serializer.SerializeValue(ref Sequence);
    }
}
