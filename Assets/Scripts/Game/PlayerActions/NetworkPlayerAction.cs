using System;
using Unity.Netcode;

public struct NetworkPlayerAction : INetworkSerializable
{
    public IPlayerAction Value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            // Deserialize the type identifier
            int typeId = 0;
            serializer.SerializeValue(ref typeId);

            // Create the appropriate action type
            Value = typeId switch
            {
                0 => new SkipAction(0),  // 0 = SkipAction
                // Add other action types here
                _ => throw new ArgumentException($"Unknown action type: {typeId}")
            };

            // Deserialize the action data
            Value.NetworkSerialize(serializer);
        }
        else
        {
            // Serialize the type identifier
            int typeId = Value.TypeId;
            serializer.SerializeValue(ref typeId);

            // Serialize the action data
            Value.NetworkSerialize(serializer);
        }
    }
}