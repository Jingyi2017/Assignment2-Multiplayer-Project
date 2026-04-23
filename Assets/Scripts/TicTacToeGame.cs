using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class TicTacToeGame : NetworkBehaviour
{
    private const string EmptyBoard = "000000000";
    private const ulong UnassignedClientId = ulong.MaxValue;

    public static TicTacToeGame Instance { get; private set; }

    private readonly NetworkVariable<FixedString32Bytes> boardState =
        new(new FixedString32Bytes(EmptyBoard));

    // 0 = X, 1 = O
    private readonly NetworkVariable<int> currentTurn = new(0);

    // -1 = playing, 0 = X wins, 1 = O wins, 2 = draw
    private readonly NetworkVariable<int> gameResult = new(-1);

    private readonly NetworkVariable<ulong> xPlayerId = new(UnassignedClientId);
    private readonly NetworkVariable<ulong> oPlayerId = new(UnassignedClientId);

    public string CurrentBoardString => GetBoardString();
    public int CurrentTurnValue => currentTurn.Value;
    public int GameResultValue => gameResult.Value;
    public bool HasTwoPlayers => oPlayerId.Value != UnassignedClientId;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        boardState.OnValueChanged += OnBoardStateChanged;
        currentTurn.OnValueChanged += OnTurnChanged;
        gameResult.OnValueChanged += OnResultChanged;
        xPlayerId.OnValueChanged += OnPlayersChanged;
        oPlayerId.OnValueChanged += OnPlayersChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            EnsurePlayerSlotsAreCorrect();
        }
    }

    public override void OnNetworkDespawn()
    {
        boardState.OnValueChanged -= OnBoardStateChanged;
        currentTurn.OnValueChanged -= OnTurnChanged;
        gameResult.OnValueChanged -= OnResultChanged;
        xPlayerId.OnValueChanged -= OnPlayersChanged;
        oPlayerId.OnValueChanged -= OnPlayersChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TrySubmitMoveFromLocal(int index)
    {
        if (index < 0 || index >= 9)
        {
            return;
        }

        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsConnectedClient)
        {
            return;
        }

        if (!IsLocalPlayersTurn())
        {
            return;
        }

        SubmitMoveRpc(index);
    }

    public void RequestRestartFromLocal()
    {
        if (GetLocalPlayerMark() == -1)
        {
            return;
        }

        RestartGameRpc();
    }

    public bool CanSelectCell(int index)
    {
        if (index < 0 || index >= 9)
        {
            return false;
        }

        string board = GetBoardString();
        return board[index] == '0' && IsLocalPlayersTurn();
    }

    public bool CanRestart()
    {
        return GetLocalPlayerMark() != -1 && gameResult.Value != -1;
    }

    public string GetCellDisplay(int index)
    {
        if (index < 0 || index >= 9)
        {
            return string.Empty;
        }

        return GetBoardString()[index] switch
        {
            '1' => "X",
            '2' => "O",
            _ => string.Empty
        };
    }

    public string BuildStatusMessage()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            return "Choose Host or Client to begin.";
        }

        int localMark = GetLocalPlayerMark();
        string youAre = localMark switch
        {
            0 => "You are X.",
            1 => "You are O.",
            _ => string.Empty
        };

        if (oPlayerId.Value == UnassignedClientId)
        {
            return string.IsNullOrEmpty(youAre)
                ? "Waiting for players..."
                : $"{youAre} Waiting for the second player to join.";
        }

        if (gameResult.Value == -1)
        {
            string whoseTurn = currentTurn.Value == 0 ? "X" : "O";

            if (localMark == currentTurn.Value)
            {
                return $"{youAre} Your turn ({whoseTurn}).";
            }

            return string.IsNullOrEmpty(youAre)
                ? $"{whoseTurn}'s turn."
                : $"{youAre} Opponent's turn ({whoseTurn}).";
        }

        if (gameResult.Value == 2)
        {
            return $"{youAre} Draw game.".Trim();
        }

        string winner = gameResult.Value == 0 ? "X" : "O";
        if (localMark == gameResult.Value)
        {
            return $"{youAre} You win!";
        }

        if (localMark != -1)
        {
            return $"{youAre} {winner} wins.";
        }

        return $"{winner} wins.";
    }

    [Rpc(SendTo.Server)]
    private void SubmitMoveRpc(int cellIndex, RpcParams rpcParams = default)
    {
        if (cellIndex < 0 || cellIndex >= 9)
        {
            return;
        }

        if (oPlayerId.Value == UnassignedClientId)
        {
            return;
        }

        if (gameResult.Value != -1)
        {
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int senderMark = GetMarkForClient(senderClientId);
        if (senderMark == -1 || senderMark != currentTurn.Value)
        {
            return;
        }

        char[] cells = GetBoardString().ToCharArray();
        if (cells[cellIndex] != '0')
        {
            return;
        }

        cells[cellIndex] = senderMark == 0 ? '1' : '2';
        boardState.Value = new FixedString32Bytes(new string(cells));

        int evaluatedResult = EvaluateBoard(cells);
        if (evaluatedResult == -1)
        {
            currentTurn.Value = 1 - currentTurn.Value;
        }
        else
        {
            gameResult.Value = evaluatedResult;
        }
    }

    [Rpc(SendTo.Server)]
    private void RestartGameRpc(RpcParams rpcParams = default)
    {
        if (oPlayerId.Value == UnassignedClientId)
        {
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (GetMarkForClient(senderClientId) == -1)
        {
            return;
        }

        ResetGameState();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        EnsurePlayerSlotsAreCorrect();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        EnsurePlayerSlotsAreCorrect();
    }

    private void EnsurePlayerSlotsAreCorrect()
    {
        if (NetworkManager == null)
        {
            return;
        }

        ulong serverClientId = NetworkManager.ServerClientId;
        bool serverStillConnected = false;
        for (int i = 0; i < NetworkManager.ConnectedClientsIds.Count; i++)
        {
            if (NetworkManager.ConnectedClientsIds[i] == serverClientId)
            {
                serverStillConnected = true;
                break;
            }
        }

        if (serverStillConnected)
        {
            xPlayerId.Value = serverClientId;
        }
        else if (!IsClientIdConnected(xPlayerId.Value))
        {
            xPlayerId.Value = UnassignedClientId;
        }

        ulong newOPlayerId = UnassignedClientId;
        for (int i = 0; i < NetworkManager.ConnectedClientsIds.Count; i++)
        {
            ulong clientId = NetworkManager.ConnectedClientsIds[i];
            if (clientId != xPlayerId.Value)
            {
                newOPlayerId = clientId;
                break;
            }
        }

        bool oPlayerChanged = oPlayerId.Value != newOPlayerId;
        oPlayerId.Value = newOPlayerId;

        if (oPlayerChanged)
        {
            ResetGameState();
        }
        else if (boardState.Value.ToString().Length == 0)
        {
            ResetGameState();
        }
    }

    private bool IsClientIdConnected(ulong clientId)
    {
        if (clientId == UnassignedClientId || NetworkManager == null)
        {
            return false;
        }

        for (int i = 0; i < NetworkManager.ConnectedClientsIds.Count; i++)
        {
            if (NetworkManager.ConnectedClientsIds[i] == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetGameState()
    {
        boardState.Value = new FixedString32Bytes(EmptyBoard);
        currentTurn.Value = 0;
        gameResult.Value = -1;
    }

    private string GetBoardString()
    {
        string board = boardState.Value.ToString();
        if (string.IsNullOrEmpty(board) || board.Length < 9)
        {
            board = EmptyBoard;
        }

        return board;
    }

    private int EvaluateBoard(char[] cells)
    {
        int[,] winLines =
        {
            { 0, 1, 2 },
            { 3, 4, 5 },
            { 6, 7, 8 },
            { 0, 3, 6 },
            { 1, 4, 7 },
            { 2, 5, 8 },
            { 0, 4, 8 },
            { 2, 4, 6 }
        };

        for (int i = 0; i < winLines.GetLength(0); i++)
        {
            char a = cells[winLines[i, 0]];
            char b = cells[winLines[i, 1]];
            char c = cells[winLines[i, 2]];

            if (a != '0' && a == b && b == c)
            {
                return a == '1' ? 0 : 1;
            }
        }

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == '0')
            {
                return -1;
            }
        }

        return 2;
    }

    private int GetLocalPlayerMark()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsConnectedClient)
        {
            return -1;
        }

        return GetMarkForClient(manager.LocalClientId);
    }

    private int GetMarkForClient(ulong clientId)
    {
        if (clientId == xPlayerId.Value)
        {
            return 0;
        }

        if (clientId == oPlayerId.Value)
        {
            return 1;
        }

        return -1;
    }

    private bool IsLocalPlayersTurn()
    {
        int mark = GetLocalPlayerMark();
        return mark != -1 && oPlayerId.Value != UnassignedClientId && gameResult.Value == -1 && mark == currentTurn.Value;
    }

    private void OnBoardStateChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
    }

    private void OnTurnChanged(int previousValue, int newValue)
    {
    }

    private void OnResultChanged(int previousValue, int newValue)
    {
    }

    private void OnPlayersChanged(ulong previousValue, ulong newValue)
    {
    }
}
