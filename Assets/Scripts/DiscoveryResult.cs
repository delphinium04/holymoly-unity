using System;
using Newtonsoft.Json;

//Discovery�� ����� ��Ÿ���� ����ü
//�� ���� JSON ���������� (tag, proto_version, source, Status, Ip, Port, capacity, Error, HostId, BuildId, Meta)���� �� ����
public enum DiscoveryStatus { Success, NotFound, Canceled, Error }

public class DiscoveryResult
{
    //�ĺ�/����/�ҽ� ���� �ʵ�
    [JsonProperty("tag")] public string Tag { get; set; }           // ���� �ĺ� �±�
    [JsonProperty("proto_version")] public int ProtoVersion { get; set; }      // JSON �������� ���� (json �������� ���� �� ���� ������)
    [JsonProperty("source")] public string Source { get; set; }     // ����, ȣ��Ʈ UGS, ���� ���� UGS �� �ҽ� ����

    //�⺻ �ʵ�
    [JsonProperty("status")] public DiscoveryStatus Status { get; set; }
    [JsonProperty("ip")] public string Ip { get; set; }
    [JsonProperty("port")] public ushort Port { get; set; }
    [JsonProperty("capacity")] public int Capacity { get; set; } //���� �ִ� �ο�
    [JsonProperty("error")] public string Error { get; set; }

    //Ȯ�� �ʵ�(UGS/���� ���� ��)
    [JsonProperty("host_id")] public string HostId { get; set; } // LobbyId, UGS�� ��� HostId
    [JsonProperty("build_id")] public string BuildId { get; set; } //����/���� ����
    [JsonProperty("meta")] public string Meta { get; set; } // ���, �� �� �߰� ����

    [JsonIgnore] public bool IsSuccess => Status == DiscoveryStatus.Success;

    public string ToJson()
       => JsonConvert.SerializeObject(this, Formatting.None);

    public static DiscoveryResult Ok(string tag, int proto, string source, string ip, ushort port, int capacity, string hostId = null, string buildId = null, string meta = null)
        => new() { Tag = tag, ProtoVersion = proto, Source = source,
           Status = DiscoveryStatus.Success, Ip = ip, Port = port, Capacity = capacity,
           HostId = hostId, BuildId = buildId, Meta = meta };

    public static DiscoveryResult NotFound(string meta = null)
        => new() { Status = DiscoveryStatus.NotFound, Meta = meta };

    public static DiscoveryResult Canceled()
        => new() { Status = DiscoveryStatus.Canceled };

    public static DiscoveryResult Fail(string error)
        => new() { Status = DiscoveryStatus.Error, Error = error };

    //������ȭ �޼���(JSON -> DiscoveryResult ��ü)
    public static bool TryDeserialize(string json, out DiscoveryResult result)
    {
        try
        {
            result = JsonConvert.DeserializeObject<DiscoveryResult>(json);
            return result != null;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    //DiscoveryResult�� ��ȿ���� �˻��ϴ� �޼���
    public bool Validate(string expectedTag = "BGame", int minProtoVersion = 1, int minCap = 2, int maxCap = 8)
    {
        if (!string.Equals(Tag, expectedTag, StringComparison.Ordinal)) return false; // ���� �ĺ� �±� �˻�
        if (ProtoVersion < minProtoVersion) return false; // �������� ���� �˻�
        if (Status == DiscoveryStatus.Success)
        {
            if (string.IsNullOrWhiteSpace(Ip) || Port == 0) return false;
            if (ProtoVersion >= 1)
            {
                if (Capacity < minCap || Capacity > maxCap) return false;
            }

            return true; // ��� �˻� ��� �� ����
        }
        return false; // Success ���°� �ƴϸ� ����
    }
}
