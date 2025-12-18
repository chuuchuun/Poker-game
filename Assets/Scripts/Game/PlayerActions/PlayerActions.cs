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
    IPlayerController Player { get; set; }
}

public class SkipAction : IPlayerAction
{
    private int currentBet;
    private IPlayerController player;

    public ActionType TypeId => ActionType.SKIP;

    public SkipAction(int currentBet, IPlayerController player = null)
    {
        this.currentBet = currentBet;
        this.player = player;
    }

    public int NewBet => currentBet;
    public bool HasFolded => false;

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentBet);
        // Do NOT serialize IPlayerController reference
    }
}

public class FoldAction : IPlayerAction
{
    private int currentBet;
    private IPlayerController player;

    public ActionType TypeId => ActionType.FOLD;
    public int NewBet => currentBet;
    public bool HasFolded => true;

    public FoldAction() { }
    public FoldAction(int currentBet, IPlayerController player = null)
    {
        this.currentBet = currentBet;
        this.player = player;
    }

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentBet);
    }
}

public class CallAction : IPlayerAction
{
    private int callAmount;
    private IPlayerController player;

    public ActionType TypeId => ActionType.CALL;
    public int NewBet => callAmount;
    public bool HasFolded => false;

    public CallAction() { }
    public CallAction(int callAmount, IPlayerController player = null)
    {
        this.callAmount = callAmount;
        this.player = player;
    }

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref callAmount);
    }
}

public class CheckAction : IPlayerAction
{
    private int currentBet;
    private IPlayerController player;

    public ActionType TypeId => ActionType.CHECK;
    public int NewBet => currentBet;
    public bool HasFolded => false;

    public CheckAction() { }
    public CheckAction(int currentBet, IPlayerController player = null)
    {
        this.currentBet = currentBet;
        this.player = player;
    }

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref currentBet);
    }
}

public class RaiseAction : IPlayerAction
{
    private int raiseTo;
    private IPlayerController player;

    public ActionType TypeId => ActionType.RAISE;
    public int NewBet => raiseTo;
    public bool HasFolded => false;

    public RaiseAction() { }
    public RaiseAction(int raiseTo, IPlayerController player = null)
    {
        this.raiseTo = raiseTo;
        this.player = player;
    }

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref raiseTo);
    }
}

public class ReRaiseAction : IPlayerAction
{
    private int reRaiseTo;
    private IPlayerController player;

    public ActionType TypeId => ActionType.RERAISE;
    public int NewBet => reRaiseTo;
    public bool HasFolded => false;

    public ReRaiseAction() { }
    public ReRaiseAction(int reRaiseTo, IPlayerController player = null)
    {
        this.reRaiseTo = reRaiseTo;
        this.player = player;
    }

    public IPlayerController Player
    {
        get => player;
        set => player = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref reRaiseTo);
    }
}
