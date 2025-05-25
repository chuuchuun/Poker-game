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

public class FoldAction : IPlayerAction
{
    private int currentBet;

    public int TypeId => 1;
    public int NewBet => currentBet;
    public bool HasFolded => true;

    public FoldAction() { }
    public FoldAction(int currentBet)
    {
        this.currentBet = currentBet;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentBet);
    }
}

public class CallAction : IPlayerAction
{
    private int callAmount;

    public int TypeId => 2;
    public int NewBet => callAmount;
    public bool HasFolded => false;

    public CallAction() { }
    public CallAction(int callAmount)
    {
        this.callAmount = callAmount;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref callAmount);
    }
}

public class CheckAction : IPlayerAction
{
    private int currentBet;

    public int TypeId => 3;
    public int NewBet => currentBet;
    public bool HasFolded => false;

    public CheckAction() { }
    public CheckAction(int currentBet)
    {
        this.currentBet = currentBet;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentBet);
    }
}

public class RaiseAction : IPlayerAction
{
    private int raiseTo;

    public int TypeId => 4;
    public int NewBet => raiseTo;
    public bool HasFolded => false;

    public RaiseAction() { }
    public RaiseAction(int raiseTo)
    {
        this.raiseTo = raiseTo;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref raiseTo);
    }
}

public class ReRaiseAction : IPlayerAction
{
    private int reRaiseTo;

    public int TypeId => 5;
    public int NewBet => reRaiseTo;
    public bool HasFolded => false;

    public ReRaiseAction() { }
    public ReRaiseAction(int reRaiseTo)
    {
        this.reRaiseTo = reRaiseTo;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref reRaiseTo);
    }
}
