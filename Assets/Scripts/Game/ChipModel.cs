using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ChipColor
{
    black,
    red,
    green,
    blue
}
public class ChipModel : NetworkBehaviour
{
    public int value;
    public ChipColor color;
    public int chipId;
    public NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>();

    public NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    public NetworkVariable<ulong> parentNetworkId = new NetworkVariable<ulong>();
    public NetworkVariable<int> stackPosition = new NetworkVariable<int>(-1);

}
