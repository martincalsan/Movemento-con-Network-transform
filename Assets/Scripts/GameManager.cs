using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    readonly NetworkVariable<int> _mode = new(0);
    static readonly string[] ModeNames = { "Server Authority", "Server + Prediction", "Client Authority" };

    public int Mode => _mode.Value;

    void Awake() => Instance = this;

    void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;
        
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host"))   NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            return;
        }

        GUILayout.Label($"Modo: {ModeNames[_mode.Value]}");
        if (GUILayout.Button("Cambiar Modo")) RequestModeChangeServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestModeChangeServerRpc() => _mode.Value = (_mode.Value + 1) % 3;
}