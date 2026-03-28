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

    // OnGUI() is a MonoBehaviour message called by Unity every frame (and potentially
    // multiple times per frame for different event types such as layout and repaint).
    // Unlike Update(), it is specifically for drawing immediate-mode UI.
    private void OnGUI()
    {
        // Guard against the NetworkManager being destroyed during shutdown.
        if (NetworkManager.Singleton == null)
            return;

        // Once a network session is active, hide this UI.
        // IsClient is true for both Client and Host roles.
        // IsServer is true for both Server and Host roles.
        // Together they cover all three roles — the UI disappears as soon as any button is pressed.
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            return;

        // Layout constants — positions are in screen pixels from the top-left corner.
        float x = 10;
        float y = 10;
        float labelWidth = 60;
        float fieldWidth = 120;
        float buttonWidth = 80;
        float rowHeight = 30;

        // Draw IP and port input fields.
        // GUI.TextField returns the current string value after any edits this frame.
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "IP:");
        _ipAddress = GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), _ipAddress);

        y += rowHeight;
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "Port:");
        _port = GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), _port);

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
    }
}
