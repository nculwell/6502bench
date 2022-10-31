using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Evt
{
    public EvtType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public uint V { get; init; }
    public uint W { get; init; }
    public ulong TimeDue { get; internal set; }

    string GetPayload()
    {
        JObject jo = new();
        jo["x"] = X;
        jo["y"] = Y;
        jo["v"] = V;
        jo["w"] = W;
        return JsonConvert.SerializeObject(jo);
    }
    public Evt(EvtType et, string payloadJson)
    {
        Type = et;
        var payload = JsonConvert.DeserializeObject<JObject>(payloadJson);
        if (payload != null)
        {
            X = (int?)payload["x"] ?? 0;
            Y = (int?)payload["y"] ?? 0;
            V = (uint?)payload["v"] ?? 0;
            W = (uint?)payload["w"] ?? 0;
        }
    }
    public Evt(EvtType et, int x, int y, uint v, uint w)
    {
        Type = et; X = x; Y = y; V = v; W = w;
    }
}
