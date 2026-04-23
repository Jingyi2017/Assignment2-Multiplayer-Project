using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class TicTacToeBootstrap : MonoBehaviour
{
    private const string DefaultIpAddress = "192.168.0.215";
    private const ushort DefaultPort = 7777;

    private Font uiFont;

    private Canvas canvas;
    private GameObject connectionPanel;
    private InputField ipInput;
    private InputField portInput;
    private Text connectionText;
    private Text statusText;
    private Button hostButton;
    private Button clientButton;
    private Button restartButton;
    private Button[] cellButtons;
    private Text[] cellLabels;

    private NetworkLauncher launcher;
    private GameObject networkPrefabTemplate;

    private void Awake()
    {
        uiFont = LoadDefaultFont();
        EnsureEventSystemExists();
        BuildUi();
        BuildNetworkManager();
        RefreshUi();
    }

    private void Update()
    {
        RefreshUi();
    }

    private void BuildNetworkManager()
    {
        NetworkManager existingManager = FindFirstObjectByType<NetworkManager>();
        if (existingManager != null)
        {
            launcher = existingManager.GetComponent<NetworkLauncher>();
            if (launcher != null)
            {
                launcher.Configure(ipInput, portInput, connectionText, connectionPanel, DefaultIpAddress, DefaultPort);
            }
            return;
        }

        GameObject managerObject = new GameObject("NetworkManager");
        DontDestroyOnLoad(managerObject);

        NetworkManager networkManager = managerObject.AddComponent<NetworkManager>();
        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }

        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.NetworkConfig.ForceSamePrefabs = false;
        networkManager.RunInBackground = true;

        launcher = managerObject.AddComponent<NetworkLauncher>();
        managerObject.AddComponent<TwoPlayerApproval>();

        networkPrefabTemplate = CreateRuntimeNetworkPrefabTemplate();
        networkManager.AddNetworkPrefab(networkPrefabTemplate);

        launcher.Configure(ipInput, portInput, connectionText, connectionPanel, DefaultIpAddress, DefaultPort);
        launcher.SetGameStatePrefabTemplate(networkPrefabTemplate);
    }

    private GameObject CreateRuntimeNetworkPrefabTemplate()
    {
        GameObject prefabTemplate = new GameObject("TicTacToeGamePrefabTemplate");
        prefabTemplate.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(prefabTemplate);

        prefabTemplate.AddComponent<NetworkObject>();
        prefabTemplate.AddComponent<TicTacToeGame>();

        return prefabTemplate;
    }

    private void BuildUi()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Canvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootPanel = CreatePanel(canvas.transform, "RootPanel", new Color(0.12f, 0.15f, 0.2f, 0.92f));
        RectTransform rootRect = rootPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(640f, 920f);

        VerticalLayoutGroup rootLayout = rootPanel.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(30, 30, 30, 30);
        rootLayout.spacing = 18f;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.childControlHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        CreateText(rootPanel.transform, "TitleText", "LAN Tic-Tac-Toe", 34, TextAnchor.MiddleCenter, FontStyle.Bold, 60f);

        connectionPanel = CreatePanel(rootPanel.transform, "ConnectionPanel", new Color(0.18f, 0.22f, 0.28f, 0.96f));
        VerticalLayoutGroup connectionLayout = connectionPanel.AddComponent<VerticalLayoutGroup>();
        connectionLayout.padding = new RectOffset(20, 20, 20, 20);
        connectionLayout.spacing = 12f;
        connectionLayout.childAlignment = TextAnchor.UpperCenter;
        connectionLayout.childControlHeight = false;
        connectionLayout.childControlWidth = true;
        connectionLayout.childForceExpandHeight = false;
        connectionLayout.childForceExpandWidth = true;
        AddLayoutElement(connectionPanel, 0f, 280f);

        CreateText(connectionPanel.transform, "IpLabel", "IP Address", 20, TextAnchor.MiddleLeft, FontStyle.Bold, 28f);
        ipInput = CreateInputField(connectionPanel.transform, "IpInput", DefaultIpAddress, false);

        CreateText(connectionPanel.transform, "PortLabel", "Port", 20, TextAnchor.MiddleLeft, FontStyle.Bold, 28f);
        portInput = CreateInputField(connectionPanel.transform, "PortInput", DefaultPort.ToString(), true);

        GameObject buttonRow = new GameObject("ConnectionButtons", typeof(RectTransform));
        buttonRow.transform.SetParent(connectionPanel.transform, false);
        HorizontalLayoutGroup buttonRowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonRowLayout.spacing = 12f;
        buttonRowLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonRowLayout.childControlHeight = true;
        buttonRowLayout.childControlWidth = true;
        buttonRowLayout.childForceExpandHeight = false;
        buttonRowLayout.childForceExpandWidth = true;
        AddLayoutElement(buttonRow, 0f, 56f);

        hostButton = CreateButton(buttonRow.transform, "HostButton", "Host", 22, 56f);
        clientButton = CreateButton(buttonRow.transform, "ClientButton", "Client", 22, 56f);
        hostButton.onClick.AddListener(OnHostClicked);
        clientButton.onClick.AddListener(OnClientClicked);

        connectionText = CreateText(connectionPanel.transform, "ConnectionText", $"Host IP: {DefaultIpAddress}    Port: {DefaultPort}", 18, TextAnchor.MiddleCenter, FontStyle.Normal, 54f);

        GameObject boardPanel = new GameObject("BoardPanel", typeof(RectTransform));
        boardPanel.transform.SetParent(rootPanel.transform, false);
        Image boardBackground = boardPanel.AddComponent<Image>();
        boardBackground.color = new Color(0.18f, 0.22f, 0.28f, 0.96f);
        GridLayoutGroup grid = boardPanel.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 170f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;
        RectTransform boardRect = boardPanel.GetComponent<RectTransform>();
        boardRect.sizeDelta = new Vector2(534f, 534f);
        AddLayoutElement(boardPanel, 534f, 534f);

        cellButtons = new Button[9];
        cellLabels = new Text[9];
        for (int i = 0; i < 9; i++)
        {
            Button cellButton = CreateButton(boardPanel.transform, $"Cell{i}", string.Empty, 72, 170f);
            Text label = cellButton.GetComponentInChildren<Text>();
            label.fontSize = 72;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;

            int index = i;
            cellButton.onClick.AddListener(() => OnCellClicked(index));

            cellButtons[i] = cellButton;
            cellLabels[i] = label;
        }

        statusText = CreateText(rootPanel.transform, "StatusText", "Choose Host or Client to begin.", 22, TextAnchor.MiddleCenter, FontStyle.Bold, 72f);
        restartButton = CreateButton(rootPanel.transform, "RestartButton", "Restart Match", 22, 60f);
        restartButton.onClick.AddListener(OnRestartClicked);
        restartButton.gameObject.SetActive(false);
    }

    private void RefreshUi()
    {
        TicTacToeGame game = TicTacToeGame.Instance;

        for (int i = 0; i < cellButtons.Length; i++)
        {
            string value = game != null && game.IsSpawned ? game.GetCellDisplay(i) : string.Empty;
            bool interactable = game != null && game.IsSpawned && game.CanSelectCell(i);

            cellLabels[i].text = value;
            cellButtons[i].interactable = interactable;
        }

        if (game != null && game.IsSpawned)
        {
            statusText.text = game.BuildStatusMessage();
            restartButton.gameObject.SetActive(game.GameResultValue != -1 && game.HasTwoPlayers);
            restartButton.interactable = game.CanRestart();
        }
        else
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                statusText.text = manager.IsHost
                    ? "Waiting for the game board to initialize..."
                    : "Joining the match...";
            }
            else
            {
                statusText.text = "Choose Host or Client to begin.";
            }

            restartButton.gameObject.SetActive(false);
        }
    }

    private void OnHostClicked()
    {
        launcher?.StartAsHost();
    }

    private void OnClientClicked()
    {
        launcher?.StartAsClient();
    }

    private void OnCellClicked(int index)
    {
        TicTacToeGame.Instance?.TrySubmitMoveFromLocal(index);
    }

    private void OnRestartClicked()
    {
        TicTacToeGame.Instance?.RequestRestartFromLocal();
    }

    private void EnsureEventSystemExists()
    {
        EventSystem existingSystem = FindFirstObjectByType<EventSystem>();
        if (existingSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, FontStyle fontStyle, float preferredHeight)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text uiText = textObject.AddComponent<Text>();
        uiText.font = uiFont;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.fontStyle = fontStyle;
        uiText.color = Color.white;
        uiText.raycastTarget = false;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;

        AddLayoutElement(textObject, 0f, preferredHeight);
        return uiText;
    }

    private Button CreateButton(Transform parent, string name, string labelText, int fontSize, float preferredHeight)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.26f, 0.34f, 0.48f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.26f, 0.34f, 0.48f, 1f);
        colors.highlightedColor = new Color(0.31f, 0.41f, 0.58f, 1f);
        colors.pressedColor = new Color(0.2f, 0.27f, 0.38f, 1f);
        colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        button.colors = colors;

        AddLayoutElement(buttonObject, 0f, preferredHeight);

        Text label = CreateText(buttonObject.transform, "Label", labelText, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold, preferredHeight);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private InputField CreateInputField(Transform parent, string name, string defaultValue, bool numericOnly)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform));
        inputObject.transform.SetParent(parent, false);

        Image background = inputObject.AddComponent<Image>();
        background.color = Color.white;

        InputField inputField = inputObject.AddComponent<InputField>();
        inputField.contentType = numericOnly ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;
        AddLayoutElement(inputObject, 0f, 52f);

        GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
        placeholderObject.transform.SetParent(inputObject.transform, false);
        Text placeholder = placeholderObject.AddComponent<Text>();
        placeholder.font = uiFont;
        placeholder.fontSize = 20;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.45f, 0.45f, 0.45f, 0.8f);
        placeholder.raycastTarget = false;
        placeholder.text = numericOnly ? "7777" : "192.168.0.215";

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(inputObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.black;
        text.raycastTarget = false;
        text.supportRichText = false;

        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(14f, 6f);
        placeholderRect.offsetMax = new Vector2(-14f, -6f);

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 6f);
        textRect.offsetMax = new Vector2(-14f, -6f);

        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.targetGraphic = background;
        inputField.text = defaultValue;

        return inputField;
    }

    private void AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight)
    {
        LayoutElement layoutElement = target.AddComponent<LayoutElement>();
        if (preferredWidth > 0f)
        {
            layoutElement.preferredWidth = preferredWidth;
        }

        if (preferredHeight > 0f)
        {
            layoutElement.preferredHeight = preferredHeight;
        }
    }

    private Font LoadDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "DejaVu Sans" }, 16);
        }

        return font;
    }
}
