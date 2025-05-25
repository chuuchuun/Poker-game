using System;
using Unity.Netcode;

public struct NetworkPlayerAction : INetworkSerializable
{
    public IPlayerAction Value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            // Read the TypeId
            int typeId = 0;
            serializer.SerializeValue(ref typeId);

            // Create the correct action instance
            Value = typeId switch
            {
                0 => new SkipAction(0),
                1 => new FoldAction(),
                2 => new CallAction(),
                3 => new CheckAction(),
                4 => new RaiseAction(),
                5 => new ReRaiseAction(),
                _ => throw new ArgumentException($"Unknown action type: {typeId}")
            };

            // Deserialize the inner action data
            Value.NetworkSerialize(serializer);
        }
        else
        {
            // Write the TypeId
            int typeId = Value.TypeId;
            serializer.SerializeValue(ref typeId);

            // Serialize the inner action data
            Value.NetworkSerialize(serializer);
        }
    }
}
