using System;
using Unity.Netcode;

public struct PlayerState : INetworkSerializable, IEquatable<PlayerState>
{
    public ulong id;
    public int currentBet;
    public bool hasFolded;
    public int currentBalance;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref currentBet);
        serializer.SerializeValue(ref hasFolded);
        serializer.SerializeValue(ref currentBalance);

    }

    public bool Equals(PlayerState other)
    {
        return id == other.id &&
               currentBet == other.currentBet &&
               hasFolded == other.hasFolded && 
               currentBalance == other.currentBalance;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(id, currentBet, hasFolded, currentBalance);
    }
}
