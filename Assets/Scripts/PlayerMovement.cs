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

    float _velocityY; // Autoridad del servidor
    float _localVelocityY; // Predicción del cliente
    
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
        
        int mode = GameManager.Instance?.Mode ?? 0;
        
        switch (mode)
        {
            case 0: // Server Authority
                MoveServerRpc(input, jump);
                break;
            
            case 1: // Server Authority con Prediction
                UpdateLocalMovement(input, jump);
                MoveServerRpc(input, jump);
                break;
            
            case 2: // Client Authority
                UpdateLocalMovement(input, jump);
                SyncMovementClientRpc(transform.position, _localVelocityY);
                break;
        }
    }

    void UpdateLocalMovement(Vector2 input, bool jump)
    {
        bool grounded = transform.position.y <= GroundY + 0.05f;

        if (grounded && jump)        _localVelocityY = Jump;
        else if (grounded)           _localVelocityY = Mathf.Max(_localVelocityY, 0f);

        _localVelocityY += Gravity * Time.deltaTime;

        var pos = transform.position + new Vector3(input.x * Speed, _localVelocityY, input.y * Speed) * Time.deltaTime;
        pos.y = Mathf.Max(pos.y, GroundY);
        pos.x = Mathf.Clamp(pos.x, -Bound, Bound);
        pos.z = Mathf.Clamp(pos.z, -Bound, Bound);

        if (pos.y == GroundY && _localVelocityY < 0) _localVelocityY = 0f;
        transform.position = pos;
    }

    [ServerRpc]
    void MoveServerRpc(Vector2 input, bool jump)
    {
        int mode = GameManager.Instance?.Mode ?? 0;
        
        // En modo 0 y 1, el servidor aplica la física
        if (mode == 0 || mode == 1)
        {
            ApplyMovement(input, jump);
            
            // En modo 1, sincronizar con el cliente
            if (mode == 1)
                SyncMovementClientRpc(transform.position, _velocityY);
        }
        // En modo 2 (client authority), el servidor no hace nada
    }

    [ClientRpc]
    void SyncMovementClientRpc(Vector3 position, float velocityY)
    {
        int mode = GameManager.Instance?.Mode ?? 0;
        
        if (mode == 1 && IsOwner)
        {
            // En predicción, sincronizar con la posición del servidor si hay mucha diferencia
            if (Vector3.Distance(transform.position, position) > 0.5f)
            {
                transform.position = position;
                _localVelocityY = velocityY;
            }
        }
        else if (mode == 2 && !IsOwner)
        {
            // En client authority, otros clientes reciben la posición
            transform.position = position;
        }
    }

    void ApplyMovement(Vector2 input, bool jump)
    {
        bool grounded = transform.position.y <= GroundY + 0.05f;

        if (grounded && jump)        _velocityY = Jump;
        else if (grounded)           _velocityY = Mathf.Max(_velocityY, 0f);

        _velocityY += Gravity * Time.deltaTime;

        var pos = transform.position + new Vector3(input.x * Speed, _velocityY, input.y * Speed) * Time.deltaTime;
        pos.y = Mathf.Max(pos.y, GroundY);
        pos.x = Mathf.Clamp(pos.x, -Bound, Bound);
        pos.z = Mathf.Clamp(pos.z, -Bound, Bound);

        if (pos.y == GroundY && _velocityY < 0) _velocityY = 0f;
        transform.position = pos;
    }
}