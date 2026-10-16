using Lastlight.Core;
public static class SliceAcceptance
{
    public static void Run(Definitions d,string output)
    {
        Directory.CreateDirectory(output);
        void Require(bool v,string m){if(!v)throw new Exception(m);}
        void Steps(World w,int n){for(int i=0;i<n;i++)w.Step(d);}
        void Until(World w,Func<bool> condition,int limit,string name){for(int i=0;i<limit && !condition();i++)w.Step(d);Require(condition(),name+" stalled at "+w.Tick+"; "+string.Join(" | ",w.Autonomy.Jobs.Select(j=>j.Kind+":"+j.DefinitionId+":"+j.Work)));w.Validate(d);}
        void Command(World w,WorldCommand c){var result=w.Apply(c,d);Require(result.Accepted,c.Kind+"/"+c.Text+": "+result.Message);}
        void Save(string name,World w,string tab="Tutorial",int z=0){SaveStore.Write(Path.Combine(output,name+".lastlight"),w,new ViewState{CameraX=64,CameraY=64,Level=z,OpenTab=tab},d);}
        var w=VerticalWorldGenerator.Create(d,74219);var people=w.Colony.People.Keys.ToArray();long scholar=people[0];
        Save("vertical-slice",w);Require(w.Levels==3 && w.Slice.Links.Count==2 && w.Slice.Links.All(l=>!l.Enabled),"Three levels/locked stairs missing.");
        var actor=w.Find(scholar)!;
        Require(!w.Apply(new(CommandKind.MoveDrafted,scholar,X:73,Y:59,Z:1),d).Accepted,"Undrafted move accepted.");
        void Field(string action,long target,Func<bool> finished){Command(w,new(CommandKind.SliceAction,scholar,TargetId:target,Text:action));Until(w,finished,18000,action);}
        Field("survey",w.Slice.Ruin.TabletId,()=>w.Slice.Ruin.Discovered);
        Require(w.Slice.Links[0].Enabled && !w.Slice.Links[1].Enabled,"Survey released incorrect stairs.");
        Require(!w.Apply(new(CommandKind.SliceAction,scholar,TargetId:w.Slice.Ruin.FolioId,Text:"archive"),d).Accepted,"Archive reachable across an unbridged rift.");
        for(int x=66;x<=68;x++)Command(w,new(CommandKind.CreateBlueprint,DefinitionId:"bridge",X:x,Y:59,Z:1,Text:"timber"));
        Until(w,()=>w.Slice.BridgeFloors.Count==3,30000,"bridge construction");
        Field("archive",w.Slice.Ruin.FolioId,()=>w.Slice.Ruin.ArchiveRead);
        Require(actor.Z==1 && w.Slice.Formulae[scholar].Known.Count==4 && w.Slice.Links.All(l=>l.Enabled),"Archive/vertical walking/knowledge missing.");
        Save("archive-discovery",w,"Ruin",1);Console.WriteLine("PASS E01/E04: real stair travel, bounded bridge construction, unreachable archive, discovery and learned formulae");
        Field("cache",w.Slice.Ruin.CacheId,()=>w.Slice.Ruin.LensRecovered);
        Field("open",w.Slice.Ruin.DoorId,()=>w.Slice.Ruin.DoorId==0);
        Field("engine",w.Slice.Ruin.EngineId,()=>w.Slice.Ruin.EngineKnowledge=="Understood");
        Require(w.LightAt(w.Slice.Ruin.GardenCell)<60,"Dormant engine lights garden.");
        foreach(string id in new[]{"ember","door","flesh","porter"})Command(w,new(CommandKind.PrepareFormula,scholar,id));
        Until(w,()=>w.Slice.Formulae[scholar].Prepared.Count==4,40000,"four physical preparations");
        Require(w.MemoryUsed(scholar,d)==6,"Prepared memory cost incorrect.");Save("prepared-formulae",w,"Formulae");
        var prepared=w.Slice.Formulae[scholar].Prepared.ToArray();
        Command(w,new(CommandKind.Draft,scholar,Quantity:1));
        Command(w,new(CommandKind.CastFormula,scholar,"ember",X:actor.X,Y:actor.Y,Z:actor.Z));
        Save("channel-in-progress",w,"Formulae",actor.Z);
        var channel=SaveStore.Read(Path.Combine(output,"channel-in-progress.lastlight"),d).World;
        Require(channel.Slice.Casts.Count==1 && channel.Slice.Formulae[scholar].Prepared.Count==3,"Reload resurrected expended preparation.");
        Command(w,new(CommandKind.Draft,scholar,Quantity:0));Require(w.Slice.Casts.Count==0 && w.Slice.Formulae[scholar].Prepared.Count==3,"Interruption refunded preparation.");
        Console.WriteLine("PASS E03: four real reagent preparations, memory capacity, channel interruption and spent charge reload");
        Field("repair",w.Slice.Ruin.EngineId,()=>w.Slice.Ruin.EngineRestored);
        Require(w.LightAt(w.Slice.Ruin.GardenCell)>=60 && w.WarmthAt(w.Slice.Ruin.GardenCell)>0,"Restoration does not adapt deep garden.");
        Save("engine-restored",w,"Ruin",2);
        // Equip physical weapons and ammo using transport jobs, then deploy to the engine's approach.
        for(int n=0;n<3;n++)
        {
            long id=people[n];var bow=w.Entities.FirstOrDefault(e=>e.DefinitionId=="bow" && !w.Autonomy.Claims.ContainsKey("item:"+e.Id));
            if(bow==null){var spawn=w.Apply(new(CommandKind.Spawn,DefinitionId:"bow",X:60,Y:67),d);Require(spawn.Accepted,spawn.Message);bow=w.Find(spawn.EntityId)!;}
            Command(w,new(CommandKind.Equip,id,TargetId:bow.Id));Until(w,()=>!w.Autonomy.Jobs.Any(j=>j.PawnId==id && j.Kind=="Equip"),18000,"equip bow");
            var arrows=w.Entities.First(e=>e.DefinitionId=="arrows" && !w.Autonomy.Claims.ContainsKey("item:"+e.Id));Command(w,new(CommandKind.Equip,id,TargetId:arrows.Id));Until(w,()=>!w.Autonomy.Jobs.Any(j=>j.PawnId==id && j.Kind=="Equip"),18000,"equip arrows");
        }
        // The warning is deterministic and is allowed to advance while equipment is collected.
        Until(w,()=>w.Slice.Ruin.GuardianId>0,3000,"ward awakening");
        for(int n=0;n<3;n++){long id=people[n];Command(w,new(CommandKind.Draft,id,Quantity:1));Command(w,new(CommandKind.CombatOrder,id,TargetId:w.Slice.Ruin.GuardianId,Text:"attack"));}
        Until(w,()=>w.Slice.Ruin.DefenseComplete,30000,"ward defense");
        foreach(long id in people)Command(w,new(CommandKind.Draft,id,Quantity:0));
        Save("observatory-defended",w,"Danger",2);Console.WriteLine("PASS E04: engine repaired with transported lens/brass/stone; ward warning, actual armed defense and restored garden light");
        Command(w,new(CommandKind.CreateZone,DefinitionId:"Growing",X:59,Y:62,Z:2,Width:6,Height:4));
        Until(w,()=>w.Slice.Tutorial.Completed.Contains("adaptation"),World.TicksPerDay*4,"deep harvest");
        // A successfully completed spell using a fresh physical preparation completes the final tutorial milestone.
        Command(w,new(CommandKind.PrepareFormula,scholar,"ember"));Until(w,()=>w.Slice.Formulae[scholar].Prepared.Any(p=>p.FormulaId=="ember"),18000,"ember preparation");
        Command(w,new(CommandKind.Draft,scholar,Quantity:1));actor=w.Find(scholar)!;Command(w,new(CommandKind.CastFormula,scholar,"ember",X:actor.X,Y:actor.Y,Z:actor.Z));Steps(w,120);Command(w,new(CommandKind.Draft,scholar,Quantity:0));
        Until(w,()=>w.Slice.Tutorial.CompletedAt>0,1000,"tutorial milestone");
        Steps(w,World.TicksPerDay*10);w.Validate(d);Require(w.Colony.People.Keys.All(id=>w.Body(id) is {Dead:false}),"Ten-day colony lost a resident.");
        Require(w.Resources("meal").Total+w.Resources("grain").Total>0,"Food loop exhausted.");Save("vertical-slice-day-ten",w);
        var reloaded=SaveStore.Read(Path.Combine(output,"vertical-slice-day-ten.lastlight"),d).World;Require(SaveStore.Fingerprint(w)==SaveStore.Fingerprint(reloaded),"Slice save fingerprint differs.");
        Steps(w,900);Steps(reloaded,900);Require(SaveStore.Fingerprint(w)==SaveStore.Fingerprint(reloaded),"Continuation diverged.");
        Console.WriteLine("PASS E05: discovery/adaptation/defense tutorial complete; ten additional simulated days (two hours at 1x), survival and deterministic reload");
        // Isolated mechanics tests use deliberately arranged fixtures, separate from the genuine journey above.
        var support=VerticalWorldGenerator.Create(d,17);int cell=support.Index(64,64,0);support.Slice.Roofs.Add(cell);
        for(int y=59;y<=69;y++)for(int x=59;x<=69;x++)support.CellAt(x,y,0).TerrainId="soil";
        foreach(var e in support.Entities.Where(e=>e.DefinitionId is "wall" or "column" && Math.Abs(e.X-64)+Math.Abs(e.Y-64)<=4).ToArray())support.Apply(new(CommandKind.Remove,e.Id),d);
        Steps(support,300);Require(support.Slice.UnstableSince.ContainsKey(cell),"Unsupported roof did not warn.");Save("support-warning",support,"Levels");Steps(support,World.TicksPerHour*2+300);Require(support.Slice.Debris.Contains(cell) && support.Slice.Collapses>0,"Unsupported roof did not collapse.");support.Validate(d);Save("support-collapse",support,"Levels");
        Console.WriteLine("PASS E02: bounded support warning and delayed collapse persist in validated saves");
        Mechanics(d,output);
    }
    private static void Mechanics(Definitions d,string output)
    {
        var w=VerticalWorldGenerator.Create(d,31);long id=w.Colony.People.Keys.First();var actor=w.Find(id)!;var p=w.Slice.Formulae[id];p.Known.UnionWith(d.Document.Formulae.Select(f=>f.Id));
        foreach(var person in w.Colony.People.Values)person.Drafted=true;actor.X=61;actor.Y=64;actor.Z=0;
        void Require(bool v,string m){if(!v)throw new Exception(m);}
        void Cast(string formula,long target=0){p.Prepared.Add(new(){Id=w.Slice.NextId++,FormulaId=formula,PreparedAt=w.Tick});p.Strain=0;var r=w.Apply(new(CommandKind.CastFormula,id,formula,X:actor.X,Y:actor.Y,Z:actor.Z,TargetId:target),d);Require(r.Accepted,r.Message);for(int i=0;i<120;i++)w.Step(d);}
        // Choose deterministic successful cast RNG for exact conservation checks.
        p.Skill=20;w.Slice.MagicRandomState=123456;
        Cast("ember");Require(w.Slice.Effects.Any(e=>e.Kind=="Light"),"Ember did not create light.");
        var door=w.Apply(new(CommandKind.Spawn,DefinitionId:"door",X:62,Y:64),d);Require(door.Accepted,door.Message);Cast("door",door.EntityId);Require(!w.Traversable(w.Index(62,64,0),d),"Patient Door does not block navigation.");
        w.InflictWound(id,"left-arm","Cut",10,0,d);Cast("flesh",id);Require(w.BleedingPerHour(id)==0 && w.Body(id)!.Wounds.Any(x=>!x.Tended),"Returning Flesh bypasses tending or fails to arrest bleed.");
        var source=w.Apply(new(CommandKind.Spawn,DefinitionId:"brass",X:60,Y:64,Quantity:20),d);Require(source.Accepted,source.Message);int before=w.Resources("brass").Total;Cast("porter",source.EntityId);
        for(int i=0;i<6000 && w.Slice.Effects.Any(e=>e.Kind=="Porter");i++)w.Step(d);
        Require(w.Slice.PorterDeliveries==1 && w.Resources("brass").Total==before,"Porter lost/duplicated cargo or failed delivery.");w.Validate(d);
        SaveStore.Write(Path.Combine(output,"four-formula-effects.lastlight"),w,new ViewState{CameraX=64,CameraY=64,OpenTab="Formulae"},d);
        for(int i=0;i<9000;i++)w.Step(d);Require(w.Traversable(w.Index(62,64,0),d),"Seal failed to expire.");
        Console.WriteLine("PASS E03: light, temporary physical seal, non-resurrecting wound support, bounded porter conservation and effect expiry");
    }
}
