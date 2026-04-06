using System;
using UnityEngine;
using Unity.Netcode;

// GameManager handles startup logic — deciding whether this instance runs as a
// dedicated server or presents the connect UI to the player.
// It is a NetworkBehaviour so it can host NetworkVariables for lobby state.
public class GameManager : NetworkBehaviour
{
    // Singleton — lets PlayerController and NetworkManagerUI read game state
    // without requiring Inspector references.
    public static GameManager Instance { get; private set; }

    // Replicated flag: false = lobby, true = game is running.
    // The server writes; all clients read.
    public NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // The client ID of the player who gets the "Start Game" button.
    // Set by the server when the first client connects.
    // ulong.MaxValue is used as a sentinel meaning "not yet assigned".
    public NetworkVariable<ulong> LobbyLeaderClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // OnNetworkSpawn() is the right place to subscribe to NGO callbacks because
    // the NetworkManager is guaranteed to be live at this point.
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
    }

    // Always mirror subscriptions with unsubscriptions to prevent leaks.
    public override void OnNetworkDespawn()
    {
        if (IsServer)
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
    }

    // Fires on the server each time a new client (including the host) connects.
    private void OnClientConnected(ulong clientId)
    {
        // The very first client to connect becomes the lobby leader.
        if (LobbyLeaderClientId.Value == ulong.MaxValue)
            LobbyLeaderClientId.Value = clientId;
    }

    // Clients call this to request the game start.
    // RequireOwnership = false because GameManager is owned by the server,
    // not by any individual player client.
    [ServerRpc(RequireOwnership = false)]
    public void StartGameServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only the designated lobby leader may start the game.
        if (rpcParams.Receive.SenderClientId != LobbyLeaderClientId.Value)
            return;

        GameStarted.Value = true;
    }

    // Start() is called once when the scene loads, before the first frame.
    // This is where we check command-line arguments to decide the role of this instance.
    void Start()
    {
        // GetCommandLineArgs() returns all arguments passed to the executable.
        // Example: MyGame.exe -server
        string[] args = Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (arg == "-server")
            {
                // The -server flag was found: start as a dedicated server immediately.
                // A dedicated server simulates the game but does not render a player.
                StartServer();
                return;
            }
        }

        // No -server flag: show the connect UI (handled by NetworkManagerUI).
        // The player will choose Client, Host, or Server from the on-screen buttons.
    }

    private void StartServer()
    {
        Debug.Log("Starting in dedicated server mode.");
        // StartServer() starts NGO in server-only mode: no local player is spawned.
        NetworkManager.Singleton.StartServer();
    }

    // OnApplicationQuit() is called when the application closes or Play mode stops.
    // Explicitly shutting down NGO ensures the UDP port is released immediately,
    // preventing "port already in use" errors on the next run.
    private void OnApplicationQuit()
    {
        // IsListening is true if this instance is currently running as a server or client.
        // We check for null in case the scene was never fully initialized.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
    }
}
