using Lastlight.Core;
using System.Text.Json;

public static class AutonomyAcceptance
{
    public static void Run(Definitions d, string output)
    {
        void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        World Camp()
        {
            var w=WorldGenerator.Create(d,74219,64,64);w.SetupSurvivalCamp(d);
            foreach(var p in w.Colony.People.Values){p.Priorities[8]=1;p.Priorities[10]=0;p.Priorities[6]=2;p.Priorities[9]=2;}
            return w;
        }
        void Steps(World w,int count){for(int i=0;i<count;i++)w.Step(d);}
        var claims=Camp();var ids=claims.Colony.People.Keys.ToArray();
        Require(claims.TryClaim("item:food",ids[0])&&!claims.TryClaim("item:food",ids[1]),"Double reservation accepted.");claims.Interrupt(ids[0],d);Require(claims.TryClaim("item:food",ids[1]),"Claim did not release.");
        Console.WriteLine("PASS C01: exclusive claims and release");
        var interrupted=Camp();interrupted.Tick=8*World.TicksPerHour;
        int Wood(World w)=>w.Resources("wood").Total+w.Autonomy.Jobs.Sum(j=>j.Cargo.GetValueOrDefault("wood"));
        int initial=Wood(interrupted);ColonyJob? carrying=null;
        for(int i=0;i<5000 && carrying==null;i++){interrupted.Step(d);carrying=interrupted.Autonomy.Jobs.Find(j=>j.Kind=="Build" && j.Cargo.ContainsKey("wood"));}
        Require(carrying!=null,"No worker carried building materials.");
        interrupted.Apply(new(CommandKind.Draft,carrying!.PawnId,Quantity:1),d);
        Require(Wood(interrupted)==initial && !interrupted.Autonomy.Jobs.Any(j=>j.PawnId==carrying.PawnId),"Draft lost or duplicated cargo.");
        Console.WriteLine("PASS C01/C04: drafting returns transported materials exactly once");
        var delivered=Camp();delivered.Tick=8*World.TicksPerHour;
        ColonyJob? building=null;
        for(int i=0;i<10000 && building==null;i++){delivered.Step(d);building=delivered.Autonomy.Jobs.Find(j=>j.Kind=="Build" && j.Work>=3);}
        Require(building!=null,"No delivered construction work found.");var plan=delivered.Colony.Blueprints.Find(b=>b.Id==building!.TargetId)!;
        int paid=plan.Delivered.Values.Sum();int progress=plan.WorkProgress;
        delivered.Apply(new(CommandKind.Draft,building!.PawnId,Quantity:1),d);
        Require(plan.Delivered.Values.Sum()==paid && plan.WorkProgress==progress,"Draft discarded delivered supplies/progress.");
        string resource=plan.Delivered.Keys.First();int before=delivered.Resources(resource).Total;
        delivered.Apply(new(CommandKind.CancelBlueprint,plan.Id),d);
        Require(delivered.Resources(resource).Total==before+paid,"Cancelled frame did not refund delivered material.");
        Console.WriteLine("PASS C04: delivered frame progress survives interruption; cancellation refunds materials");
        var pathWorld=WorldGenerator.Create(d,9,32,32);var pawn=pathWorld.Entities.First(e=>e.DefinitionId=="settler");var profile=pathWorld.Colony.People[pawn.Id];
        int target=pathWorld.Index(pawn.X+2,pawn.Y);var path=pathWorld.FindPath(pawn,profile,target,false,d);Require(path!=null,"Open path missing.");
        var areaResult=pathWorld.Apply(new(CommandKind.CreateZone,DefinitionId:"Allowed",X:pawn.X,Y:pawn.Y,Width:1,Height:1),d);
        pathWorld.Apply(new(CommandKind.AssignPolicy,pawn.Id,Quantity:(int)areaResult.EntityId,Text:"Area"),d);
        Require(pathWorld.FindPath(pawn,profile,target,false,d)==null,"Allowed area ignored.");
        Console.WriteLine("PASS C02: navigation respects allowed-area restriction");
        profile.AllowedZoneId=0;
        pathWorld.Apply(new(CommandKind.Draft,pawn.Id,Quantity:1),d);
        var move=pathWorld.Apply(new(CommandKind.MoveDrafted,pawn.Id,X:pawn.X+2,Y:pawn.Y),d);Require(move.Accepted,move.Message);
        var targetPos=(X:target%pathWorld.Width,Y:target/pathWorld.Width);
        pathWorld.Apply(new(CommandKind.PaintTerrain,DefinitionId:"water",X:targetPos.X,Y:targetPos.Y),d);
        Steps(pathWorld,3000);Require(!pathWorld.Autonomy.Jobs.Any(j=>j.PawnId==pawn.Id) && pathWorld.Autonomy.PathFailures==1,"Changed path did not fail/release cleanly.");
        Console.WriteLine("PASS C02: topology change invalidates drafted path with bounded failure");
        var w=Camp();
        for(int i=0;i<World.TicksPerDay*2 && !w.Entities.Any(e=>e.DefinitionId=="stove");i++)w.Step(d);
        var stove=w.Entities.Find(e=>e.DefinitionId=="stove");Require(stove!=null,"Workers failed to build stove.");
        Require(w.Autonomy.Built>0,"Construction did not finish.");
        w.Apply(new(CommandKind.EditBill,stove!.Id,"simple-meal",Text:"add"),d);
        string sample=Path.Combine(output,"autonomous-colony.lastlight");
        SaveStore.Write(sample,w,new ViewState{CameraX=w.Width/2,CameraY=w.Height/2,Zoom=1},d);
        long start=w.Tick;Steps(w,World.TicksPerDay*10);
        Require(w.Entities.Where(e=>e.DefinitionId=="settler").Count()==5,"Residents disappeared.");
        Require(w.Entities.Where(e=>e.DefinitionId=="settler").All(e=>e.Health==100),"Ten-day survival gate failed.");
        Require(w.Colony.People.Values.All(p=>p.MealsEaten>=5),"A resident failed to eat autonomously.");
        Require(w.Autonomy.Harvested>=20 && w.Autonomy.Hauled>0 && w.Autonomy.Bills.Sum(b=>b.Completed)>10,"Farming, hauling or bills inactive.");
        w.Validate(d);
        Console.WriteLine($"PASS C03-C05: five residents survive ten unattended days; harvests={w.Autonomy.Harvested}, hauls={w.Autonomy.Hauled}, meals={w.Autonomy.Bills.Sum(b=>b.Completed)}, built={w.Autonomy.Built}");
        SaveStore.Write(Path.Combine(output,"autonomous-day-ten.lastlight"),w,new ViewState{CameraX=w.Width/2,CameraY=w.Height/2},d);
        var loaded=SaveStore.Read(Path.Combine(output,"autonomous-day-ten.lastlight"),d).World;
        Steps(w,3000);Steps(loaded,3000);Require(SaveStore.Fingerprint(w)==SaveStore.Fingerprint(loaded),"Reload diverged with jobs in flight.");
        Console.WriteLine("PASS C01-C05: in-flight job/crop/bill/need/claim continuation matches reload");
        // Two benches share a stock target. Disable consumption for this isolated production check.
        var limits=WorldGenerator.Create(d,42,32,32);limits.Tick=8*World.TicksPerHour;
        foreach(var p in limits.Colony.People.Values){p.Schedule=Enumerable.Repeat(ScheduleActivity.Work,24).ToArray();p.Food=1000;p.Priorities=Enumerable.Repeat(0,16).ToArray();p.Priorities[6]=1;}
        foreach(var pos in new[]{(13,12),(17,12)})
        {var result=limits.Apply(new(CommandKind.Spawn,DefinitionId:"stove",X:pos.Item1,Y:pos.Item2),d);Require(result.Accepted,result.Message);var add=limits.Apply(new(CommandKind.EditBill,result.EntityId,"simple-meal",Text:"add"),d);limits.Apply(new(CommandKind.EditBill,add.EntityId,Quantity:2,Text:"target"),d);}
        var supply=limits.Entities.First(e=>e.DefinitionId=="grain");supply.Quantity=15;
        limits.Apply(new(CommandKind.Spawn,DefinitionId:"grain",X:15,Y:21,Quantity:15),d);
        int concurrent=0;for(int i=0;i<6000;i++){limits.Step(d);concurrent=Math.Max(concurrent,limits.Autonomy.Jobs.Count(j=>j.Kind=="Produce"));}
        Require(concurrent==2,"Test did not exercise simultaneous production reservations.");Require(limits.Resources("meal").Total==2,"Shared stock target failed: "+limits.Resources("meal").Total+" meals, "+string.Join(",",limits.Autonomy.Jobs.Select(j=>j.Kind+":"+j.Work))+" completed "+limits.Autonomy.Bills.Sum(b=>b.Completed));
        Console.WriteLine("PASS C05: two benches respect stock plus committed outputs");
        var firstBill=limits.Autonomy.Bills[0];var secondBill=limits.Autonomy.Bills[1];
        limits.Apply(new(CommandKind.EditBill,secondBill.Id,Quantity:1,Text:"suspend"),d);
        limits.Apply(new(CommandKind.EditBill,firstBill.Id,Quantity:1,Text:"mode"),d);
        limits.Apply(new(CommandKind.EditBill,firstBill.Id,Quantity:3,Text:"remaining"),d);
        Steps(limits,6000);Require(firstBill.Remaining==0 && limits.Resources("meal").Total==5,"Fixed-count bill failed to stop after three additional batches.");
        Console.WriteLine("PASS C05: fixed-count production stops at requested batches");
        File.WriteAllText(Path.Combine(output,"stage-c-acceptance.txt"),$"Pinned seed 74219, 64x64, elapsed {World.TicksPerDay*10} ticks ({(w.Tick-start)/(double)World.TicksPerDay:0.00} days including continuation check). Five living residents, health 100 each. Harvests {w.Autonomy.Harvested}; hauling jobs {w.Autonomy.Hauled}; completed bills {w.Autonomy.Bills.Sum(b=>b.Completed)}. Deterministic reload continuation passed.\n");
    }
}
