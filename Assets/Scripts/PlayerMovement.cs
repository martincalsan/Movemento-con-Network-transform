using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkTransform))]
public class PlayerMovement : NetworkBehaviour
{
    const float Speed   = 5f;
    const float Jump    = 7f;
    const float Gravity = -20f;
    const float GroundY = 0.5f;
    const float Bound   = 4.5f;

    float _velocityY;
    InputActionAsset _inputActions;
    InputAction _moveAction;
    InputAction _jumpAction;

    void OnEnable()
    {
        if (_inputActions == null)
            _inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
        _inputActions?.Enable();
    }

    void OnDisable()
    {
        _inputActions?.Disable();
    }

    void Awake()
    {
        if (_inputActions == null)
            _inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
        
        if (_inputActions != null)
        {
            _moveAction = _inputActions.FindAction("Player/Move");
            _jumpAction = _inputActions.FindAction("Player/Jump");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            transform.position = new Vector3(
                Random.Range(-3f, 3f), GroundY, Random.Range(-3f, 3f));
    }

    void Update()
    {
        if (!IsOwner || _moveAction == null || _jumpAction == null) return;

        var input = _moveAction.ReadValue<Vector2>();
        bool jump = _jumpAction.WasPressedThisFrame();
        
        MoveServerRpc(input, jump);
    }

    [ServerRpc]
    void MoveServerRpc(Vector2 input, bool jump) => ApplyMovement(input, jump);

    void ApplyMovement(Vector2 input, bool jump)
    {
        bool grounded = transform.position.y <= GroundY + 0.05f;

        if (grounded && jump)        _velocityY = Jump;
        else if (grounded)           _velocityY = Mathf.Max(_velocityY, 0f);

        _velocityY += Gravity * Time.deltaTime;

        var pos = transform.position + new Vector3(input.x * Speed, _velocityY, input.y * Speed) * Time.deltaTime;
        pos.y = Mathf.Max(pos.y, GroundY);

        if (pos.y == GroundY && _velocityY < 0) _velocityY = 0f;
        transform.position = pos;
    }
}