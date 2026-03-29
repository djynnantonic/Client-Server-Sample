using UnityEngine;

// CameraController follows the local player by maintaining a fixed offset
// from the player's position each frame.
// Attach this script to the Main Camera in the scene.
// PlayerController calls SetTarget() on spawn to assign the correct player.
public class CameraController : MonoBehaviour
{
    // The offset from the player's position where the camera will sit.
    // Positive Y = above, negative Z = behind.
    // Adjust these values in the Inspector to change the camera angle.
    [SerializeField] private Vector3 _offset = new Vector3(0, 8, -6);

    // The player transform this camera is following.
    // Null until a player calls SetTarget().
    private Transform _target;

    // Called by PlayerController.OnNetworkSpawn() on the owning client only.
    // This ensures the camera follows the correct player in a multi-player session.
    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all Update() calls each frame.
        // This ensures the player has already moved before the camera follows,
        // preventing a one-frame lag between player and camera position.
        if (_target == null)
            return;

        transform.position = _target.position + _offset;

        // Keep the camera looking at the player regardless of position.
        transform.LookAt(_target);
    }
}
