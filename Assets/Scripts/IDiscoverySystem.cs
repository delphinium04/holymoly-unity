using System.Threading;
using System.Threading.Tasks;

public interface IDiscoverySystem
{
    // Ŭ���̾�Ʈ�� ȣ��Ʈ�� ã�� �޼���
    Task<DiscoveryResult> FindSessionAsync(CancellationToken ct);

    // ȣ��Ʈ ��ε�ĳ��Ʈ�� �����ϴ� �޼���
    Task StartSessionAsync(ushort gamePort, int capacity, CancellationToken ct);
}
