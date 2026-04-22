using System;
using System.Collections.Generic;
using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Utils;
using Unity.Collections;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// NetworkBehaviour that represents a player connection and is the "Default Player Prefab" inside Netcode for
    /// GameObjects' (Netcode) NetworkManager. This NetworkBehaviour will contain several other NetworkBehaviours that
    /// should persist throughout the duration of this connection, meaning it will persist between scenes.
    /// </summary>
    /// <remarks>
    /// It is not necessary to explicitly mark this as a DontDestroyOnLoad object as Netcode will handle migrating this
    /// Player object between scene loads.
    /// </remarks>
    [RequireComponent(typeof(NetworkObject))]
    public class PersistentPlayer : NetworkBehaviour
    {
        const ulong k_UnassignedClientId = ulong.MaxValue;
        const string k_EmptyBoard = ".........";

        [SerializeField]
        PersistentPlayerRuntimeCollection m_PersistentPlayerRuntimeCollection;

        [SerializeField]
        NetworkNameState m_NetworkNameState;

        [SerializeField]
        NetworkAvatarGuidState m_NetworkAvatarGuidState;

        public NetworkNameState NetworkNameState => m_NetworkNameState;

        public NetworkAvatarGuidState NetworkAvatarGuidState => m_NetworkAvatarGuidState;

        /// <summary>
        /// Shared team score from the earlier project revision. Server-write / everyone-read.
        /// </summary>
        public NetworkVariable<int> TeamScore { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // ---------------------------------------------------------------------
        // Tic-Tac-Toe state. The host player's PersistentPlayer acts as the
        // board authority, and the server is the only writer for all values.
        // ---------------------------------------------------------------------
        public NetworkVariable<FixedString32Bytes> TicTacToeBoard { get; } = new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes(k_EmptyBoard),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<FixedString128Bytes> TicTacToeStatus { get; } = new NetworkVariable<FixedString128Bytes>(
            new FixedString128Bytes("Connect two players to begin."),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<ulong> TicTacToeXPlayerId { get; } = new NetworkVariable<ulong>(
            k_UnassignedClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<ulong> TicTacToeOPlayerId { get; } = new NetworkVariable<ulong>(
            k_UnassignedClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<ulong> TicTacToeCurrentTurnPlayerId { get; } = new NetworkVariable<ulong>(
            k_UnassignedClientId,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> TicTacToeRoundState { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> TicTacToeXWins { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> TicTacToeOWins { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> TicTacToeDraws { get; } = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsTicTacToeBoardAuthority => IsSpawned && NetworkManager != null && OwnerClientId == NetworkManager.ServerClientId;

        public override void OnNetworkSpawn()
        {
            gameObject.name = "PersistentPlayer" + OwnerClientId;

            m_PersistentPlayerRuntimeCollection.Add(this);
            if (IsServer)
            {
                TeamScore.Value = 0;

                var sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(OwnerClientId);
                if (sessionPlayerData.HasValue)
                {
                    var playerData = sessionPlayerData.Value;
                    m_NetworkNameState.Name.Value = playerData.PlayerName;
                    if (playerData.HasCharacterSpawned)
                    {
                        m_NetworkAvatarGuidState.AvatarGuid.Value = playerData.AvatarNetworkGuid;
                    }
                    else
                    {
                        m_NetworkAvatarGuidState.SetRandomAvatar();
                        playerData.AvatarNetworkGuid = m_NetworkAvatarGuidState.AvatarGuid.Value;
                        SessionManager<SessionPlayerData>.Instance.SetPlayerData(OwnerClientId, playerData);
                    }
                }

                if (IsTicTacToeBoardAuthority)
                {
                    InitializeTicTacToeState();
                }

                RefreshTicTacToeAssignmentsOnServer();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            RemovePersistentPlayer();
        }

        public override void OnNetworkDespawn()
        {
            RemovePersistentPlayer();
        }

        void RemovePersistentPlayer()
        {
            bool wasServer = IsServer;
            m_PersistentPlayerRuntimeCollection.Remove(this);
            if (wasServer)
            {
                var sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(OwnerClientId);
                if (sessionPlayerData.HasValue)
                {
                    var playerData = sessionPlayerData.Value;
                    playerData.PlayerName = m_NetworkNameState.Name.Value;
                    playerData.AvatarNetworkGuid = m_NetworkAvatarGuidState.AvatarGuid.Value;
                    SessionManager<SessionPlayerData>.Instance.SetPlayerData(OwnerClientId, playerData);
                }

                RefreshTicTacToeAssignmentsOnServer();
            }
        }

        /// <summary>
        /// Client-to-server move request. This is a ServerRpc because each player
        /// asks the authoritative server to validate and apply their turn.
        /// </summary>
        [ServerRpc]
        public void RequestTicTacToeMoveServerRpc(int boardIndex)
        {
            var boardAuthority = GetTicTacToeBoardAuthority();
            if (boardAuthority == null)
            {
                return;
            }

            boardAuthority.ApplyTicTacToeMove(OwnerClientId, boardIndex);
        }

        /// <summary>
        /// Allows either player to request a new round after a win/draw.
        /// </summary>
        [ServerRpc]
        public void RequestTicTacToeResetServerRpc()
        {
            var boardAuthority = GetTicTacToeBoardAuthority();
            if (boardAuthority == null)
            {
                return;
            }

            boardAuthority.RefreshTicTacToeAssignmentsOnServer(resetBoard: true);
        }

        void InitializeTicTacToeState()
        {
            TicTacToeBoard.Value = new FixedString32Bytes(k_EmptyBoard);
            TicTacToeStatus.Value = new FixedString128Bytes("Connect two players to begin.");
            TicTacToeXPlayerId.Value = k_UnassignedClientId;
            TicTacToeOPlayerId.Value = k_UnassignedClientId;
            TicTacToeCurrentTurnPlayerId.Value = k_UnassignedClientId;
            TicTacToeRoundState.Value = 0;
            TicTacToeXWins.Value = 0;
            TicTacToeOWins.Value = 0;
            TicTacToeDraws.Value = 0;
        }

        PersistentPlayer GetTicTacToeBoardAuthority()
        {
            foreach (var player in m_PersistentPlayerRuntimeCollection.Items)
            {
                if (player != null && player.IsSpawned && player.OwnerClientId == NetworkManager.ServerClientId)
                {
                    return player;
                }
            }

            return null;
        }

        void RefreshTicTacToeAssignmentsOnServer(bool resetBoard = false)
        {
            if (!IsServer)
            {
                return;
            }

            var boardAuthority = GetTicTacToeBoardAuthority();
            if (boardAuthority == null)
            {
                return;
            }

            if (!boardAuthority.IsTicTacToeBoardAuthority)
            {
                boardAuthority.RefreshTicTacToeAssignmentsOnServer(resetBoard);
                return;
            }

            var sortedPlayers = new List<PersistentPlayer>();
            foreach (var player in m_PersistentPlayerRuntimeCollection.Items)
            {
                if (player != null && player.IsSpawned)
                {
                    sortedPlayers.Add(player);
                }
            }
            sortedPlayers.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

            boardAuthority.TicTacToeXPlayerId.Value = sortedPlayers.Count > 0 ? sortedPlayers[0].OwnerClientId : k_UnassignedClientId;
            boardAuthority.TicTacToeOPlayerId.Value = sortedPlayers.Count > 1 ? sortedPlayers[1].OwnerClientId : k_UnassignedClientId;

            if (sortedPlayers.Count < 2)
            {
                boardAuthority.TicTacToeRoundState.Value = 0;
                boardAuthority.TicTacToeBoard.Value = new FixedString32Bytes(k_EmptyBoard);
                boardAuthority.TicTacToeCurrentTurnPlayerId.Value = boardAuthority.TicTacToeXPlayerId.Value;
                boardAuthority.TicTacToeStatus.Value = new FixedString128Bytes("Waiting for 2 players...");
                return;
            }

            if (resetBoard || boardAuthority.TicTacToeRoundState.Value == 0)
            {
                boardAuthority.StartNewTicTacToeRound();
            }
        }

        void StartNewTicTacToeRound()
        {
            TicTacToeBoard.Value = new FixedString32Bytes(k_EmptyBoard);
            TicTacToeRoundState.Value = 1;
            TicTacToeCurrentTurnPlayerId.Value = TicTacToeXPlayerId.Value;
            TicTacToeStatus.Value = new FixedString128Bytes($"{GetPlayerLabel(TicTacToeXPlayerId.Value)} is X and goes first.");
        }

        void ApplyTicTacToeMove(ulong requestingClientId, int boardIndex)
        {
            if (!IsServer || !IsTicTacToeBoardAuthority)
            {
                return;
            }

            if (TicTacToeRoundState.Value != 1)
            {
                return;
            }

            if (boardIndex < 0 || boardIndex >= 9)
            {
                return;
            }

            if (TicTacToeCurrentTurnPlayerId.Value != requestingClientId)
            {
                TicTacToeStatus.Value = new FixedString128Bytes($"It is {GetPlayerLabel(TicTacToeCurrentTurnPlayerId.Value)}'s turn.");
                return;
            }

            string board = TicTacToeBoard.Value.ToString();
            if (board.Length != 9)
            {
                board = k_EmptyBoard;
            }

            if (board[boardIndex] != '.')
            {
                return;
            }

            char mark = requestingClientId == TicTacToeXPlayerId.Value ? 'X' : 'O';
            char[] boardChars = board.ToCharArray();
            boardChars[boardIndex] = mark;
            string updatedBoard = new string(boardChars);
            TicTacToeBoard.Value = new FixedString32Bytes(updatedBoard);

            if (HasWinningLine(updatedBoard, mark))
            {
                TicTacToeRoundState.Value = 2;
                if (mark == 'X')
                {
                    TicTacToeXWins.Value++;
                }
                else
                {
                    TicTacToeOWins.Value++;
                }

                TicTacToeStatus.Value = new FixedString128Bytes($"{GetPlayerLabel(requestingClientId)} wins this round as {mark}! Press Reset Round.");
                return;
            }

            if (!updatedBoard.Contains("."))
            {
                TicTacToeRoundState.Value = 2;
                TicTacToeDraws.Value++;
                TicTacToeStatus.Value = new FixedString128Bytes("Draw! Press Reset Round to play again.");
                return;
            }

            ulong nextPlayerId = mark == 'X' ? TicTacToeOPlayerId.Value : TicTacToeXPlayerId.Value;
            TicTacToeCurrentTurnPlayerId.Value = nextPlayerId;
            TicTacToeStatus.Value = new FixedString128Bytes($"{GetPlayerLabel(nextPlayerId)}'s turn ({(mark == 'X' ? 'O' : 'X')}).");
        }

        bool HasWinningLine(string board, char mark)
        {
            return
                (board[0] == mark && board[1] == mark && board[2] == mark) ||
                (board[3] == mark && board[4] == mark && board[5] == mark) ||
                (board[6] == mark && board[7] == mark && board[8] == mark) ||
                (board[0] == mark && board[3] == mark && board[6] == mark) ||
                (board[1] == mark && board[4] == mark && board[7] == mark) ||
                (board[2] == mark && board[5] == mark && board[8] == mark) ||
                (board[0] == mark && board[4] == mark && board[8] == mark) ||
                (board[2] == mark && board[4] == mark && board[6] == mark);
        }

        string GetPlayerLabel(ulong clientId)
        {
            if (clientId == k_UnassignedClientId)
            {
                return "Nobody";
            }

            foreach (var player in m_PersistentPlayerRuntimeCollection.Items)
            {
                if (player != null && player.OwnerClientId == clientId)
                {
                    string playerName = player.NetworkNameState != null ? player.NetworkNameState.Name.Value.ToString() : string.Empty;
                    if (!string.IsNullOrWhiteSpace(playerName))
                    {
                        return playerName;
                    }

                    return $"Player {clientId}";
                }
            }

            return $"Player {clientId}";
        }
    }
}
