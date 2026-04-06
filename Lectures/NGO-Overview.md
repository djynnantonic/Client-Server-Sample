# Unity Netcode for GameObjects — Lecture Slides

---

## Slide 1: What Is NGO?

**Unity Netcode for GameObjects (NGO)** is Unity's first-party networking library for multiplayer games.

- Runs on top of a **transport layer** (Unity Transport / UTP) that handles raw UDP packets
- Provides high-level abstractions so you write *game logic*, not socket code
- Everything revolves around a single coordinator: the **NetworkManager**
- One build can run as a **Server**, a **Client**, or a **Host** (both at once)

```
           ┌───────────────────────────────────┐
           │           NetworkManager           │
           │  • Starts/stops the session        │
           │  • Spawns networked objects        │
           │  • Routes messages                 │
           └──────────┬────────────────┬────────┘
                      │                │
             ┌────────▼──────┐ ┌───────▼────────┐
             │ NetworkObject │ │ NetworkBehaviour│
             │  (component)  │ │  (your script) │
             └───────────────┘ └───────┬─────────┘
                                       │
                        ┌──────────────┼──────────────┐
                        │              │              │
               NetworkVariable      ServerRpc     ClientRpc
               (replicated state)  (client→server) (server→client)
```

---

## Slide 2: Topology & Roles

NGO supports three **session modes** — chosen at runtime, not compile time.

| Mode | Who runs it | Has a local player? |
|------|------------|---------------------|
| **Server** | Dedicated machine | No — simulation only |
| **Client** | Player machine | Yes |
| **Host** | Player machine | Yes — also runs the server |

**Key design rules:**
- The **server is authoritative** — it owns all game state
- **Clients request** changes via `ServerRpc`; the server decides whether to apply them
- **State replicates** to all clients automatically via `NetworkVariable`
- Objects have an **owner** — typically the client who controls that object

> In this project one binary serves all roles. A `-server` command-line flag starts it as a dedicated server; without it, the player picks Host or Join from the UI.

---

## Keyword: `NetworkBehaviour`

**What it is:** The base class for any script that participates in networking — replaces `MonoBehaviour`.

**Why it matters:** Inheriting from `NetworkBehaviour` gives your script access to every NGO property and lifecycle method (`IsServer`, `IsOwner`, `OnNetworkSpawn`, etc.).

```csharp
// GameManager.cs
public class GameManager : NetworkBehaviour { ... }

// PlayerController.cs
public class PlayerController : NetworkBehaviour { ... }
```

> **Rule of thumb:** If a script reads or writes networked state, or needs to know its role, it should be a `NetworkBehaviour`.

---

## Keyword: `NetworkManager` / `NetworkManager.Singleton`

**What it is:** The central coordinator for the session — one per scene.

- `NetworkManager.Singleton` is a static reference to the scene's NetworkManager GameObject
- Exposes role flags, connected client lists, spawn controls, and events
- Must be in the scene before play starts (not spawned at runtime)

```csharp
// NetworkManagerUI.cs — check whether we're currently running
if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
    DrawConnectUI();

// GameManager.cs — subscribe to a server-side event
NetworkManager.OnClientConnectedCallback += OnClientConnected;
```

> Access `NetworkManager.Singleton` from any script; access `NetworkManager` directly from inside a `NetworkBehaviour`.

---

## Keyword: `StartServer()` / `StartHost()` / `StartClient()`

**What they are:** The three methods that begin a network session.

```csharp
// NetworkManagerUI.cs — called when the player clicks a button
NetworkManager.Singleton.StartClient();   // join an existing session
NetworkManager.Singleton.StartHost();    // start a session AND join as a player
NetworkManager.Singleton.StartServer();  // start a session with no local player

// GameManager.cs — called automatically when -server flag is found
NetworkManager.Singleton.StartServer();
```

| Method | Server simulation | Local player |
|--------|------------------|--------------|
| `StartServer()` | ✅ | ❌ |
| `StartHost()` | ✅ | ✅ |
| `StartClient()` | ❌ | ✅ |

> Call `ApplyConnectionSettings()` (set IP/port on the transport) **before** calling any of these.

---

## Keyword: `IsServer` / `IsClient`

**What they are:** Boolean properties on `NetworkBehaviour` that tell a script what role *this instance* is running as.

```csharp
// GameManager.cs — only subscribe to callbacks on the server
public override void OnNetworkSpawn()
{
    if (IsServer)
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
}

// PlayerController.cs — only the server assigns color and position
if (IsServer)
{
    _colorIndex.Value = (int)(OwnerClientId % (ulong)PlayerColors.Length);
    transform.position = new Vector3((_playerNumber.Value - 1) * 1f, 0f, 0f);
}
```

> On a **Host**, both `IsServer` and `IsClient` are `true` at the same time.

---

## Keyword: `IsOwner`

**What it is:** `true` only on the client instance that **owns** this `NetworkObject`.

- Ownership is assigned by the server at spawn time (usually to the connecting client)
- Use `IsOwner` to gate input handling and camera setup — only the owner should drive their own player

```csharp
// PlayerController.cs — only the owning client sets up input
public override void OnNetworkSpawn()
{
    // ... shared setup for all instances ...

    if (!IsOwner)
        return;   // everything below runs only on the owner

    SetPlayerNameServerRpc(LocalPlayerName);
    _moveAction = InputSystem.actions.FindAction("Move");
    _moveAction.Enable();
}
```

> `IsOwner` is preferred over `IsLocalPlayer` because it works correctly on *any* `NetworkBehaviour`, not just the player object.

---

## Keyword: `OwnerClientId` / `LocalClientId`

**What they are:** Client ID numbers used to identify who owns what.

- `OwnerClientId` — property on a `NetworkBehaviour`; the ID of the client that owns *this object*
- `NetworkManager.Singleton.LocalClientId` — the ID of *this* client in the current session (server = 0)

```csharp
// PlayerController.cs — map client ID to a color slot
_colorIndex.Value = (int)(OwnerClientId % (ulong)PlayerColors.Length);

// NetworkManagerUI.cs — check if this client is the lobby leader
bool isLobbyLeader = GameManager.Instance.LobbyLeaderClientId.Value
                     == NetworkManager.Singleton.LocalClientId;
```

> Client IDs are assigned sequentially starting from 0. The server (or host-as-server) is always ID 0.

---

## Keyword: `OnNetworkSpawn()` / `OnNetworkDespawn()`

**What they are:** NGO lifecycle callbacks on `NetworkBehaviour` — prefer these over `Start()` / `OnDestroy()` for anything networking-related.

- `OnNetworkSpawn()` — called after the object is registered with the network and ownership is set; safe to read `IsOwner`, `IsServer`, `OwnerClientId`
- `OnNetworkDespawn()` — called just before the object is removed from the network; use it to unsubscribe from events

```csharp
// PlayerController.cs
public override void OnNetworkSpawn()
{
    _colorIndex.OnValueChanged += OnColorChanged;
    ApplyColor(_colorIndex.Value);
    // ...
}

public override void OnNetworkDespawn()
{
    _colorIndex.OnValueChanged -= OnColorChanged;   // always unsubscribe!
    _playerNumber.OnValueChanged -= OnPlayerNumberChanged;
    _playerName.OnValueChanged -= OnPlayerNameChanged;
}
```

> Using `Start()` for network setup is a common bug — the network may not be ready yet.

---

## Keyword: `NetworkVariable<T>`

**What it is:** A generic container that **automatically replicates** its value from the server to all clients.

- The server writes; clients read (by default)
- Changing `.Value` on the server pushes the new value to every connected client
- Supported types: primitives, structs, and NGO value types (like `FixedString64Bytes`)

```csharp
// GameManager.cs
public NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(
    false,                                   // initial value
    NetworkVariableReadPermission.Everyone,  // who can read
    NetworkVariableWritePermission.Server    // who can write
);

// Reading the value (any client, any script):
if (GameManager.Instance.GameStarted.Value)
    DrawLobbyUI();
```

> `NetworkVariable` is the right tool for **persistent state** (health, color, name). Use RPCs for **one-time events** (fire weapon, play sound).

---

## Keyword: `NetworkVariableReadPermission` / `NetworkVariableWritePermission`

**What they are:** Enums that control which machines can read or write a `NetworkVariable`.

| Permission | Meaning |
|-----------|---------|
| `ReadPermission.Everyone` | All clients and server can read |
| `ReadPermission.Owner` | Only the owning client and server can read |
| `WritePermission.Server` | Only the server can write (most common) |
| `WritePermission.Owner` | Only the owning client can write |

```csharp
// PlayerController.cs — server writes, everyone reads
private NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

> In a server-authoritative design you will almost always use `WritePermission.Server`.

---

## Keyword: `NetworkVariable.OnValueChanged`

**What it is:** A delegate event fired on every client (including the server) when a `NetworkVariable`'s value changes.

- Signature: `(TPreviousValue previous, TCurrentValue current)`
- Fires **after** the value has already been applied — `current` is the new live value
- Subscribe in `OnNetworkSpawn`, unsubscribe in `OnNetworkDespawn`

```csharp
// PlayerController.cs — react to color changes on every client
_colorIndex.OnValueChanged += OnColorChanged;

private void OnColorChanged(int previous, int current)
{
    ApplyColor(current);
}
```

> Always call the apply method **immediately after subscribing** too — a late-joining client will receive the current value but won't get a change event for it.

---

## Keyword: `FixedString64Bytes`

**What it is:** A fixed-size, value-type UTF-8 string from `Unity.Collections` — safe to store in a `NetworkVariable`.

- Regular C# `string` is a reference type and cannot be used directly in `NetworkVariable<T>`
- `FixedString64Bytes` stores up to **64 bytes** of UTF-8 text (enough for most names/labels)
- Convert to/from `string` with `.ToString()` and the implicit constructor

```csharp
// PlayerController.cs
private NetworkVariable<FixedString64Bytes> _playerName =
    new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes("Player"), ...);

// Sending a string to the server via RPC
SetPlayerNameServerRpc(LocalPlayerName);  // string implicitly converts

// Reading it back
ApplyPlayerName(_playerName.Value.ToString());
```

> Other sizes exist: `FixedString32Bytes`, `FixedString128Bytes`, `FixedString512Bytes`.

---

## Keyword: `[ServerRpc]` / `ServerRpcParams`

**What they are:** `[ServerRpc]` marks a method so that **calling it on a client sends it to the server** to execute there.

- Method name **must** end in `ServerRpc`
- `RequireOwnership = false` allows clients that don't own the object to call it
- `ServerRpcParams` is an optional last parameter — use it to read `SenderClientId` on the server

```csharp
// PlayerController.cs — owner calls this; server executes it
[ServerRpc(RequireOwnership = false)]
private void SetPlayerNameServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
{
    // Validate: only accept from the actual owner
    if (rpcParams.Receive.SenderClientId != OwnerClientId)
        return;

    _playerName.Value = name;   // now replicates to all clients via NetworkVariable
}
```

> The server can't trust client input blindly. Always validate `SenderClientId` against expected ownership when using `RequireOwnership = false`.

---

## Keyword: `ConnectedClients`

**What it is:** A `Dictionary<ulong, NetworkClient>` on the server listing every currently-connected client.

- Only valid on the server/host
- Keys are client IDs; values are `NetworkClient` objects (which expose their owned objects)
- `ConnectedClients.Count` gives the number of game clients (the server itself is **not** included)

```csharp
// PlayerController.cs — assign a 1-based player number at spawn time
if (IsServer)
{
    // Count is 1 when the first client spawns, 2 for the second, etc.
    _playerNumber.Value = NetworkManager.ConnectedClients.Count;
}
```

> Because the dedicated server is not in `ConnectedClients`, `.Count` directly equals the number of human players — no off-by-one adjustment needed.

---

## Keyword: `OnClientConnectedCallback`

**What it is:** A server-side event on `NetworkManager` that fires each time a new client (or the host) successfully connects.

- Signature: `Action<ulong>` — the argument is the new client's ID
- Subscribe in `OnNetworkSpawn`, unsubscribe in `OnNetworkDespawn`
- Fires for every client including the host itself (client ID 0)

```csharp
// GameManager.cs
public override void OnNetworkSpawn()
{
    if (IsServer)
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
}

public override void OnNetworkDespawn()
{
    if (IsServer)
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
}

private void OnClientConnected(ulong clientId)
{
    if (LobbyLeaderClientId.Value == ulong.MaxValue)
        LobbyLeaderClientId.Value = clientId;  // first to connect becomes leader
}
```

---

## Keyword: `UnityTransport` / `SetConnectionData()`

**What it is:** `UnityTransport` (UTP) is the **transport layer** component that sends and receives raw UDP packets. NGO sits on top of it.

- Lives as a component on the same GameObject as the `NetworkManager`
- Must be configured with the target IP and port **before** starting a session
- `SetConnectionData(ip, port)` sets both the listen address (server) and connect address (client)

```csharp
// NetworkManagerUI.cs — called before StartClient/Host/Server
private void ApplyConnectionSettings()
{
    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    if (transport != null)
        transport.SetConnectionData(_ipAddress, ushort.Parse(_port));
}
```

> Default port is **7777**. Default address **127.0.0.1** (loopback) is fine for testing on one machine; use the host machine's LAN IP for cross-machine play.

---

## Keyword: `NetworkManager.Shutdown()` / `IsListening`

**What they are:** Clean session teardown.

- `IsListening` — `true` when the `NetworkManager` is actively running as a server or client
- `Shutdown()` — stops the session, releases the UDP port, and despawns all network objects

```csharp
// GameManager.cs — called when the application closes
private void OnApplicationQuit()
{
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        NetworkManager.Singleton.Shutdown();
}
```

> Always call `Shutdown()` before quitting or restarting a session. Skipping it can leave the UDP port bound, causing "port already in use" errors on the next run — especially noticeable in the Unity Editor.
