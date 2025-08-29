using System.Threading;
using System.Threading.Tasks;

public interface IDiscoverySystem
{
    // 클라이언트가 호스트를 찾는 메서드
    Task<DiscoveryResult> FindSessionAsync(CancellationToken ct);

    // 호스트 브로드캐스트를 시작하는 메서드
    Task StartSessionAsync(ushort gamePort, int capacity, CancellationToken ct);
}
