using Unity.Netcode;

public interface IPlayerAction : INetworkSerializable
{
    int NewBet { get; }
    bool HasFolded { get; }
    int TypeId { get; }
}

public class SkipAction : IPlayerAction
{
    private int currentBet;
    public int TypeId => 0;

    public SkipAction(int currentBet)
    {
        this.currentBet = currentBet;
    }

    public int NewBet => currentBet;
    public bool HasFolded => false;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    { 
        serializer.SerializeValue(ref currentBet);
    }
}