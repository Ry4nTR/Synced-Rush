using System;
using Unity.Collections;
using Unity.Netcode;

public struct NetLobbyPlayer : INetworkSerializable, IEquatable<NetLobbyPlayer>
{
    public ulong clientId;
    public FixedString32Bytes name;
    public bool isReady;
    public bool isHost;
    public int teamId;
    public bool isAlive;

    public bool Equals(NetLobbyPlayer other)
    {
        return clientId == other.clientId &&
               name.Equals(other.name) &&
               isReady == other.isReady &&
               isHost == other.isHost &&
               teamId == other.teamId &&
               isAlive == other.isAlive;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref isReady);
        serializer.SerializeValue(ref isHost);
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref isAlive);
    }
}