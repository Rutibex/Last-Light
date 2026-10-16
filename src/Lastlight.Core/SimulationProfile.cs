using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Lastlight.Core;

public sealed class SimulationProfile
{
    public sealed class Measurement
    {
        public long Calls {get;set;}
        public long Elapsed {get;set;}
        public long Maximum {get;set;}
        public double TotalMilliseconds => Elapsed * 1000.0 / Stopwatch.Frequency;
        public double MaximumMilliseconds => Maximum * 1000.0 / Stopwatch.Frequency;
    }
    public Dictionary<string,Measurement> Systems {get;} = new(StringComparer.Ordinal);
    public void Measure(string name,Action advance)
    {
        long start=Stopwatch.GetTimestamp();advance();long elapsed=Stopwatch.GetTimestamp()-start;
        if(!Systems.TryGetValue(name,out var m))Systems.Add(name,m=new());
        m.Calls++;m.Elapsed+=elapsed;m.Maximum=Math.Max(m.Maximum,elapsed);
    }
}
public sealed partial class World
{
    // Diagnostics neither affect saved state nor random streams.
    [JsonIgnore] public SimulationProfile? Profile {get;set;}
    private void AdvanceProfiled(Definitions d)
    {
        var p=Profile!;
        p.Measure("Solar",()=>AdvanceSolar(d));p.Measure("Institutions",()=>AdvanceInstitutions(d));
        p.Measure("Region",()=>AdvanceRegion(d));p.Measure("Physical",()=>AdvancePhysical(d));
        p.Measure("Slice",()=>AdvanceSlice(d));p.Measure("People",()=>AdvancePeopleAndDanger(d));
        p.Measure("Colony",()=>AdvanceColony(d));
    }
}
