using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public class NetworkLauncher : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private Text connectionText;
    [SerializeField] private GameObject connectionPanel;

    [Header("Defaults")]
    [SerializeField] private string defaultIpAddress = "192.168.0.215";
    [SerializeField] private ushort defaultPort = 7777;

    private NetworkManager networkManager;
    private UnityTransport transport;
    private GameObject gameStatePrefabTemplate;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();
    }

    private void OnEnable()
    {
        if (networkManager == null)
        {
            networkManager = GetComponent<NetworkManager>();
        }

        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void Configure(
        InputField ipInputRef,
        InputField portInputRef,
        Text connectionTextRef,
        GameObject connectionPanelRef,
        string defaultIp,
        ushort defaultPortValue)
    {
        ipInput = ipInputRef;
        portInput = portInputRef;
        connectionText = connectionTextRef;
        connectionPanel = connectionPanelRef;
        defaultIpAddress = defaultIp;
        defaultPort = defaultPortValue;

        if (ipInput != null && string.IsNullOrWhiteSpace(ipInput.text))
        {
            ipInput.text = defaultIpAddress;
        }

        if (portInput != null && string.IsNullOrWhiteSpace(portInput.text))
        {
            portInput.text = defaultPort.ToString();
        }

        SetConnectionText($"Host IP: {defaultIpAddress}    Port: {defaultPort}");
    }

    public void SetGameStatePrefabTemplate(GameObject prefabTemplate)
    {
        gameStatePrefabTemplate = prefabTemplate;
    }

    public void StartAsHost()
    {
        if (networkManager == null || transport == null || networkManager.IsListening)
        {
            return;
        }

        string hostIp = ReadIp();
        ushort port = ReadPort();

        transport.SetConnectionData(hostIp, port, "0.0.0.0");

        bool started = networkManager.StartHost();
        if (!started)
        {
            SetConnectionText("Failed to start host.");
            return;
        }

        SetConnectionText($"Hosting on {hostIp}:{port}");
        SpawnGameStateIfNeeded();
    }

    public void StartAsClient()
    {
        if (networkManager == null || transport == null || networkManager.IsListening)
        {
            return;
        }

        string hostIp = ReadIp();
        ushort port = ReadPort();

        transport.SetConnectionData(hostIp, port);

        bool started = networkManager.StartClient();
        SetConnectionText(started
            ? $"Connecting to {hostIp}:{port}..."
            : "Failed to start client.");
    }

    public void ShutdownSession()
    {
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        DestroySpawnedGameStates();

        if (connectionPanel != null)
        {
            connectionPanel.SetActive(true);
        }

        SetConnectionText("Disconnected.");
    }

    private void SpawnGameStateIfNeeded()
    {
        if (!networkManager.IsServer || gameStatePrefabTemplate == null)
        {
            return;
        }

        if (TicTacToeGame.Instance != null)
        {
            return;
        }

        GameObject spawned = Instantiate(gameStatePrefabTemplate);
        spawned.name = "GameController";
        spawned.hideFlags = HideFlags.None;

        NetworkObject networkObject = spawned.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Destroy(spawned);
            SetConnectionText("Cannot start game: missing NetworkObject on runtime prefab.");
            return;
        }

        networkObject.Spawn();
    }

    private void DestroySpawnedGameStates()
    {
        TicTacToeGame[] games = FindObjectsByType<TicTacToeGame>(FindObjectsSortMode.None);
        for (int i = 0; i < games.Length; i++)
        {
            if (games[i] == null || games[i].gameObject == gameStatePrefabTemplate)
            {
                continue;
            }

            Destroy(games[i].gameObject);
        }
    }

    private string ReadIp()
    {
        if (ipInput == null || string.IsNullOrWhiteSpace(ipInput.text))
        {
            return defaultIpAddress;
        }

        return ipInput.text.Trim();
    }

    private ushort ReadPort()
    {
        if (portInput != null && ushort.TryParse(portInput.text, out ushort parsedPort))
        {
            return parsedPort;
        }

        return defaultPort;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager == null || clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionPanel != null)
        {
            connectionPanel.SetActive(false);
        }

        string role = networkManager.IsHost ? "Host" : "Client";
        SetConnectionText($"{role} connected.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager == null || clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionPanel != null)
        {
            connectionPanel.SetActive(true);
        }

        string reason = networkManager.DisconnectReason;
        if (string.IsNullOrWhiteSpace(reason))
        {
            SetConnectionText("Disconnected.");
        }
        else
        {
            SetConnectionText($"Disconnected: {reason}");
        }
    }

    private void SetConnectionText(string message)
    {
        if (connectionText != null)
        {
            connectionText.text = message;
        }
    }
}
