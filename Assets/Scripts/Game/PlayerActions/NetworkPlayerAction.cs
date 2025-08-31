using Unity.Netcode;

public struct NetworkPlayerAction : INetworkSerializable
{
    public int ActionType;
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
            0 => new SkipAction(0),
            1 => new FoldAction(BetAmount),
            2 => new CallAction(BetAmount), 
            3 => new CheckAction(BetAmount),
            4 => new RaiseAction(BetAmount),
            5 => new ReRaiseAction(BetAmount),
            _ => new SkipAction(0)
        };
    }
}