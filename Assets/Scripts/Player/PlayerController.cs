using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;

// PlayerController handles movement, color assignment, and the player number label.
// It inherits from NetworkBehaviour instead of MonoBehaviour, which gives it
// access to NGO properties like IsOwner, IsServer, and IsClient, as well as
// network lifecycle callbacks like OnNetworkSpawn.
public class PlayerController : NetworkBehaviour
{
    // [SerializeField] exposes a private field in the Unity Inspector.
    // Adjust move speed per-prefab without changing code.
    [SerializeField] private float _moveSpeed = 5f;

    // Assign the TextMeshPro child object in the prefab Inspector.
    // This label floats above the capsule and shows the player number.
    [SerializeField] private TextMeshPro _nameLabel;

    // Eight distinct colors, one per player slot (indexed by OwnerClientId % 8).
    private static readonly Color[] PlayerColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new Color(1f, 0.5f, 0f),    // orange
        new Color(0.5f, 0f, 1f),    // purple
    };

    // NetworkVariable<int> is replicated automatically by NGO to all clients.
    // The server writes the value; all clients (including the server) can read it.
    // When the value changes on any client, OnValueChanged fires on that client.
    private NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private InputAction _moveAction;

    // OnNetworkSpawn() is called by NGO after this object is spawned on the network
    // and ownership has been assigned. Use this instead of Start() for any setup
    // that depends on knowing IsOwner, IsServer, or IsClient.
    public override void OnNetworkSpawn()
    {
        // The server assigns each player a color index based on their client ID.
        // OwnerClientId is 0 for the host, 1 for the first client, etc.
        // Modulo 8 keeps the index within the PlayerColors array bounds.
        if (IsServer)
            _colorIndex.Value = (int)(OwnerClientId % (ulong)PlayerColors.Length);

        // Subscribe to future color changes so all clients update visuals
        // if the value arrives after OnNetworkSpawn (common on late-joining clients).
        _colorIndex.OnValueChanged += OnColorChanged;

        // Apply the color immediately in case the value was already set.
        ApplyColor(_colorIndex.Value);

        // Set the player number label. OwnerClientId + 1 gives 1-based numbering (P1, P2, ...).
        if (_nameLabel != null)
            _nameLabel.text = $"P{OwnerClientId + 1}";

        // IsOwner is true only on the instance that owns this object.
        // Without this guard, every client would set up input for every player.
        if (!IsOwner)
            return;

        // Find the "Move" action defined in the InputSystem_Actions asset.
        // This maps to WASD keys and left gamepad stick by default.
        _moveAction = InputSystem.actions.FindAction("Move");
        if (_moveAction == null)
        {
            Debug.LogError("[PlayerController] Move action not found!");
            return;
        }

        // Actions must be explicitly enabled before they produce input values.
        _moveAction.Enable();

        // Tell the camera to follow this player.
        // Camera.main finds the scene camera tagged "MainCamera".
        // Only the owning client reaches this code, so each client's camera
        // follows only their own player.
        CameraController cam = Camera.main?.GetComponent<CameraController>();
        if (cam != null)
            cam.SetTarget(transform);
    }

    // OnNetworkDespawn() is called when this object is removed from the network.
    // Always unsubscribe from NetworkVariable callbacks here to avoid memory leaks.
    public override void OnNetworkDespawn()
    {
        _colorIndex.OnValueChanged -= OnColorChanged;
    }

    private void OnColorChanged(int previous, int current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(int index)
    {
        // GetComponent<Renderer>() gets the MeshRenderer on this capsule.
        // .material creates a unique material instance per object so changing
        // one player's color doesn't affect the others.
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = PlayerColors[index];
    }

    private void Update()
    {
        // Only the owning client should move this player.
        // Non-owners receive position updates automatically via NetworkTransform.
        if (!IsOwner)
            return;

        // ReadValue<Vector2>() returns the current WASD or stick input this frame:
        // x = horizontal (-1 left, +1 right), y = vertical (-1 back, +1 forward)
        Vector2 input = _moveAction.ReadValue<Vector2>();

        // Map the 2D input onto the XZ plane (Y=0 keeps the player on the ground).
        // Multiply by speed and Time.deltaTime to make movement frame-rate independent.
        Vector3 move = new Vector3(input.x, 0, input.y) * _moveSpeed * Time.deltaTime;

        // Translate moves the transform in world space.
        // NetworkTransform will replicate this position change to all other clients.
        transform.Translate(move, Space.World);
    }

    private void LateUpdate()
    {
        // Billboard the label so it always faces the camera regardless of player rotation.
        // This uses the camera's own rotation axes to orient the label correctly.
        if (_nameLabel != null && Camera.main != null)
        {
            _nameLabel.transform.LookAt(
                _nameLabel.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up
            );
        }
    }
}
