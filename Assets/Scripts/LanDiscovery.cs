using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// 로컬 네트워크 환경용 호스트의 Ip와 Port를 자동으로 찾는 스크립트
public class LanDiscovery : IDiscoverySystem
{
    public const int DiscoveryPort = 47777; // 발견 전용 포트

    private readonly int _timeoutMs;            // 타임아웃 시간
    private readonly float _broadcastIntervalSec; // 브로드캐스트 주기

    public LanDiscovery(int timeoutMs = 1200, float broadcastIntervalSec = 2.0f) 
    {
        //SessionManager에서 넘겨오는 값, 없으면 기본 값
        _timeoutMs = timeoutMs; 
        _broadcastIntervalSec = broadcastIntervalSec;
    }

    //클라이언트가 호스트를 찾는 메서드
    public async Task<DiscoveryResult> FindSessionAsync(CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(DiscoveryPort); // 47777 포트를 열어 수신 대기

            // 부모 토큰과 결합된 토큰 소스 생성(부모 토큰이 취소되거나, 타임아웃 시 취소됨)
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(_timeoutMs);               // _timeoutMs 경과 시 자동 취소
            var cancelTask = Task.Delay(Timeout.Infinite, linked.Token); //외부 취소 대기

            // 타임 아웃 되기 전까지 반복 수신
            while (!linked.IsCancellationRequested)
            {
                var recvTask = udp.ReceiveAsync();        // 다음 UDP 패킷 수신을 비동기로 대기
                var recvOrCancel = await Task.WhenAny(recvTask, cancelTask); // 수신이 완료되거나 취소될 때까지 대기

                if (recvOrCancel == cancelTask) // 취소가 먼저 완료되면
                    return ct.IsCancellationRequested ? DiscoveryResult.Canceled() : DiscoveryResult.NotFound();

                var res = recvTask.Result;               // 수신 결과
                var json = Encoding.UTF8.GetString(res.Buffer).Trim(); // 바이트 -> 문자열(Trim으로 공백 제거)

                if (DiscoveryResult.TryDeserialize(json, out var dr)) // JSON 역직렬화 시도
                {
                    if (dr.Validate("BGame", 1)) // 유효성 검사
                    {
                        return dr; // 유효한 결과 반환
                    }
                    else
                    {
                        continue; // 유효하지 않으면 다음 수신 대기
                    } 
                }
                else 
                {
                    continue; // 역직렬화 실패 시 다음 수신 대기
                }
            }
            return DiscoveryResult.Canceled();           // 루프 탈출(취소) 시 취소 처리
        }
        catch (OperationCanceledException) { return DiscoveryResult.Canceled(); }        // 취소 예외 → 취소 결과 통일
        catch (SocketException se) { return DiscoveryResult.Fail($"Socket error: {se.SocketErrorCode}"); } // 네트워크 오류처리
        catch (Exception e) { return DiscoveryResult.Fail(e.Message); } // 그 외 모든 예외
    }

    // 호스트일 때 브로드캐스트
    public async Task StartSessionAsync(ushort gamePort, int capacity, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };  // 송신 전용 UDP 소켓
            var ep = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort); //브로드캐스트 대상 설정(전역 IP와 포트)
            var delay = TimeSpan.FromSeconds(_broadcastIntervalSec); // 전송 간격

            var payload = DiscoveryResult.Ok(
                tag: "BGame",
                proto: 1,
                source: "lan",
                ip: GetLocalIPv4OrLoopback(),
                port: gamePort,
                capacity: capacity,
                hostId: null,
                buildId: null,
                meta: "Prototype"
            );

            var json = payload.ToJson();                 // 객체 → JSON 문자열
            var data = Encoding.UTF8.GetBytes(json);     // 문자열 → 바이트

            while (!ct.IsCancellationRequested)          // 취소될 때까지 반복
            {
                try
                {
                    udp.Send(data, data.Length, ep);
                    Debug.Log("매칭 인원을 찾고 있습니다....");
                } // 브로드캐스트 전송(실패해도 다음 루프로)
                catch (SocketException ex)
                {
                    Debug.LogWarning($"[LanDiscovery] UDP send error: {ex.SocketErrorCode}"); // 네트워크 오류 경고(일시적 일 수 있으므로 무시)
                }
                await Task.Delay(delay, ct);     // 주기 대기(취소 가능 대기)
            }
        }
        catch (OperationCanceledException)
        {
            // 정상적인 취소 종료
        }
    }

    private static string GetLocalIPv4OrLoopback() // 로컬 IPv4 주소를 가져오는 메서드 
    {
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var ipProps = ni.GetIPProperties();
                foreach (var ua in ipProps.UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        return ua.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }
}
