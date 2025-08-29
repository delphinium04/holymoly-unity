using System;
using Newtonsoft.Json;

//Discovery의 결과를 나타내는 구조체
//현 버전 JSON 프로토콜은 (tag, proto_version, source, Status, Ip, Port, capacity, Error, HostId, BuildId, Meta)순서 로 구성
public enum DiscoveryStatus { Success, NotFound, Canceled, Error }

public class DiscoveryResult
{
    //식별/버전/소스 관련 필드
    [JsonProperty("tag")] public string Tag { get; set; }           // 게임 식별 태그
    [JsonProperty("proto_version")] public int ProtoVersion { get; set; }      // JSON 프로토콜 버전 (json 프로토콜 수정 시 버전 관리용)
    [JsonProperty("source")] public string Source { get; set; }     // 로컬, 호스트 UGS, 전용 서버 UGS 등 소스 구분

    //기본 필드
    [JsonProperty("status")] public DiscoveryStatus Status { get; set; }
    [JsonProperty("ip")] public string Ip { get; set; }
    [JsonProperty("port")] public ushort Port { get; set; }
    [JsonProperty("capacity")] public int Capacity { get; set; } //게임 최대 인원
    [JsonProperty("error")] public string Error { get; set; }

    //확장 필드(UGS/전용 서버 용)
    [JsonProperty("host_id")] public string HostId { get; set; } // LobbyId, UGS의 경우 HostId
    [JsonProperty("build_id")] public string BuildId { get; set; } //빌드/버전 정보
    [JsonProperty("meta")] public string Meta { get; set; } // 모드, 맵 등 추가 정보

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

    //역직렬화 메서드(JSON -> DiscoveryResult 객체)
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

    //DiscoveryResult의 유효성을 검사하는 메서드
    public bool Validate(string expectedTag = "BGame", int minProtoVersion = 1, int minCap = 2, int maxCap = 8)
    {
        if (!string.Equals(Tag, expectedTag, StringComparison.Ordinal)) return false; // 게임 식별 태그 검사
        if (ProtoVersion < minProtoVersion) return false; // 프로토콜 버전 검사
        if (Status == DiscoveryStatus.Success)
        {
            if (string.IsNullOrWhiteSpace(Ip) || Port == 0) return false;
            if (ProtoVersion >= 1)
            {
                if (Capacity < minCap || Capacity > maxCap) return false;
            }

            return true; // 모든 검사 통과 시 정상
        }
        return false; // Success 상태가 아니면 실패
    }
}
