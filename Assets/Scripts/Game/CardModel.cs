using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
[System.Serializable]
public class CardModel : NetworkBehaviour
{
    public int value;
    public CardSuit suit;
}
