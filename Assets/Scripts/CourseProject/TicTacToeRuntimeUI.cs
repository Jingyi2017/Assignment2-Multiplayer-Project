using Unity.BossRoom.ConnectionManagement;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CourseProject
{
    /// <summary>
    /// Runtime-created UI that turns the uploaded multiplayer sample into a small,
    /// easy-to-test 2-player networked Tic-Tac-Toe project.
    ///
    /// This script intentionally creates the board and the host/client controls in
    /// code so the project can be dropped into the existing sample without fragile
    /// scene or prefab editing.
    /// </summary>
    public class TicTacToeRuntimeUI : MonoBehaviour
    {
        const int k_Port = 7777;

        static bool s_Initialized;

        Text m_ConnectionText;
        Text m_StatusText;
        Text m_AssignmentText;
        Text m_ScoreText;
        InputField m_NameField;
        InputField m_IpField;
        Button m_HostButton;
        Button m_ClientButton;
        Button m_DisconnectButton;
        Button m_ResetButton;
        Button[] m_CellButtons;
        Text[] m_CellTexts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateRuntimeUi()
        {
            if (s_Initialized)
            {
                return;
            }

            s_Initialized = true;
            var go = new GameObject("CourseProject_TicTacToeRuntimeUI");
            DontDestroyOnLoad(go);
            go.AddComponent<TicTacToeRuntimeUI>();
        }

        void Awake()
        {
            EnsureEventSystemExists();
            BuildUi();
        }

        void Update()
        {
            RefreshUi();
        }

        void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(eventSystemObject);
            }
        }

        void BuildUi()
        {
            var canvasObject = new GameObject("TicTacToeCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvasObject.transform, new Vector2(660, 760), new Vector2(0.5f, 0.5f), Vector2.zero);
            panel.color = new Color(0f, 0f, 0f, 0.82f);

            CreateText(panel.transform, "CS596 Multiplayer Tic-Tac-Toe", 28, TextAnchor.UpperCenter, new Vector2(0, -20), new Vector2(620, 40));
            m_ConnectionText = CreateText(panel.transform, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(-300, -70), new Vector2(600, 70));

            m_NameField = CreateInputField(panel.transform, $"Player{Random.Range(100, 999)}", new Vector2(-150, -150), new Vector2(260, 40));
            m_IpField = CreateInputField(panel.transform, "127.0.0.1", new Vector2(150, -150), new Vector2(260, 40));
            CreateText(panel.transform, "Name", 16, TextAnchor.MiddleLeft, new Vector2(-255, -120), new Vector2(80, 25));
            CreateText(panel.transform, "Host IP", 16, TextAnchor.MiddleLeft, new Vector2(45, -120), new Vector2(80, 25));

            m_HostButton = CreateButton(panel.transform, "Start Host", new Vector2(-210, -205), new Vector2(150, 42), OnStartHostClicked);
            m_ClientButton = CreateButton(panel.transform, "Start Client", new Vector2(-30, -205), new Vector2(150, 42), OnStartClientClicked);
            m_DisconnectButton = CreateButton(panel.transform, "Disconnect", new Vector2(150, -205), new Vector2(150, 42), OnDisconnectClicked);
            m_ResetButton = CreateButton(panel.transform, "Reset Round", new Vector2(0, 275), new Vector2(180, 42), OnResetClicked);

            m_AssignmentText = CreateText(panel.transform, string.Empty, 18, TextAnchor.MiddleCenter, new Vector2(0, -260), new Vector2(620, 30));
            m_StatusText = CreateText(panel.transform, string.Empty, 20, TextAnchor.MiddleCenter, new Vector2(0, -295), new Vector2(620, 60));
            m_ScoreText = CreateText(panel.transform, string.Empty, 20, TextAnchor.MiddleCenter, new Vector2(0, 235), new Vector2(620, 30));

            m_CellButtons = new Button[9];
            m_CellTexts = new Text[9];
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    float x = -130 + (col * 130);
                    float y = 80 - (row * 130);
                    var button = CreateButton(panel.transform, string.Empty, new Vector2(x, y), new Vector2(110, 110), () => OnBoardCellClicked(index));
                    var text = button.GetComponentInChildren<Text>();
                    text.fontSize = 44;
                    text.alignment = TextAnchor.MiddleCenter;
                    m_CellButtons[index] = button;
                    m_CellTexts[index] = text;
                }
            }
        }

        void RefreshUi()
        {
            var networkManager = NetworkManager.Singleton;
            var connectionManager = FindFirstObjectByType<ConnectionManager>();
            var localPlayer = GetLocalPersistentPlayer();
            var boardAuthority = GetBoardAuthorityPersistentPlayer();

            bool connected = networkManager != null && networkManager.IsListening && (networkManager.IsClient || networkManager.IsServer);
            bool enoughPlayers = boardAuthority != null && boardAuthority.TicTacToeXPlayerId.Value != ulong.MaxValue && boardAuthority.TicTacToeOPlayerId.Value != ulong.MaxValue;

            if (networkManager == null)
            {
                m_ConnectionText.text = "NetworkManager not found in scene yet.";
            }
            else if (!connected)
            {
                m_ConnectionText.text = "Not connected. Use Start Host on one instance and Start Client on the second instance.\nUse 127.0.0.1 for same-machine testing or the host machine\'s LAN IP for two devices.";
            }
            else
            {
                string role = networkManager.IsHost ? "Host" : (networkManager.IsServer ? "Server" : "Client");
                m_ConnectionText.text = $"Connected as {role}. Local ClientId: {networkManager.LocalClientId}. ServerClientId: {NetworkManager.ServerClientId}.\nUse 127.0.0.1 for same-machine testing or the host machine\'s LAN IP for two devices.";
            }

            if (boardAuthority == null)
            {
                m_AssignmentText.text = connected ? "Waiting for the authoritative board object to spawn..." : "Connect two players. X and O are assigned automatically.";
                m_StatusText.text = connected ? "Once both players are connected, the shared board will activate." : "Server-authoritative moves are sent through ServerRpc calls.";
                m_ScoreText.text = "Scoreboard: X 0 | O 0 | Draws 0";
                for (int i = 0; i < 9; i++)
                {
                    m_CellTexts[i].text = string.Empty;
                    m_CellButtons[i].interactable = false;
                }
            }
            else
            {
                string board = boardAuthority.TicTacToeBoard.Value.ToString();
                if (board.Length != 9)
                {
                    board = ".........";
                }

                for (int i = 0; i < 9; i++)
                {
                    char c = board[i];
                    m_CellTexts[i].text = c == '.' ? string.Empty : c.ToString();

                    bool myTurn = localPlayer != null && boardAuthority.TicTacToeCurrentTurnPlayerId.Value == localPlayer.OwnerClientId;
                    bool openCell = c == '.';
                    bool roundInProgress = boardAuthority.TicTacToeRoundState.Value == 1;
                    m_CellButtons[i].interactable = connected && enoughPlayers && myTurn && openCell && roundInProgress;
                }

                string localMark = GetPlayerMark(localPlayer, boardAuthority);
                m_AssignmentText.text = localPlayer == null
                    ? "Connected. Waiting for your player object..."
                    : $"You are Player {networkManager.LocalClientId} ({localMark}). X={FormatPlayer(boardAuthority.TicTacToeXPlayerId.Value)} | O={FormatPlayer(boardAuthority.TicTacToeOPlayerId.Value)}";

                m_StatusText.text = boardAuthority.TicTacToeStatus.Value.ToString();
                m_ScoreText.text = $"Scoreboard: X {boardAuthority.TicTacToeXWins.Value} | O {boardAuthority.TicTacToeOWins.Value} | Draws {boardAuthority.TicTacToeDraws.Value}";
            }

            m_HostButton.interactable = connectionManager != null && (networkManager == null || !networkManager.IsListening);
            m_ClientButton.interactable = connectionManager != null && (networkManager == null || !networkManager.IsListening);
            m_DisconnectButton.interactable = connectionManager != null && connected;
            m_ResetButton.interactable = connected && enoughPlayers && localPlayer != null && boardAuthority != null && boardAuthority.TicTacToeRoundState.Value != 1;
        }

        void OnStartHostClicked()
        {
            var connectionManager = FindFirstObjectByType<ConnectionManager>();
            if (connectionManager == null)
            {
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(m_NameField.text) ? "HostPlayer" : m_NameField.text.Trim();
            string ip = string.IsNullOrWhiteSpace(m_IpField.text) ? "127.0.0.1" : m_IpField.text.Trim();
            connectionManager.StartHostIp(playerName, ip, k_Port);
        }

        void OnStartClientClicked()
        {
            var connectionManager = FindFirstObjectByType<ConnectionManager>();
            if (connectionManager == null)
            {
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(m_NameField.text) ? "ClientPlayer" : m_NameField.text.Trim();
            string ip = string.IsNullOrWhiteSpace(m_IpField.text) ? "127.0.0.1" : m_IpField.text.Trim();
            connectionManager.StartClientIp(playerName, ip, k_Port);
        }

        void OnDisconnectClicked()
        {
            var connectionManager = FindFirstObjectByType<ConnectionManager>();
            if (connectionManager != null)
            {
                connectionManager.RequestShutdown();
            }
        }

        void OnResetClicked()
        {
            var localPlayer = GetLocalPersistentPlayer();
            if (localPlayer != null)
            {
                localPlayer.RequestTicTacToeResetServerRpc();
            }
        }

        void OnBoardCellClicked(int boardIndex)
        {
            var localPlayer = GetLocalPersistentPlayer();
            if (localPlayer != null)
            {
                localPlayer.RequestTicTacToeMoveServerRpc(boardIndex);
            }
        }

        PersistentPlayer GetLocalPersistentPlayer()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return null;
            }

            foreach (var player in FindObjectsByType<PersistentPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsSpawned && player.OwnerClientId == networkManager.LocalClientId)
                {
                    return player;
                }
            }

            return null;
        }

        PersistentPlayer GetBoardAuthorityPersistentPlayer()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return null;
            }

            foreach (var player in FindObjectsByType<PersistentPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsSpawned && player.OwnerClientId == NetworkManager.ServerClientId)
                {
                    return player;
                }
            }

            return null;
        }

        string GetPlayerMark(PersistentPlayer localPlayer, PersistentPlayer boardAuthority)
        {
            if (localPlayer == null || boardAuthority == null)
            {
                return "Unassigned";
            }

            if (localPlayer.OwnerClientId == boardAuthority.TicTacToeXPlayerId.Value)
            {
                return "X";
            }

            if (localPlayer.OwnerClientId == boardAuthority.TicTacToeOPlayerId.Value)
            {
                return "O";
            }

            return "Spectator";
        }

        string FormatPlayer(ulong clientId)
        {
            return clientId == ulong.MaxValue ? "Waiting" : $"Player {clientId}";
        }

        Image CreatePanel(Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPosition)
        {
            var image = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return image;
        }

        Text CreateText(Transform parent, string value, int fontSize, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.text = value;
            return text;
        }

        InputField CreateInputField(Transform parent, string defaultValue, Vector2 anchoredPosition, Vector2 size)
        {
            var root = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

            var text = CreateText(root.transform, defaultValue, 18, TextAnchor.MiddleLeft, Vector2.zero, size - new Vector2(20f, 8f));
            text.color = Color.black;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10, 4);
            text.rectTransform.offsetMax = new Vector2(-10, -4);

            var placeholder = CreateText(root.transform, string.Empty, 18, TextAnchor.MiddleLeft, Vector2.zero, size - new Vector2(20f, 8f));
            placeholder.color = new Color(0f, 0f, 0f, 0.35f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(10, 4);
            placeholder.rectTransform.offsetMax = new Vector2(-10, -4);

            var input = root.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;
            return input;
        }

        Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.78f, 0.95f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = CreateText(buttonObject.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, size);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}
