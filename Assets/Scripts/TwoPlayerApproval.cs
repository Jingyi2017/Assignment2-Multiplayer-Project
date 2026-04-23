using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class TwoPlayerApproval : MonoBehaviour
{
    [SerializeField] private int maxPlayers = 2;

    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
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

        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.ConnectionApprovalCallback = null;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool approved = networkManager.ConnectedClientsIds.Count < maxPlayers;

        response.Approved = approved;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = approved ? string.Empty : $"This match already has {maxPlayers} players.";
    }
}
