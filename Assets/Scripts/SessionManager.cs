using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetMode
{
    Lan,        // LAN ��ε�ĳ��Ʈ�� ����
    UgsP2P,     // UGS Lobby/Relay�� ����(ȣ��Ʈ ����)
    UgsDS       // UGS �� ���� ������ ����(ȣ��Ʈ ����)
}

[Serializable]
public class NetSettings
{
    public NetMode Mode = NetMode.Lan; //���� LAN�� ����

    // LAN
    public ushort LanPort = 7777;
    public int LanTimeoutMs = 1500;
    public float LanBroadcastInterval = 0.5f;

    // UGS (���� ����)
    public string ProjectId = "";
    public bool UseRelay = true;
}

public static class DiscoveryFactory // ���� �˻� ��ü ���� ���丮
{
    public static IDiscoverySystem Create(NetSettings s)
    {
        switch (s.Mode) // NetMode�� ���� ������ ISessionDiscovery ����ü ��ȯ
        {
            case NetMode.Lan:
                return new LanDiscovery(s.LanTimeoutMs, s.LanBroadcastInterval);

            /* �ʿ��� ������ ���� �Ǵ� UGS ���� �ڵ�(���߿� ����)
            case NetMode.UgsP2P:
                return new UgsDiscovery(
                    projectId: s.ProjectId,
                    proto: s.ProtoVersion,
                    gameVersion: s.GameVersion,
                    useRelay: true
                );

            case NetMode.UgsDS:
                return new UgsDedicatedDiscovery(
                    projectId: s.ProjectId,
                    proto: s.ProtoVersion,
                    gameVersion: s.GameVersion
                );
            */
            default:
                throw new NotSupportedException($"Unknown NetMode: {s.Mode}");

        }
    }
}

public class SessionManager : MonoBehaviour
{
    [SerializeField] private NetSettings settings; // ��Ʈ��ũ ����
    [SerializeField] private UnityTransport transport;
    [SerializeField] private float autoStartDelaySec = 3f; // �ο� ���� �� �ڵ� ���� ��� �ð�
    [SerializeField] private int _capacity; // ���� ���� �ִ� �ο�

    private IDiscoverySystem _discovery;   // ���� ���� �˻� ���(LANDiscovery, UGSDiscovery ��)
    private CancellationTokenSource _clientCts;   // Ŭ���̾�Ʈ ���� �۾� ��ҿ� ��ū
    private CancellationTokenSource _hostCts; // ȣ��Ʈ ���� �۾� ��ҿ� ��ū
    private CancellationTokenSource _broadcastCts; // ��ε�ĳ��Ʈ ���� �۾� ��ҿ� ��ū
    private bool _iAmHost;   // ���� ȣ��Ʈ ����

    private void Awake() { _discovery = DiscoveryFactory.Create(settings); }// ������ �´� IDiscoverySystem ����ü ���� 
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) // ESC Ű�� ���
        {
            if (_iAmHost) 
            {
               CancelHost();
            }
            else 
            {
                CancelClient();
            }
        }
    }
    // 1v1 ��ư Ŭ�� ��
    public async void OnClick_Local1v1() 
    {
        _clientCts = new CancellationTokenSource();
        _capacity = 2; // 1v1 ����

        try
        {
            var findSession = await _discovery.FindSessionAsync(_clientCts.Token); // ���� �˻� ����

            if (findSession.Validate("BGame", 1, _capacity))
            {
                Debug.Log($"ȣ��Ʈ�� �߰��߽��ϴ�! {findSession.Ip}:{findSession.Port}");
                await StartClientAsync(findSession.Ip, findSession.Port, _clientCts.Token);
                _iAmHost = false;
                return;
            }
        }
        catch (OperationCanceledException) { Debug.Log("�۾��� ��ҵǾ����ϴ�."); }
        catch (Exception e) { Debug.LogError($"���� �߻�: {e.Message}"); }

        // ȣ��Ʈ�� �߰ߵ��� ������ ȣ��Ʈ�� ��
        _clientCts?.Dispose();
        _clientCts = null;

        _hostCts = new CancellationTokenSource();
        try
        {
            Debug.Log("ȣ��Ʈ�� �߰����� ���Ͽ� ȣ��Ʈ�� �˴ϴ�....");
            await StartHostAsync(_hostCts.Token);
            _iAmHost = true;

            _broadcastCts = CancellationTokenSource.CreateLinkedTokenSource(_hostCts.Token); // ȣ��Ʈ ��ū�� ��ε�ĳ��Ʈ ��ū ��ũ
            _ = _discovery.StartSessionAsync(settings.LanPort, _capacity, _broadcastCts.Token); // ȣ��Ʈ�� ��ε�ĳ��Ʈ ����(��׶���� Task ����)

            bool gameStarting = false;

            _ = HostWaitRoomAndStartAsync(_hostCts.Token, onRoomFull: async () =>
            {
                if (gameStarting) return; // �ߺ� ����
                gameStarting = true;

                // ���� á�� �� ��ε� ĳ��Ʈ�� ����
                try { _broadcastCts?.Cancel(); } catch { }
                _broadcastCts?.Dispose();
                _broadcastCts = null;

                Debug.Log("2�� �� ������ ���� �˴ϴ�!");
                await Task.Delay(TimeSpan.FromSeconds(autoStartDelaySec), _hostCts.Token);
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("WaitScene", LoadSceneMode.Single); // �� ��ȯ
                }
            });
        }
        catch (OperationCanceledException) { Debug.Log("�۾��� ��ҵǾ����ϴ�."); }
        catch (Exception e) { Debug.LogError($"���� �߻�: {e.Message}"); }
    }

    // Ŭ���̾�Ʈ�� ����
    private async Task StartClientAsync(string hostIp, ushort port, CancellationToken ct)
    {
        // UTP ����
        transport.SetConnectionData(hostIp, port);
        var nm = NetworkManager.Singleton;

        // ���� ����/Ŭ���̾�Ʈ ������ ������ ����
        if (nm.IsServer || nm.IsClient) nm.Shutdown();

        bool ok = nm.StartClient(); // Ŭ���̾�Ʈ ����
        if (!ok) throw new Exception("Ŭ���̾�Ʈ�� ���� ����");

        // ���� �Ϸ� ���
        var tcs = new TaskCompletionSource<bool>();
        void OnClientConnected(ulong _) { tcs.TrySetResult(true); }
        nm.OnClientConnectedCallback += OnClientConnected;

        using (ct.Register(() => tcs.TrySetCanceled()))
        {
            await tcs.Task; // ������ ���� ������
        }
        nm.OnClientConnectedCallback -= OnClientConnected;

        Debug.Log("Ŭ���̾�Ʈ�� ���� �Ϸ�");
    }

    //ȣ��Ʈ�� ����
    private async Task StartHostAsync(CancellationToken ct)
    {
        // UTP ���� ����
        transport.SetConnectionData("0.0.0.0", settings.LanPort, "0.0.0.0");

        var nm = NetworkManager.Singleton;
        if (nm.IsServer || nm.IsClient) nm.Shutdown();

        bool ok = nm.StartHost(); // ȣ��Ʈ ����
        if (!ok) throw new Exception("ȣ��Ʈ�� ���� ����");

        // ȣ��Ʈ �غ� ���
        await Task.Yield();
        Debug.Log("ȣ��Ʈ �غ� �Ϸ�");
    }

    private async Task HostWaitRoomAndStartAsync(CancellationToken ct, Func<Task> onRoomFull)
    {
        var nm = NetworkManager.Singleton;

        void Check()
        {
            if (nm.ConnectedClientsList.Count >= _capacity) // ���� ����
            {
                 _ = onRoomFull(); 
            }
        }

        Action<ulong> onConnect = _ => Check();
        Action<ulong> onDisconnect = _ => Check();

        // ����/�������� �̺�Ʈ ���
        nm.OnClientConnectedCallback += onConnect;
        nm.OnClientDisconnectCallback += onDisconnect;

       try
       {
           Check(); // ȣ��Ʈ �ڽ� 1�� ���¿��� ���� üũ
           while (!ct.IsCancellationRequested)
                await Task.Delay(200, ct); // 0.2�� ���� + ��ҽ� ��� Ż��
       
       }
       catch (OperationCanceledException) { /* ���� ��� */ }
       finally
       {
           // �̺�Ʈ ����
           nm.OnClientConnectedCallback -= onConnect;
           nm.OnClientDisconnectCallback -= onDisconnect;
       }
    }

    private void CancelClient(){
        try { _clientCts?.Cancel(); } catch { }
        _clientCts?.Dispose();
        _clientCts = null;

        // Ŭ���̾�Ʈ�� ���ǿ� �پ��ִٸ� ����
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) 
        {
            NetworkManager.Singleton.Shutdown();
        }
        _iAmHost = false;
        Debug.Log("Ŭ���̾�Ʈ �۾��� ����߽��ϴ�.");
    }

    private void CancelHost(){
        try { _hostCts?.Cancel(); } catch { }
        _hostCts?.Dispose();
        _hostCts = null;

        try { _broadcastCts?.Cancel(); } catch { }
        _broadcastCts?.Dispose();
        _broadcastCts = null;

        // ȣ��Ʈ�� ���ǿ� �پ��ִٸ� ����
        if (NetworkManager.Singleton.IsServer) 
        {
            NetworkManager.Singleton.Shutdown();
        }

        _iAmHost = false;
        Debug.Log("ȣ��Ʈ �۾��� ����߽��ϴ�.");
    }
}
