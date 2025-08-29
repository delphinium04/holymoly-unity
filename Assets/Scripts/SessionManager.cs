using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetMode
{
    Lan,        // LAN 브로드캐스트로 연결
    UgsP2P,     // UGS Lobby/Relay로 연결(호스트 존재)
    UgsDS       // UGS 및 전용 서버로 연결(호스트 없음)
}

[Serializable]
public class NetSettings
{
    public NetMode Mode = NetMode.Lan; //현재 LAN만 구현

    // LAN
    public ushort LanPort = 7777;
    public int LanTimeoutMs = 1500;
    public float LanBroadcastInterval = 0.5f;

    // UGS (나중 구현)
    public string ProjectId = "";
    public bool UseRelay = true;
}

public static class DiscoveryFactory // 세션 검색 객체 생성 팩토리
{
    public static IDiscoverySystem Create(NetSettings s)
    {
        switch (s.Mode) // NetMode에 따라 적절한 ISessionDiscovery 구현체 반환
        {
            case NetMode.Lan:
                return new LanDiscovery(s.LanTimeoutMs, s.LanBroadcastInterval);

            /* 필요할 것으로 예상 되는 UGS 관련 코드(나중에 구현)
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
    [SerializeField] private NetSettings settings; // 네트워크 설정
    [SerializeField] private UnityTransport transport;
    [SerializeField] private float autoStartDelaySec = 3f; // 인원 충족 시 자동 시작 대기 시간
    [SerializeField] private int _capacity; // 현재 세션 최대 인원

    private IDiscoverySystem _discovery;   // 사용될 세션 검색 방법(LANDiscovery, UGSDiscovery 등)
    private CancellationTokenSource _clientCts;   // 클라이언트 관련 작업 취소용 토큰
    private CancellationTokenSource _hostCts; // 호스트 관련 작업 취소용 토큰
    private CancellationTokenSource _broadcastCts; // 브로드캐스트 관련 작업 취소용 토큰
    private bool _iAmHost;   // 현재 호스트 여부

    private void Awake() { _discovery = DiscoveryFactory.Create(settings); }// 설정에 맞는 IDiscoverySystem 구현체 생성 
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) // ESC 키로 취소
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
    // 1v1 버튼 클릭 시
    public async void OnClick_Local1v1() 
    {
        _clientCts = new CancellationTokenSource();
        _capacity = 2; // 1v1 설정

        try
        {
            var findSession = await _discovery.FindSessionAsync(_clientCts.Token); // 세션 검색 시작

            if (findSession.Validate("BGame", 1, _capacity))
            {
                Debug.Log($"호스트를 발견했습니다! {findSession.Ip}:{findSession.Port}");
                await StartClientAsync(findSession.Ip, findSession.Port, _clientCts.Token);
                _iAmHost = false;
                return;
            }
        }
        catch (OperationCanceledException) { Debug.Log("작업이 취소되었습니다."); }
        catch (Exception e) { Debug.LogError($"오류 발생: {e.Message}"); }

        // 호스트가 발견되지 않으면 호스트가 됨
        _clientCts?.Dispose();
        _clientCts = null;

        _hostCts = new CancellationTokenSource();
        try
        {
            Debug.Log("호스트를 발견하지 못하여 호스트가 됩니다....");
            await StartHostAsync(_hostCts.Token);
            _iAmHost = true;

            _broadcastCts = CancellationTokenSource.CreateLinkedTokenSource(_hostCts.Token); // 호스트 토큰과 브로드캐스트 토큰 링크
            _ = _discovery.StartSessionAsync(settings.LanPort, _capacity, _broadcastCts.Token); // 호스트로 브로드캐스트 시작(백그라운드로 Task 무시)

            bool gameStarting = false;

            _ = HostWaitRoomAndStartAsync(_hostCts.Token, onRoomFull: async () =>
            {
                if (gameStarting) return; // 중복 방지
                gameStarting = true;

                // 정원 찼을 때 브로드 캐스트만 멈춤
                try { _broadcastCts?.Cancel(); } catch { }
                _broadcastCts?.Dispose();
                _broadcastCts = null;

                Debug.Log("2초 후 게임이 시작 됩니다!");
                await Task.Delay(TimeSpan.FromSeconds(autoStartDelaySec), _hostCts.Token);
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene("WaitScene", LoadSceneMode.Single); // 씬 전환
                }
            });
        }
        catch (OperationCanceledException) { Debug.Log("작업이 취소되었습니다."); }
        catch (Exception e) { Debug.LogError($"오류 발생: {e.Message}"); }
    }

    // 클라이언트로 시작
    private async Task StartClientAsync(string hostIp, ushort port, CancellationToken ct)
    {
        // UTP 설정
        transport.SetConnectionData(hostIp, port);
        var nm = NetworkManager.Singleton;

        // 이전 서버/클라이언트 전적이 있으면 종료
        if (nm.IsServer || nm.IsClient) nm.Shutdown();

        bool ok = nm.StartClient(); // 클라이언트 시작
        if (!ok) throw new Exception("클라이언트로 접속 실패");

        // 접속 완료 대기
        var tcs = new TaskCompletionSource<bool>();
        void OnClientConnected(ulong _) { tcs.TrySetResult(true); }
        nm.OnClientConnectedCallback += OnClientConnected;

        using (ct.Register(() => tcs.TrySetCanceled()))
        {
            await tcs.Task; // 서버에 붙을 때까지
        }
        nm.OnClientConnectedCallback -= OnClientConnected;

        Debug.Log("클라이언트로 접속 완료");
    }

    //호스트로 시작
    private async Task StartHostAsync(CancellationToken ct)
    {
        // UTP 리슨 설정
        transport.SetConnectionData("0.0.0.0", settings.LanPort, "0.0.0.0");

        var nm = NetworkManager.Singleton;
        if (nm.IsServer || nm.IsClient) nm.Shutdown();

        bool ok = nm.StartHost(); // 호스트 시작
        if (!ok) throw new Exception("호스트로 시작 실패");

        // 호스트 준비 대기
        await Task.Yield();
        Debug.Log("호스트 준비 완료");
    }

    private async Task HostWaitRoomAndStartAsync(CancellationToken ct, Func<Task> onRoomFull)
    {
        var nm = NetworkManager.Singleton;

        void Check()
        {
            if (nm.ConnectedClientsList.Count >= _capacity) // 정원 도달
            {
                 _ = onRoomFull(); 
            }
        }

        Action<ulong> onConnect = _ => Check();
        Action<ulong> onDisconnect = _ => Check();

        // 접속/접속해제 이벤트 등록
        nm.OnClientConnectedCallback += onConnect;
        nm.OnClientDisconnectCallback += onDisconnect;

       try
       {
           Check(); // 호스트 자신 1명 상태에서 최초 체크
           while (!ct.IsCancellationRequested)
                await Task.Delay(200, ct); // 0.2초 폴링 + 취소시 즉시 탈출
       
       }
       catch (OperationCanceledException) { /* 정상 취소 */ }
       finally
       {
           // 이벤트 해제
           nm.OnClientConnectedCallback -= onConnect;
           nm.OnClientDisconnectCallback -= onDisconnect;
       }
    }

    private void CancelClient(){
        try { _clientCts?.Cancel(); } catch { }
        _clientCts?.Dispose();
        _clientCts = null;

        // 클라이언트가 세션에 붙어있다면 종료
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) 
        {
            NetworkManager.Singleton.Shutdown();
        }
        _iAmHost = false;
        Debug.Log("클라이언트 작업을 취소했습니다.");
    }

    private void CancelHost(){
        try { _hostCts?.Cancel(); } catch { }
        _hostCts?.Dispose();
        _hostCts = null;

        try { _broadcastCts?.Cancel(); } catch { }
        _broadcastCts?.Dispose();
        _broadcastCts = null;

        // 호스트가 세션에 붙어있다면 종료
        if (NetworkManager.Singleton.IsServer) 
        {
            NetworkManager.Singleton.Shutdown();
        }

        _iAmHost = false;
        Debug.Log("호스트 작업을 취소했습니다.");
    }
}
