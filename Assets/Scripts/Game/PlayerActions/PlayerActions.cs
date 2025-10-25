using Unity.Netcode;

public enum ActionType
{
    SKIP,
    FOLD,
    CALL,
    CHECK,
    RAISE,
    RERAISE
}

public interface IPlayerAction : INetworkSerializable
{
    int NewBet { get; }
    bool HasFolded { get; }
    ActionType TypeId { get; }
}

public class SkipAction : IPlayerAction
{
    private int currentBet;
    public ActionType TypeId => ActionType.SKIP;

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

    public ActionType TypeId => ActionType.FOLD;
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

    public ActionType TypeId => ActionType.CALL;
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

    public ActionType TypeId => ActionType.CHECK;
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

    public ActionType TypeId => ActionType.RAISE;
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

    public ActionType TypeId => ActionType.RERAISE;
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
