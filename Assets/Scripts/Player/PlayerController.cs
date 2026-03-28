using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

// PlayerController handles movement for a networked player.
// It inherits from NetworkBehaviour instead of MonoBehaviour, which gives it
// access to NGO properties like IsOwner, IsServer, and IsClient, as well as
// network lifecycle callbacks like OnNetworkSpawn.
public class PlayerController : NetworkBehaviour
{
    // [SerializeField] exposes a private field in the Unity Inspector.
    // Adjust move speed per-prefab without changing code.
    [SerializeField] private float _moveSpeed = 5f;

    private InputAction _moveAction;

    // OnNetworkSpawn() is called by NGO after this object is spawned on the network
    // and ownership has been assigned. Use this instead of Start() for any setup
    // that depends on knowing IsOwner, IsServer, or IsClient.
    public override void OnNetworkSpawn()
    {
        // IsOwner is true only on the instance that owns this object.
        // In NGO, each player prefab is owned by the client that spawned it.
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
}
