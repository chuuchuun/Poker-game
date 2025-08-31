using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class RoundModel : NetworkBehaviour
{
    private QueueControlBehavior queueControlBehavior => GameManager.Instance.GetComponent<QueueControlBehavior>();
    private DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();
    private BettingControlBehavior bettingController => GameManager.Instance.GetComponent<BettingControlBehavior>();

    private List<PlayerController> playerModels = new List<PlayerController>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[RoundModel] OnNetworkSpawn | ObjId={NetworkObjectId} | IsServer={IsServer} | IsOwner={IsOwner}");
    }

    private void Awake()
    {
        UpdatePlayers();
    }

    private void Start()
    {
        Debug.Log($"[RoundModel] Pre-RPC check | IsSpawned={NetworkObject.IsSpawned} | ObjId={NetworkObjectId} | IsServer={IsServer} | IsOwner={IsOwner}");
    }

    public int GetCurrentHighestBet() => bettingController.GetCurrentHighestBet();

    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        deckControlBehavior.InitializeDeckAndSlots();
        UpdatePlayers();
        deckControlBehavior.DealCards(playerModels);

        queueControlBehavior.SetFirstPlayerToMove(firstPlayerId);
        queueControlBehavior.SetPlayers(playerIds.ToList());

        bettingController.InitializeBetting(playerModels);
    }

    private void UpdatePlayers()
    {
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + player.name);
            }
        }
    }

    public void TryToMakePlayerAction(IPlayerAction action)
    {
        Debug.Log($"[RoundModel] TryToMakePlayerAction | IsSpawned={IsSpawned} | IsServer={IsServer}");

        if (IsServer)
        {
            ProcessPlayerAction(NetworkManager.Singleton.LocalClientId, action);
        }
        else
        {
            var wrapper = new NetworkPlayerAction
            {
                ActionType = action.TypeId,
                BetAmount = action.NewBet
            };
            SubmitPlayerActionServerRpc(wrapper);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerActionServerRpc(NetworkPlayerAction networkAction, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        IPlayerAction action = networkAction.ToAction();

        Debug.Log($"[SERVER] Received action type {networkAction.ActionType} with bet {networkAction.BetAmount} from {senderId}");
        Debug.Log($"[SERVER] Converted to: {action.GetType().Name}");

        ProcessPlayerAction(senderId, action);
        PlayerActionProcessedClientRpc(senderId, networkAction.ActionType, networkAction.BetAmount, networkAction);
    }

    [ClientRpc]
    private void PlayerActionProcessedClientRpc(ulong playerId, int actionType, int betAmount, NetworkPlayerAction networkAction)
    {
        Debug.Log($"[CLIENT] Player {playerId} made action={actionType}, bet={betAmount}");
        IPlayerAction action = networkAction.ToAction();
        ProcessPlayerAction(playerId, action);
    }

    private void ProcessPlayerAction(ulong playerId, IPlayerAction action)
    {
        Debug.Log($"[RoundModel] Processing {action.GetType().Name} for player {playerId}");

        if (!queueControlBehavior.ShouldAcceptActionFromPlayerWithId(playerId)) return;

        var player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId)?.GetComponent<PlayerController>();
        if (player == null) return;

        bool actionSuccessful = bettingController.ProcessPlayerAction(playerId, action, player);

        if (actionSuccessful)
        {
            Debug.Log($"Current bank is {bettingController.GetCurrentBank()}");
            queueControlBehavior.SwitchTurnToNextPlayer();
            UpdatePlayersState();
        }
    }

    private void UpdatePlayersState()
    {
        if (!IsServer) return;

        foreach (var player in playerModels)
        {
            bettingController.UpdatePlayerState(player);
        }
    }
}