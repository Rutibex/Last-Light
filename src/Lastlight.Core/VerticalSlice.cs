namespace Lastlight.Core;
public sealed class LevelLink
{
    public long OwnerId { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public bool Enabled { get; set; } = true;
}
public sealed class PreparedFormula
{
    public long Id { get; set; }
    public string FormulaId { get; set; } = "";
    public long PreparedAt { get; set; }
}
public sealed class FormulaProfile
{
    public long EntityId { get; set; }
    public int Skill { get; set; } = 5;
    public double Strain { get; set; }
    public HashSet<string> Known { get; set; } = [];
    public List<string> Queue { get; set; } = [];
    public List<PreparedFormula> Prepared { get; set; } = [];
    public int Capacity => 4+Skill/5;
}
public sealed class FormulaCast
{
    public double FailureChance {get;set;}
    public long ActorId { get; set; }
    public string FormulaId { get; set; } = "";
    public int Cell { get; set; }
    public long TargetId { get; set; }
    public long FinishAt { get; set; }
    public int OriginCell { get; set; }
}
public sealed class FormulaEffect
{
    public long Id { get; set; }
    public long ActorId { get; set; }
    public string Kind { get; set; } = "";
    public int Cell { get; set; }
    public int Radius { get; set; }
    public long ExpiresAt { get; set; }
    public long SourceId { get; set; }
    public int Destination { get; set; } = -1;
    public string CargoId { get; set; } = "";
    public int CargoCount { get; set; }
    public List<int> Path { get; set; } = [];
    public List<string> Claims { get; set; } = [];
    public bool PickedUp { get; set; }
}
public sealed class RuinState
{
    public long TabletId { get; set; }
    public long FolioId { get; set; }
    public long CacheId { get; set; }
    public long DoorId { get; set; }
    public long EngineId { get; set; }
    public long GuardianId { get; set; }
    public bool Discovered { get; set; }
    public bool ArchiveRead { get; set; }
    public bool CacheSurveyed { get; set; }
    public bool LensRecovered { get; set; }
    public long LensId { get; set; }
    public string EngineKnowledge { get; set; } = "Unknown";
    public bool EngineRestored { get; set; }
    public bool DefenseComplete { get; set; }
    public bool WardWarning { get; set; }
    public long WardAt { get; set; }
    public int GardenCell { get; set; } = -1;
}
public sealed class TutorialState
{
    public bool Enabled { get; set; } = true;
    public HashSet<string> Completed { get; set; } = [];
    public bool Dismissed { get; set; }
    public long StartedAt { get; set; }
    public long CompletedAt { get; set; }
}
public sealed record SliceNotice(long Id,long Tick,string Kind,string Text,int Cell);
public sealed class SliceState
{
    public bool Initialized { get; set; }
    public bool Enabled { get; set; }
    public long NextId { get; set; } = 1;
    public ulong MagicRandomState { get; set; }
    public List<LevelLink> Links { get; set; } = [];
    public HashSet<int> Roofs { get; set; } = [];
    public HashSet<int> BridgeFloors { get; set; } = [];
    public Dictionary<int,long> UnstableSince { get; set; } = new();
    public HashSet<int> Debris { get; set; } = [];
    public HashSet<long> Buried { get; set; } = [];
    public Dictionary<long,FormulaProfile> Formulae { get; set; } = new();
    public Dictionary<long,FormulaCast> Casts { get; set; } = new();
    public List<FormulaEffect> Effects { get; set; } = [];
    public RuinState Ruin { get; set; } = new();
    public TutorialState Tutorial { get; set; } = new();
    public List<SliceNotice> Notices { get; set; } = [];
    public HashSet<long> Hostiles { get; set; } = [];
    public int Collapses { get; set; }
    public int CompletedCasts { get; set; }
    public int FailedCasts { get; set; }
    public int PorterDeliveries { get; set; }
    public bool MusicEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
}
public sealed partial class World
{
    public SliceState Slice { get; set; } = new();
    public void EnsureSlice(Definitions d)
    {
        if(!Slice.Initialized){Slice.MagicRandomState=new StableRandom((ulong)(uint)Seed+404).State;Slice.Initialized=true;}
        foreach(long id in Colony.People.Keys)if(!Slice.Formulae.ContainsKey(id))Slice.Formulae[id]=new(){EntityId=id};
    }
    public string LevelName(int z)=>z switch{0=>"Surface",1=>"Observatory vault",2=>"Deep archive",3=>"Regional sites",_=>"Level "+z};
    public IEnumerable<int> HorizontalNeighbors(int i)
    {
        var c=Position(i);foreach(var (dx,dy) in new[]{(0,-1),(-1,0),(1,0),(0,1)})if(Contains(c.X+dx,c.Y+dy,c.Z))yield return Index(c.X+dx,c.Y+dy,c.Z);
    }
    private IEnumerable<int> VerticalNeighbors(int i)
    {
        foreach(var link in Slice.Links.Where(l=>l.Enabled && Find(l.OwnerId)?.DefinitionId=="stairs"))
        {if(link.A==i)yield return link.B;else if(link.B==i)yield return link.A;}
    }
    public bool Sealed(int cell)=>Slice.Effects.Any(e=>e.Kind=="Seal" && e.Cell==cell && e.ExpiresAt>Tick);
    public bool Traversable(int cell,Definitions d)=>cell>=0 && cell<Cells.Count && d.Terrain[Cells[cell].TerrainId].Walkable && WaterDepth(cell)<4 && !Entities.Any(e=>e.DefinitionId=="f-floodgate" && Index(e.X,e.Y,e.Z)==cell && !Physical.Devices.GetValueOrDefault(e.Id,new()).Open) && !Slice.Debris.Contains(cell) && !Sealed(cell) && !Entities.Any(e=>e.DefinitionId is "wall" or "fence" or "column" or "sealed-door" && BuildingCells(e).Contains(Position(cell)));
    public int SupportDistance(int cell,bool floor)
    {
        var p=Position(cell);int best=1000;
        for(int y=Math.Max(0,p.Y-5);y<=Math.Min(Height-1,p.Y+5);y++)for(int x=Math.Max(0,p.X-5);x<=Math.Min(Width-1,p.X+5);x++)
        {
            int at=Index(x,y,p.Z);bool anchor=floor?Cells[at].TerrainId is "soil" or "sand" or "stone" || Cells[at].TerrainId=="floor" && !Slice.BridgeFloors.Contains(at):Cells[at].TerrainId=="stone";
            if(anchor)best=Math.Min(best,Math.Abs(x-p.X)+Math.Abs(y-p.Y));
        }
        foreach(var e in Entities.Where(e=>e.Z==p.Z && e.DefinitionId is "wall" or "column"))best=Math.Min(best,Math.Abs(e.X-p.X)+Math.Abs(e.Y-p.Y));
        return best;
    }
    public bool Supported(int cell,bool floor)=>SupportDistance(cell,floor)<=(floor?3:4);
    private void Notice(string kind,string text,int cell)
    {
        Slice.Notices.Add(new(Slice.NextId++,Tick,kind,text,cell));if(Slice.Notices.Count>128)Slice.Notices.RemoveAt(0);Record(text);
    }
    private void AdvanceSupport(Definitions d)
    {
        if(!Slice.Enabled || Tick%300!=0)return;
        var unstable=Slice.Roofs.Where(i=>!Supported(i,false)).Concat(Slice.BridgeFloors.Where(i=>!Supported(i,true))).Distinct().Order().ToArray();
        foreach(int cell in Slice.UnstableSince.Keys.Where(i=>!unstable.Contains(i)).ToArray())Slice.UnstableSince.Remove(cell);
        foreach(int cell in unstable)
        {
            if(!Slice.UnstableSince.TryGetValue(cell,out long since)){Slice.UnstableSince[cell]=Tick;Danger.PauseSerial++;Notice("Support","An unsupported span is cracking; support it within two game hours.",cell);continue;}
            if(Tick-since>=TicksPerHour*2)Collapse(cell,d);
        }
    }
    public void Collapse(int cell,Definitions d)
    {
        bool bridge=Slice.BridgeFloors.Remove(cell);Slice.Roofs.Remove(cell);Slice.UnstableSince.Remove(cell);Slice.Collapses++;var pos=Position(cell);
        if(bridge)
        {
            foreach(var e in Entities.Where(e=>!IsAway(e.Id)&&Index(e.X,e.Y,e.Z)==cell).ToArray())
            {
                if(d.Entities[e.DefinitionId].Kind==EntityKind.Building){Apply(new(CommandKind.Remove,e.Id),d);continue;}
                int below=pos.Z+1<Levels?Index(pos.X,pos.Y,pos.Z+1):-1;
                if(below>=0 && Traversable(below,d)){MoveActor(e,below);if(Body(e.Id)!=null)InflictWound(e.Id,"left-leg","Fracture",18,0,d);}
                else
                {
                    int safe=NearestTerrainCell(cell,d);if(safe!=cell){MoveActor(e,safe);if(Body(e.Id)!=null)InflictWound(e.Id,"torso","Bruise",15,0,d);}
                }
            }
            if(Entities.Any(e=>Index(e.X,e.Y,e.Z)==cell)){Cells[cell].TerrainId="rubble";Slice.Debris.Add(cell);}else Cells[cell].TerrainId="void";
        }
        else
        {
            Cells[cell].TerrainId="rubble";Slice.Debris.Add(cell);
            foreach(var e in Entities.Where(e=>!IsAway(e.Id)&&Index(e.X,e.Y,e.Z)==cell).ToArray())if(Body(e.Id)!=null){InflictWound(e.Id,"left-leg","Fracture",18,0,d);Slice.Buried.Add(e.Id);Interrupt(e.Id,d);}
        }
        Danger.PauseSerial++;Notice("Collapse","A span collapsed, leaving dangerous debris.",cell);
    }
    private int NearestTerrainCell(int start,Definitions d)
    {
        var queue=new Queue<int>();queue.Enqueue(start);var seen=new HashSet<int>{start};
        while(queue.Count>0){int at=queue.Dequeue();foreach(int next in HorizontalNeighbors(at)){if(!seen.Add(next))continue;if(Traversable(next,d))return next;queue.Enqueue(next);}}return start;
    }
    public double LightAt(int cell)
    {
        if(!Slice.Enabled)return 100;var p=Position(cell);double light=p.Z==0?NaturalSolarLight():p.Z==1?20:8;
        foreach(var e in Slice.Effects.Where(e=>e.Kind=="Light" && e.ExpiresAt>Tick))if(Position(e.Cell).Z==p.Z && Math.Abs(Position(e.Cell).X-p.X)+Math.Abs(Position(e.Cell).Y-p.Y)<=e.Radius)light=Math.Max(light,80);
        foreach(var lamp in Entities.Where(e=>e.DefinitionId=="sunlamp" && e.Z==p.Z))if(DevicePowered(lamp.Id) && Math.Abs(lamp.X-p.X)+Math.Abs(lamp.Y-p.Y)<=4)light=Math.Max(light,70);
        var engine=Find(Slice.Ruin.EngineId);
        if(Slice.Ruin.EngineRestored && engine!=null && engine.Z==p.Z && Math.Abs(engine.X-p.X)+Math.Abs(engine.Y-p.Y)<=14)light=Math.Max(light,85);
        return light;
    }
    public double WarmthAt(int cell)
    {
        var p=Position(cell);double warmth=p.Z==0?SolarAmbient(14):p.Z==1?4:-4;
        if(LightAt(cell)>=80 && p.Z>0)warmth+=12;return warmth;
    }
    public bool CropReady(CropState crop)=>Slice.Enabled||Physical.Enabled?crop.GrowthTicks>=2*TicksPerDay:Tick-crop.PlantedAt>=2*TicksPerDay;
    private void AdvanceSlice(Definitions d)
    {
        EnsureSlice(d);if(!Slice.Enabled)return;AdvanceSupport(d);AdvanceFormulae(d);AdvanceRuin(d);AdvanceTutorial(d);
        foreach(var crop in Autonomy.Crops.Values)if(CropFailure(crop)=="" && LightAt(crop.Cell)>=(crop.OutputId=="fungus"?5:60))crop.GrowthTicks+=SolarCropTicks(crop);
    }
    public void RemoveSliceEntity(long id,Definitions d)
    {
        Slice.Links.RemoveAll(l=>l.OwnerId==id);Slice.Hostiles.Remove(id);Slice.Buried.Remove(id);Slice.Formulae.Remove(id);Slice.Casts.Remove(id);
        foreach(var effect in Slice.Effects.Where(e=>e.ActorId==id).ToArray())EndEffect(effect,d);
    }
    public IEnumerable<ColonyAlert> SliceAlerts()
    {
        if(!Slice.Enabled)yield break;
        if(Slice.UnstableSince.Count>0)yield return new("support",$"{Slice.UnstableSince.Count} unsafe spans","Open Levels for support distances and collapse countdowns.","Levels");
        if(Slice.Buried.Count>0)yield return new("buried","Residents trapped by debris","Designate Clear on debris; doctors can stabilize nearby patients.","Levels");
        if(Slice.Ruin.WardWarning)yield return new("ward","Observatory warder awakening","Prepare defenders or use the doorway to control the approach.","Ruin");
        if(Slice.Tutorial.Enabled && !Slice.Tutorial.Dismissed && Slice.Tutorial.CompletedAt==0)yield return new("tutorial","Observatory refuge tutorial","Open Tutorial for the next objective and context.","Tutorial");
    }
}
