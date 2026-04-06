using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// NetworkManagerUI draws the connect screen using Unity's built-in IMGUI system.
// IMGUI (Immediate Mode GUI) requires no Canvas setup — GUI calls are made every frame
// and simply stop being called when the UI should disappear.
public class NetworkManagerUI : MonoBehaviour
{
    // Default values pre-filled in the text fields.
    // 127.0.0.1 is the loopback address — it always refers to the same machine.
    // 7777 is the default port used by Unity Transport.
    private string _ipAddress = "127.0.0.1";
    private string _port = "7777";

    // Player display name shown above the capsule in-game.
    // Generated once at startup and editable by the player before connecting.
    private string _playerName;

    // Awake() is called before the first frame. We generate the name here so it
    // is ready before OnGUI() draws the connect screen.
    private void Awake()
    {
        _playerName = NameGenerator.Generate();
    }

    // OnGUI() is a MonoBehaviour message called by Unity every frame (and potentially
    // multiple times per frame for different event types such as layout and repaint).
    // Unlike Update(), it is specifically for drawing immediate-mode UI.
    private void OnGUI()
    {
        // Guard against the NetworkManager being destroyed during shutdown.
        if (NetworkManager.Singleton == null)
            return;

        // Phase 1: not yet connected — show the connect screen.
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            DrawConnectUI();
            return;
        }

        // Dedicated server (no local player) needs no UI.
        if (!NetworkManager.Singleton.IsClient)
            return;

        // Phase 2: connected but game not yet started — show the lobby screen.
        if (GameManager.Instance != null && !GameManager.Instance.GameStarted.Value)
            DrawLobbyUI();

        // Phase 3: game is running — no UI needed.
    }

    // Draws the pre-connection screen where the player enters an IP/port
    // and chooses their role (Client, Host, or Server).
    private void DrawConnectUI()
    {
        // Layout constants — positions are in screen pixels from the top-left corner.
        float x = 10;
        float y = 10;
        float labelWidth = 60;
        float fieldWidth = 120;
        float buttonWidth = 80;
        float rowHeight = 30;

        // Draw IP, port, and name input fields.
        // GUI.TextField returns the current string value after any edits this frame.
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "IP:");
        _ipAddress = GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), _ipAddress);

        y += rowHeight;
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "Port:");
        _port = GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), _port);

        y += rowHeight;
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "Name:");
        _playerName = GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), _playerName);

        y += rowHeight;

        // Client: connects to an existing server or host at the specified IP and port.
        // Use this when another machine (or another instance) is already running as Host or Server.
        if (GUI.Button(new Rect(x, y, buttonWidth, rowHeight), "Client"))
        {
            ApplyConnectionSettings();
            NetworkManager.Singleton.StartClient();
        }

        // Host: starts a combined server + local client on this machine.
        // The host participates in the game as a player AND runs the authoritative simulation.
        if (GUI.Button(new Rect(x + buttonWidth + 10, y, buttonWidth, rowHeight), "Host"))
        {
            ApplyConnectionSettings();
            NetworkManager.Singleton.StartHost();
        }

        // Server: starts a dedicated server on this machine with no local player.
        // Clients connect to this instance. Equivalent to launching with the -server flag.
        if (GUI.Button(new Rect(x + (buttonWidth + 10) * 2, y, buttonWidth, rowHeight), "Server"))
        {
            ApplyConnectionSettings();
            NetworkManager.Singleton.StartServer();
        }
    }

    // Draws the lobby screen shown after connecting but before the game starts.
    private void DrawLobbyUI()
    {
        GUI.Label(new Rect(10, 10, 400, 30), "(Wait for all players to join before starting game)");

        // Only the lobby leader (first player to connect) sees the Start Game button.
        // The lobby leader is identified by matching their local client ID.
        bool isLobbyLeader = GameManager.Instance.LobbyLeaderClientId.Value ==
                             NetworkManager.Singleton.LocalClientId;

        if (isLobbyLeader)
        {
            if (GUI.Button(new Rect(10, 45, 120, 30), "Start Game"))
                GameManager.Instance.StartGameServerRpc();
        }
    }

    // Pushes the IP address and port from the UI fields into UnityTransport
    // before starting any network session. This must be called before Start*/Connect.
    private void ApplyConnectionSettings()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // SetConnectionData configures both the address to listen on (server)
            // and the address to connect to (client), depending on the role started.
            transport.SetConnectionData(_ipAddress, ushort.Parse(_port));
        }

        // Store the chosen name so PlayerController can read it after spawning.
        // This is a simple static bridge — no scene dependency needed.
        PlayerController.LocalPlayerName = _playerName;
    }
}
