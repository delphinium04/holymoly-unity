using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// ���� ��Ʈ��ũ ȯ��� ȣ��Ʈ�� Ip�� Port�� �ڵ����� ã�� ��ũ��Ʈ
public class LanDiscovery : IDiscoverySystem
{
    public const int DiscoveryPort = 47777; // �߰� ���� ��Ʈ

    private readonly int _timeoutMs;            // Ÿ�Ӿƿ� �ð�
    private readonly float _broadcastIntervalSec; // ��ε�ĳ��Ʈ �ֱ�

    public LanDiscovery(int timeoutMs = 1200, float broadcastIntervalSec = 2.0f) 
    {
        //SessionManager���� �Ѱܿ��� ��, ������ �⺻ ��
        _timeoutMs = timeoutMs; 
        _broadcastIntervalSec = broadcastIntervalSec;
    }

    //Ŭ���̾�Ʈ�� ȣ��Ʈ�� ã�� �޼���
    public async Task<DiscoveryResult> FindSessionAsync(CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(DiscoveryPort); // 47777 ��Ʈ�� ���� ���� ���

            // �θ� ��ū�� ���յ� ��ū �ҽ� ����(�θ� ��ū�� ��ҵǰų�, Ÿ�Ӿƿ� �� ��ҵ�)
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(_timeoutMs);               // _timeoutMs ��� �� �ڵ� ���
            var cancelTask = Task.Delay(Timeout.Infinite, linked.Token); //�ܺ� ��� ���

            // Ÿ�� �ƿ� �Ǳ� ������ �ݺ� ����
            while (!linked.IsCancellationRequested)
            {
                var recvTask = udp.ReceiveAsync();        // ���� UDP ��Ŷ ������ �񵿱�� ���
                var recvOrCancel = await Task.WhenAny(recvTask, cancelTask); // ������ �Ϸ�ǰų� ��ҵ� ������ ���

                if (recvOrCancel == cancelTask) // ��Ұ� ���� �Ϸ�Ǹ�
                    return ct.IsCancellationRequested ? DiscoveryResult.Canceled() : DiscoveryResult.NotFound();

                var res = recvTask.Result;               // ���� ���
                var json = Encoding.UTF8.GetString(res.Buffer).Trim(); // ����Ʈ -> ���ڿ�(Trim���� ���� ����)

                if (DiscoveryResult.TryDeserialize(json, out var dr)) // JSON ������ȭ �õ�
                {
                    if (dr.Validate("BGame", 1)) // ��ȿ�� �˻�
                    {
                        return dr; // ��ȿ�� ��� ��ȯ
                    }
                    else
                    {
                        continue; // ��ȿ���� ������ ���� ���� ���
                    } 
                }
                else 
                {
                    continue; // ������ȭ ���� �� ���� ���� ���
                }
            }
            return DiscoveryResult.Canceled();           // ���� Ż��(���) �� ��� ó��
        }
        catch (OperationCanceledException) { return DiscoveryResult.Canceled(); }        // ��� ���� �� ��� ��� ����
        catch (SocketException se) { return DiscoveryResult.Fail($"Socket error: {se.SocketErrorCode}"); } // ��Ʈ��ũ ����ó��
        catch (Exception e) { return DiscoveryResult.Fail(e.Message); } // �� �� ��� ����
    }

    // ȣ��Ʈ�� �� ��ε�ĳ��Ʈ
    public async Task StartSessionAsync(ushort gamePort, int capacity, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };  // �۽� ���� UDP ����
            var ep = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort); //��ε�ĳ��Ʈ ��� ����(���� IP�� ��Ʈ)
            var delay = TimeSpan.FromSeconds(_broadcastIntervalSec); // ���� ����

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

            var json = payload.ToJson();                 // ��ü �� JSON ���ڿ�
            var data = Encoding.UTF8.GetBytes(json);     // ���ڿ� �� ����Ʈ

            while (!ct.IsCancellationRequested)          // ��ҵ� ������ �ݺ�
            {
                try
                {
                    udp.Send(data, data.Length, ep);
                    Debug.Log("��Ī �ο��� ã�� �ֽ��ϴ�....");
                } // ��ε�ĳ��Ʈ ����(�����ص� ���� ������)
                catch (SocketException ex)
                {
                    Debug.LogWarning($"[LanDiscovery] UDP send error: {ex.SocketErrorCode}"); // ��Ʈ��ũ ���� ���(�Ͻ��� �� �� �����Ƿ� ����)
                }
                await Task.Delay(delay, ct);     // �ֱ� ���(��� ���� ���)
            }
        }
        catch (OperationCanceledException)
        {
            // �������� ��� ����
        }
    }

    private static string GetLocalIPv4OrLoopback() // ���� IPv4 �ּҸ� �������� �޼��� 
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
