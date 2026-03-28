using System;
using UnityEngine;
using Unity.Netcode;

// GameManager handles startup logic — deciding whether this instance runs as a
// dedicated server or presents the connect UI to the player.
// It is a plain MonoBehaviour because it exists before any network session starts.
public class GameManager : MonoBehaviour
{
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
