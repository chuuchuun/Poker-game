using Unity.Netcode;

public struct NetworkPlayerAction : INetworkSerializable
{
    public ActionType ActionType;
    public int BetAmount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ActionType);
        serializer.SerializeValue(ref BetAmount);
    }

    public IPlayerAction ToAction()
    {
        return ActionType switch
        {
            ActionType.SKIP => new SkipAction(0),
            ActionType.FOLD => new FoldAction(BetAmount),
            ActionType.CALL => new CallAction(BetAmount),
            ActionType.CHECK => new CheckAction(BetAmount),
            ActionType.RAISE => new RaiseAction(BetAmount),
            ActionType.RERAISE => new ReRaiseAction(BetAmount),
            _ => new SkipAction(0)
        };
    }
}