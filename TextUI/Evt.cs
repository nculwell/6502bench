using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Evt
{
    public EvtType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public uint V { get; init; }
    public uint W { get; init; }
    public ulong TimeDue { get; set; }

    public Evt(EvtType et, int x = 0, int y = 0, uint v = 0, uint w = 0)
    {
        Type = et; X = x; Y = y; V = v; W = w;
    }

}
