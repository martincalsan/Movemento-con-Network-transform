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

    float _velocityY;       // servidor (modos 0 y 1)
    float _localVelocityY;  // cliente (modos 1 y 2)

    InputAction _moveAction;
    InputAction _jumpAction;

    void Awake()
    {
        var asset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        if (asset == null) return;
        asset.Enable();
        _moveAction = asset.FindAction("Player/Move");
        _jumpAction = asset.FindAction("Player/Jump");
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
    bool jump  = _jumpAction.WasPressedThisFrame();

    switch (GameManager.Instance?.Mode ?? 0)
    {
        case 0:
            MoveServerRpc(input, jump);
            break;

        case 1:
            MoveServerRpc(input, jump);  // servidor mueve, NetworkTransform sincroniza
            break;

        case 2:
            ApplyMovement(input, jump, ref _localVelocityY);
            SyncPositionServerRpc(transform.position, _localVelocityY);
            break;
    }
}

    // Modos 0 y 1: servidor aplica física y opcionalmente corrige al cliente
    [ServerRpc]
    void MoveServerRpc(Vector2 input, bool jump)
    {
        ApplyMovement(input, jump, ref _velocityY);

        if (GameManager.Instance?.Mode == 1)
            CorrectOwnerClientRpc(transform.position, _velocityY);
    }

    // Modo 2: servidor recibe posición del cliente y la valida
    [ServerRpc]
    void SyncPositionServerRpc(Vector3 pos, float velocityY)
    {
        pos.x = Mathf.Clamp(pos.x, -Bound, Bound);
        pos.z = Mathf.Clamp(pos.z, -Bound, Bound);
        transform.position = pos;
        _velocityY = velocityY;
    }

    // Modo 1: servidor corrige al owner si diverge demasiado
    [ClientRpc]
    void CorrectOwnerClientRpc(Vector3 serverPos, float serverVelocityY)
    {
        if (!IsOwner) return;
        if (Vector3.Distance(transform.position, serverPos) > 0.5f)
        {
            transform.position  = serverPos;
            _localVelocityY     = serverVelocityY;
        }
    }

    void ApplyMovement(Vector2 input, bool jump, ref float velocityY)
    {
        bool grounded = transform.position.y <= GroundY + 0.05f;

        if (grounded && jump)       velocityY = Jump;
        else if (grounded)          velocityY = Mathf.Max(velocityY, 0f);

        velocityY += Gravity * Time.deltaTime;

        var pos = transform.position + new Vector3(input.x * Speed, velocityY, input.y * Speed) * Time.deltaTime;
        pos.y = Mathf.Max(pos.y, GroundY);
        pos.x = Mathf.Clamp(pos.x, -Bound, Bound);
        pos.z = Mathf.Clamp(pos.z, -Bound, Bound);

        if (pos.y == GroundY && velocityY < 0) velocityY = 0f;
        transform.position = pos;
    }
}