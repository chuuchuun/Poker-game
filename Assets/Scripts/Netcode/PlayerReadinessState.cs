using Unity.Netcode;
using System;

[Serializable]
public struct PlayerReadinessState : INetworkSerializable, IEquatable<PlayerReadinessState>
{
    public ulong clientId;
    public bool isReady;

    public PlayerReadinessState(ulong id, bool ready)
    {
        clientId = id;
        isReady = ready;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref isReady);
    }

    public bool Equals(PlayerReadinessState other)
    {
        return clientId == other.clientId && isReady == other.isReady;
    }
}
