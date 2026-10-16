using Lastlight.Core;
using System.Text.Json;
public static class SliceEdges
{
    public static void Run(Definitions d,string output,string assets)
    {
        Directory.CreateDirectory(output);
        void Require(bool v,string m){if(!v)throw new Exception(m);}
        void Steps(World w,int n){for(int i=0;i<n;i++)w.Step(d);}
        var w=VerticalWorldGenerator.Create(d,41);var ids=w.Colony.People.Keys.ToArray();long caster=ids[0];foreach(var p in w.Colony.People.Values)p.Drafted=true;
        var actor=w.Find(caster)!;actor.X=61;actor.Y=64;var magic=w.Slice.Formulae[caster];magic.Skill=20;magic.Known.Add("porter");magic.Prepared.Add(new(){Id=w.Slice.NextId++,FormulaId="porter",PreparedAt=w.Tick});w.Slice.MagicRandomState=123456;
        var source=w.Apply(new(CommandKind.Spawn,DefinitionId:"brass",X:60,Y:64,Quantity:20),d);Require(source.Accepted,source.Message);int stock=w.Resources("brass").Total;
        var result=w.Apply(new(CommandKind.CastFormula,caster,"porter",TargetId:source.EntityId),d);Require(result.Accepted,result.Message);
        for(int i=0;i<2000 && !w.Slice.Effects.Any(e=>e.Kind=="Porter" && e.PickedUp);i++)w.Step(d);
        var porter=w.Slice.Effects.FirstOrDefault(e=>e.Kind=="Porter");Require(porter is {CargoCount:15,PickedUp:true},"Porter fixture did not pick up bounded cargo.");
        string path=Path.Combine(output,"porter-in-transit.lastlight");SaveStore.Write(path,w,new ViewState{CameraX=64,CameraY=64},d);var loaded=SaveStore.Read(path,d).World;
        Require(loaded.Slice.Effects.Single().CargoCount==15 && loaded.Slice.Formulae[caster].Prepared.Count==0,"Porter reload lost cargo or restored charge.");
        porter!.ExpiresAt=w.Tick+30;loaded.Slice.Effects.Single().ExpiresAt=loaded.Tick+30;Steps(w,30);Steps(loaded,30);
        Require(w.Slice.Effects.Count==0 && w.Resources("brass").Total==stock && SaveStore.Fingerprint(w)==SaveStore.Fingerprint(loaded),"Expired porter lost/duplicated cargo or diverged.");
        Console.WriteLine("PASS E03 edge: in-transit porter claims/cargo reload and expiry refund conserve every item");
        var stairs=VerticalWorldGenerator.Create(d,43);long worker=stairs.Colony.People.Keys.First();foreach(var p in stairs.Colony.People.Values)p.Drafted=true;
        stairs.Slice.Links[0].Enabled=true;var route=stairs.Apply(new(CommandKind.MoveDrafted,worker,X:73,Y:59,Z:1),d);Require(route.Accepted,route.Message);
        stairs.Apply(new(CommandKind.Remove,stairs.Slice.Links[0].OwnerId),d);Steps(stairs,1200);Require(stairs.Find(worker)!.Z==0 && stairs.Slice.Links.Count==1,"Removed stairs allowed cached vertical travel.");stairs.Validate(d);
        Console.WriteLine("PASS E01 edge: removing stairs invalidates queued cross-elevation travel");
        var collapse=VerticalWorldGenerator.Create(d,47);ids=collapse.Colony.People.Keys.ToArray();long victim=ids[0],builder=ids[1];foreach(var p in collapse.Colony.People.Values)p.Drafted=true;
        int cell=collapse.Index(64,64,0);collapse.Slice.Roofs.Add(cell);for(int y=59;y<=69;y++)for(int x=59;x<=69;x++)collapse.CellAt(x,y,0).TerrainId="soil";
        foreach(var e in collapse.Entities.Where(e=>e.DefinitionId is "wall" or "column" && Math.Abs(e.X-64)+Math.Abs(e.Y-64)<=4).ToArray())collapse.Apply(new(CommandKind.Remove,e.Id),d);
        collapse.Find(victim)!.X=64;collapse.Find(victim)!.Y=64;collapse.Find(builder)!.X=63;collapse.Find(builder)!.Y=64;
        Steps(collapse,2400);Require(collapse.Slice.Buried.Contains(victim) && collapse.Body(victim)!.Wounds.Any(x=>x.Kind=="Fracture"),"Collapse did not injure/bury resident.");
        collapse.Apply(new(CommandKind.Draft,builder,Quantity:0),d);Array.Fill(collapse.Colony.People[builder].Schedule,ScheduleActivity.Work);for(int i=0;i<collapse.Colony.People[builder].Priorities.Length;i++)collapse.Colony.People[builder].Priorities[i]=0;collapse.Colony.People[builder].Priorities[8]=1;
        result=collapse.Apply(new(CommandKind.Designate,DefinitionId:"Clear",X:64,Y:64),d);Require(result.Accepted,result.Message);
        for(int i=0;i<9000 && collapse.Slice.Debris.Contains(cell);i++)collapse.Step(d);
        Require(!collapse.Slice.Debris.Contains(cell) && !collapse.Slice.Buried.Contains(victim) && collapse.Traversable(cell,d),"Clear work did not release buried resident.");collapse.Validate(d);
        Console.WriteLine("PASS E02 edge: warned collapse injures/buries resident; physical Clear work restores terrain and releases them");
        string fixture=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assets))!,"../../../tests/fixtures/raid-ready.lastlight");
        Require(File.Exists(fixture),"Authentic Stage D migration fixture missing.");
        if(File.Exists(fixture))
        {
            var raw=JsonSerializer.Deserialize<SaveEnvelope>(File.ReadAllText(fixture),JsonFormat.Options)!;Require(raw.FormatVersion==4,"Stage D migration fixture changed.");using var payload=JsonDocument.Parse(raw.Payload);var old=payload.RootElement.GetProperty("world");var migrated=SaveStore.Read(fixture,d).World;
            Require(migrated.Levels==1 && !migrated.Slice.Enabled && migrated.Tick==old.GetProperty("tick").GetInt64() && migrated.NextEntityId==old.GetProperty("nextEntityId").GetInt64() && migrated.Entities.Count==old.GetProperty("entities").GetArrayLength() && migrated.CombatRandomState==old.GetProperty("combatRandomState").GetUInt64(),"Migration rewrote Stage D world facts.");
            Console.WriteLine("PASS E save migration: authentic format-4 Stage D map, clock, identities, entities and combat RNG preserved");
        }
        var corrupt=VerticalWorldGenerator.Create(d,51);corrupt.Slice.Links[0].B++;bool rejected=false;try{corrupt.Validate(d);}catch(InvalidDataException){rejected=true;}Require(rejected,"Malformed stair link accepted.");
        corrupt=VerticalWorldGenerator.Create(d,53);corrupt.Slice.Formulae.Values.First().Strain=double.NaN;rejected=false;try{corrupt.Validate(d);}catch(InvalidDataException){rejected=true;}Require(rejected,"Malformed magic state accepted.");Console.WriteLine("PASS E integrity: malformed links and nonfinite strain rejected");
    }
}
