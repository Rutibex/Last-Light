namespace Lastlight.Core;

public sealed class ColonyJob
{
    public long PawnId { get; set; }
    public string Kind { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public int ItemCondition { get; set; } = 100;
    public long AuxiliaryId { get; set; }
    public long TargetId { get; set; }
    public int Cell { get; set; }
    public int Destination { get; set; } = -1;
    public double WorkCredit { get; set; }
    public int Work { get; set; }
    public List<long> Sources { get; set; } = [];
    public List<int> Amounts { get; set; } = [];
    public Dictionary<string,int> Cargo { get; set; } = new();
    public List<int> Path { get; set; } = [];
    public List<string> Claims { get; set; } = [];
}
public sealed class ProductionBill
{
    public long Id { get; set; }
    public long BenchId { get; set; }
    public string RecipeId { get; set; } = "simple-meal";
    public int Target { get; set; } = 20;
    public bool CountMode { get; set; }
    public int Remaining { get; set; } = 10;
    public long WorkerId { get; set; }
    public int IngredientRadius { get; set; } = 256;
    public bool Repeat { get; set; }
    public bool Suspended { get; set; }
    public int Completed { get; set; }
}
public sealed class CropState
{
    public int Cell { get; set; }
    public long GrowthTicks { get; set; }
    public long Stress {get;set;}
    public long PlantedAt { get; set; }
    public string OutputId { get; set; } = "grain";
}
public sealed class AutonomousState
{
    public List<ColonyJob> Jobs { get; set; } = [];
    public Dictionary<string,long> Claims { get; set; } = new();
    public List<ProductionBill> Bills { get; set; } = [];
    public Dictionary<int,CropState> Crops { get; set; } = new();
    public Dictionary<string,int> Research { get; set; } = new();
    public int Built { get; set; }
    public int Hauled { get; set; }
    public int Harvested { get; set; }
    public int PathFailures { get; set; }
}
public sealed partial class World
{
    public AutonomousState Autonomy { get; set; } = new();
    public int Index(int x,int y,int z=0) => (z*Height+y)*Width+x;
    private (int X,int Y,int Z) Position(int i) => (i%Width,i/Width%Height,i/(Width*Height));
    public IEnumerable<(int X,int Y,int Z)> BuildingCells(Entity e)
    {
        var b=ColonyCatalog.Find(e.DefinitionId);
        if (b==null) { yield return (e.X,e.Y,e.Z); yield break; }
        foreach(var c in BlueprintCells(new Blueprint {BuildableId=e.DefinitionId,X=e.X,Y=e.Y,Z=e.Z,Rotation=e.Rotation})) yield return(c.X,c.Y,e.Z);
    }
    private bool Passable(int i, PawnProfile p, Definitions d)
    {
        if (!Traversable(i,d)) return false;
        var pos=Position(i);
        if(WaterDepth(i)>=4 || Entities.Any(e=>e.DefinitionId=="f-floodgate" && BuildingCells(e).Contains(pos) && !Physical.Devices.GetValueOrDefault(e.Id,new()).Open))return false;
        if (Entities.Any(e=>e.DefinitionId is "wall" or "fence" or "column" or "sealed-door" && BuildingCells(e).Contains(pos))) return false;
        var zone=Colony.Zones.Find(z=>z.Id==p.AllowedZoneId);
        return zone==null || zone.Cells.Contains(i);
    }
    private IEnumerable<int> Neighbors(int i)
    {
        foreach(int next in HorizontalNeighbors(i))yield return next;
        foreach(int next in VerticalNeighbors(i))yield return next;
    }
    // Paths use explicit level links. Every edge is revalidated against live topology and policies.
    public List<int>? FindPath(Entity pawn, PawnProfile profile, int destination, bool adjacent, Definitions d)
    {
        if(destination<0 || destination>=Cells.Count) return null;
        int start=Index(pawn.X,pawn.Y,pawn.Z);
        var blocked=Entities.Where(e=>e.DefinitionId is "wall" or "fence" or "column" or "sealed-door").SelectMany(BuildingCells).Select(c=>Index(c.X,c.Y,c.Z)).ToHashSet();
        var zone=Colony.Zones.Find(z=>z.Id==profile.AllowedZoneId);
        bool Open(int i)=>WaterDepth(i)<4 && i>=0 && i<Cells.Count && d.Terrain[Cells[i].TerrainId].Walkable && !Entities.Any(e=>e.DefinitionId=="f-floodgate" && Index(e.X,e.Y,e.Z)==i && !Physical.Devices.GetValueOrDefault(e.Id,new()).Open) && !blocked.Contains(i) && !Slice.Debris.Contains(i) && !Sealed(i) && (zone==null || zone.Cells.Contains(i));
        var goals=(adjacent ? HorizontalNeighbors(destination) : new[]{destination}).Where(Open).ToHashSet();
        if(goals.Contains(start)) return [];
        var queue=new Queue<int>(); queue.Enqueue(start);
        var from=new Dictionary<int,int>{{start,-1}};
        while(queue.Count>0)
        {
            int current=queue.Dequeue();
            foreach(int next in Neighbors(current))
            {
                if(from.ContainsKey(next) || !Open(next)) continue;
                from[next]=current;
                if(goals.Contains(next))
                {
                    var path=new List<int>(); for(int at=next;at!=start;at=from[at])path.Add(at); path.Reverse();return path;
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }
    public bool TryClaim(string key,long pawnId)
    {
        if(!Colony.People.ContainsKey(pawnId)) return false;
        if(Autonomy.Claims.TryGetValue(key,out long owner)) return owner==pawnId;
        Autonomy.Claims.Add(key,pawnId); return true;
    }
    private bool Start(ColonyJob job)
    {
        if(job.Claims.Any(k=>Autonomy.Claims.ContainsKey(k))) return false;
        foreach(string key in job.Claims) TryClaim(key,job.PawnId);
        Autonomy.Jobs.Add(job); return true;
    }
    public void Interrupt(long pawnId,Definitions d)
    {
        foreach(var job in Autonomy.Jobs.Where(j=>j.PawnId==pawnId).ToArray())
        {
            var pawn=Find(pawnId);
            if(pawn!=null)foreach(var item in job.Cargo) Drop(item.Key,item.Value,Index(pawn.X,pawn.Y,pawn.Z),d);
            foreach(string key in job.Claims) Autonomy.Claims.Remove(key);
            Autonomy.Jobs.Remove(job);
        }
        foreach(var body in Danger.Bodies.Values.Where(b=>b.CarriedBy==pawnId))body.CarriedBy=0;
        // Also release standalone claims used by tools/tests.
        CancelChannel(pawnId);
        foreach(var key in Autonomy.Claims.Where(k=>k.Value==pawnId && !Slice.Effects.Any(e=>e.Claims.Contains(k.Key))).Select(k=>k.Key).ToArray())Autonomy.Claims.Remove(key);
    }
    private void Drop(string id,int count,int cell,Definitions d)
    {
        var p=Position(cell); int max=d.Entities[id].MaxStack;
        foreach(var e in Entities.Where(e=>e.DefinitionId==id && !e.Forbidden && Index(e.X,e.Y,e.Z)==cell && !Autonomy.Claims.ContainsKey("item:"+e.Id)).ToArray())
        {int n=Math.Min(count,max-e.Quantity);e.Quantity+=n;count-=n;if(count==0)return;}
        while(count>0){int n=Math.Min(max,count);Entities.Add(new Entity{Id=NextEntityId++,DefinitionId=id,Name=d.Entities[id].Name,X=p.X,Y=p.Y,Z=p.Z,Quantity=n});count-=n;}
    }
    private bool FoodAllowed(PawnProfile p,string id)
    {
        var policy=Colony.Policies.Find(q=>q.Id==p.Policies["Food"]);
        return id=="meal" || (policy?.Values.GetValueOrDefault("Raw food",1)!=0 && (id!="grain" || policy?.Values.GetValueOrDefault("Duskgrain",1)!=0));
    }
    private bool ReserveIngredients(ColonyJob j,Dictionary<string,int> ingredients,Definitions d, int radius=256)
    {
        var pawn=Find(j.PawnId)!;var p=Colony.People[j.PawnId];
        foreach(var ingredient in ingredients)
        {
            int left=ingredient.Value;
            foreach(var e in Entities.Where(e=>e.DefinitionId==ingredient.Key && !Region.Artifacts.Any(a=>a.LocalEntityId==e.Id) && !e.Forbidden && !Autonomy.Claims.ContainsKey("item:"+e.Id)).OrderBy(e=>Math.Abs(e.X-pawn.X)+Math.Abs(e.Y-pawn.Y)).ThenBy(e=>e.Id))
            {
                var origin=Position(j.Cell);
                if(Math.Abs(e.X-origin.X)+Math.Abs(e.Y-origin.Y)>radius || FindPath(pawn,p,Index(e.X,e.Y,e.Z),false,d)==null)continue;
                int n=Math.Min(left,e.Quantity);j.Sources.Add(e.Id);j.Amounts.Add(n);j.Claims.Add("item:"+e.Id);left-=n;if(left==0)break;
            }
            if(left>0)return false;
        }
        return true;
    }
    private ColonyJob? PickJob(Entity pawn,PawnProfile p,Definitions d)
    {
        long id=pawn.Id; int here=Index(pawn.X,pawn.Y,pawn.Z);
        bool Reach(int cell,bool adj=false)=>FindPath(pawn,p,cell,adj,d)!=null;
        ColonyJob Job(string kind,long target,int cell)=>new(){PawnId=id,Kind=kind,TargetId=target,Cell=cell,Claims=[kind+":"+target]};
        if(p.Food>=200 && p.Rest>=100 && PickMedicalJob(pawn,p,d) is { } medical)return medical;
        if(p.Food<450)
        {
            foreach(var food in Entities.Where(e=>e.DefinitionId is "meal" or "grain" or "fungus" && !e.Forbidden && FoodAllowed(p,e.DefinitionId) && !Autonomy.Claims.ContainsKey("item:"+e.Id)).OrderBy(e=>e.DefinitionId=="meal"?0:1).ThenBy(e=>Math.Abs(e.X-pawn.X)+Math.Abs(e.Y-pawn.Y)).ThenBy(e=>e.Id))
                if(Reach(Index(food.X,food.Y,food.Z)))return new(){PawnId=id,Kind="Eat",TargetId=food.Id,Cell=Index(food.X,food.Y,food.Z),Claims=["item:"+food.Id]};
        }
        var activity=p.Schedule[Tick%TicksPerDay/TicksPerHour];
        if(p.Rest<180 || activity==ScheduleActivity.Sleep && p.Rest<950)
        {
            foreach(var bed in Entities.Where(e=>e.DefinitionId=="bed" && !e.Forbidden && !Autonomy.Claims.ContainsKey("bed:"+e.Id)).OrderBy(e=>e.Id))
                if(Reach(Index(bed.X,bed.Y,bed.Z)))return new(){PawnId=id,Kind="Sleep",TargetId=bed.Id,Cell=Index(bed.X,bed.Y,bed.Z),Claims=["bed:"+bed.Id]};
            return new(){PawnId=id,Kind="Sleep",Cell=here};
        }
        if(p.Recreation<150 || activity==ScheduleActivity.Recreation && p.Recreation<900)return new(){PawnId=id,Kind="Recreation",Cell=here};
        if(activity==ScheduleActivity.Sleep || activity==ScheduleActivity.Recreation)return null;
        if(activity==ScheduleActivity.Study && PickInstitutionJob(pawn,p,d) is {} institution)return institution;
        var work=Enumerable.Range(0,p.Priorities.Length).Where(i=>p.Priorities[i]>0).OrderBy(i=>Colony.ManualPriorities?p.Priorities[i]:3).ThenBy(i=>i);
        if(activity==ScheduleActivity.Study)work=work.Where(i=>ColonyCatalog.Work[i]=="Research").OrderBy(i=>i);
        foreach(int category in work)
        {
            string name=ColonyCatalog.Work[category];
            if(PickPhysicalJob(pawn,p,name,d) is {} physicalJob)return physicalJob;
            if(name is "Research" or "Construct" && PickCampaignJob(pawn,p,d) is {} campaignJob)return campaignJob;
            if(name=="Research" && PickFormulaPreparation(pawn,p,d) is { } prep)return prep;
            if(Capacity(pawn.Id).Manipulation<.25)continue;
            if(name=="Construct")
            {
                foreach(var b in Colony.Blueprints.OrderBy(b=>b.Id))
                {
                    int cell=Index(b.X,b.Y,b.Z); if(Autonomy.Claims.ContainsKey("Build:"+b.Id) || !Reach(cell,true))continue;
                    var j=Job("Build",b.Id,cell);string material=MaterialResource(b.MaterialId);
                    int remaining=ColonyCatalog.Find(b.BuildableId)!.Cost-b.Delivered.GetValueOrDefault(material);
                    if(remaining<=0 || ReserveIngredients(j,new(){{material,remaining}},d))return j;
                }
                foreach(var mark in Colony.Designations.Where(m=>m.Kind=="Deconstruct").OrderBy(m=>m.Id))
                    if(!Autonomy.Claims.ContainsKey("Deconstruct:"+mark.Id)&&Reach(Index(mark.X,mark.Y,mark.Z),true))return Job("Deconstruct",mark.Id,Index(mark.X,mark.Y,mark.Z));
            }
            if(name=="Grow")
            {
                foreach(int cell in Colony.Zones.Where(z=>z.Kind==ZoneKind.Growing).SelectMany(z=>z.Cells).Distinct().Order())
                {
                    if(Cells[cell].TerrainId!="soil" || Autonomy.Claims.ContainsKey("crop:"+cell) || !Reach(cell))continue;
                    if(!Autonomy.Crops.TryGetValue(cell,out var crop))return new(){PawnId=id,Kind="Sow",Cell=cell,Claims=["crop:"+cell]};
                    if(CropReady(crop))return new(){PawnId=id,Kind="Harvest",Cell=cell,Claims=["crop:"+cell]};
                }
            }
            if(name is "Cook" or "Craft")
            {
                foreach(var bill in Autonomy.Bills.Where(b=>!b.Suspended))
                {
                    var recipe=d.Document.Recipes.Find(r=>r.Id==bill.RecipeId)!;var bench=Find(bill.BenchId);if(Physical.Enabled && (bench==null || RecipeBench(recipe.Id)!=bench.DefinitionId || !BenchReady(bench,recipe.Id)))continue;
                    if(bill.WorkerId!=0 && bill.WorkerId!=id)continue;
                    if(bill.CountMode && bill.Remaining<=Autonomy.Jobs.Count(j=>j.Kind=="Produce" && j.TargetId==bill.Id))continue;
                    if(bench==null || bench.Forbidden || (name=="Cook")!=(bench.DefinitionId=="stove") || Autonomy.Claims.ContainsKey("bench:"+bench.Id))continue;
                    int committed=Autonomy.Jobs.Where(j=>j.Kind=="Produce" && Autonomy.Bills.Any(b=>b.Id==j.TargetId && d.Document.Recipes.Find(r=>r.Id==b.RecipeId)!.OutputId==recipe.OutputId)).Sum(j=>d.Document.Recipes.Find(r=>r.Id==Autonomy.Bills.Find(b=>b.Id==j.TargetId)!.RecipeId)!.OutputCount);
                    if(!bill.Repeat && !bill.CountMode && Resources(recipe.OutputId).Available+committed>=bill.Target)continue;
                    int cell=Index(bench.X,bench.Y,bench.Z); if(!Reach(cell,true))continue;
                    var j=Job("Produce",bill.Id,cell);j.Claims=["bench:"+bench.Id];if(ReserveIngredients(j,recipe.Ingredients,d,bill.IngredientRadius))return j;
                }
            }
            if(name=="Construct")foreach(var mark in Colony.Designations.Where(m=>m.Kind=="Clear").OrderBy(m=>m.Id))
                if(Slice.Debris.Contains(Index(mark.X,mark.Y,mark.Z)) && !Autonomy.Claims.ContainsKey("Clear:"+mark.Id) && Reach(Index(mark.X,mark.Y,mark.Z),true))return Job("Clear",mark.Id,Index(mark.X,mark.Y,mark.Z));
            if(name=="Mine")foreach(var mark in Colony.Designations.Where(m=>m.Kind=="Mine").OrderBy(m=>m.Id))
                if((!Physical.Enabled||CellAt(mark.X,mark.Y,mark.Z).TerrainId is "stone" or "sand") && !Autonomy.Claims.ContainsKey("Mine:"+mark.Id) && Reach(Index(mark.X,mark.Y,mark.Z),true))return Job("Mine",mark.Id,Index(mark.X,mark.Y,mark.Z));
            if(name=="Haul")
            {
                foreach(var item in Entities.Where(e=>d.Entities[e.DefinitionId].Kind==EntityKind.ItemStack && !Region.Artifacts.Any(a=>a.LocalEntityId==e.Id) && !e.Forbidden && !Autonomy.Claims.ContainsKey("item:"+e.Id)).OrderBy(e=>e.Id))
                {
                    int at=Index(item.X,item.Y,item.Z);int current=Colony.Zones.Where(z=>z.Kind==ZoneKind.Stockpile && z.Cells.Contains(at) && ZoneAccepts(z,item)).Select(z=>z.Priority).DefaultIfEmpty(-1).Max();
                    foreach(var zone in Colony.Zones.Where(z=>z.Kind==ZoneKind.Stockpile && z.Priority>current && ZoneAccepts(z,item)).OrderByDescending(z=>z.Priority).ThenBy(z=>z.Cells.Min(c=>Math.Abs(Position(c).X-item.X)+Math.Abs(Position(c).Y-item.Y))).ThenBy(z=>z.Id))
                    foreach(int destination in zone.Cells.OrderBy(c=>Math.Abs(Position(c).X-item.X)+Math.Abs(Position(c).Y-item.Y)).ThenBy(c=>c))
                    {
                        if(Autonomy.Claims.ContainsKey("storage:"+destination) || Entities.Any(e=>Index(e.X,e.Y,e.Z)==destination && d.Entities[e.DefinitionId].Kind==EntityKind.ItemStack) || !Reach(at) || !Reach(destination))continue;
                        return new(){PawnId=id,Kind="Haul",TargetId=item.Id,Cell=destination,Destination=destination,Sources=[item.Id],Amounts=[item.Quantity],Claims=["item:"+item.Id,"storage:"+destination]};
                    }
                }
            }
            if(name=="Research" && Colony.ResearchProject!="" && Autonomy.Research.GetValueOrDefault(Colony.ResearchProject)<1000)
                foreach(var bench in Entities.Where(e=>e.DefinitionId=="study" && !e.Forbidden).OrderBy(e=>e.Id))
                    if(!Autonomy.Claims.ContainsKey("bench:"+bench.Id) && Reach(Index(bench.X,bench.Y,bench.Z),true))return new(){PawnId=id,Kind="Research",TargetId=bench.Id,Cell=Index(bench.X,bench.Y,bench.Z),Claims=["bench:"+bench.Id]};
        }
        return null;
    }
    private bool ValidJob(ColonyJob j,PawnProfile p,Definitions d)
    {
        if(!CanAct(j.PawnId))return false;
        if(j.Kind=="Campaign")return ValidCampaignJob(j);
        if(j.Kind=="Institution")return ValidInstitutionJob(j);
        if(j.Kind is "Provision" or "RegionalTrade")return ValidRegionJob(j,p);
        if(j.Kind is "Firefight" or "Husbandry" or "Repair" or "Refuel" or "Hunt")return ValidPhysicalJob(j,p);
        if(j.Kind is "Prepare" or "Ruin")return ValidSliceJob(j,p);
        if(j.Sources.Any(id=>Find(id)==null || Find(id)!.Forbidden))return false;
        if(j.Kind is "Tend" or "Rescue" or "PatientRest" or "Equip" or "Loot")return ValidMedicalJob(j,p);
        if(j.Kind=="Move")return p.Drafted;
        if(p.Drafted || Find(j.PawnId)?.Health==0)return false;
        if(j.Sources.Any(id=>Find(id)==null || Find(id)!.Forbidden))return false;
        string category=j.Kind switch { "Build" or "Deconstruct" or "Clear"=>"Construct", "Haul"=>"Haul", "Sow" or "Harvest"=>"Grow", "Mine"=>"Mine", "Research"=>"Research", "Produce"=>Find(Autonomy.Bills.Find(b=>b.Id==j.TargetId)?.BenchId??0)?.DefinitionId=="stove"?"Cook":"Craft", _=>"" };
        if(category!="" && (p.Priorities[Array.IndexOf(ColonyCatalog.Work,category)]==0 || p.Schedule[Tick%TicksPerDay/TicksPerHour] is ScheduleActivity.Sleep or ScheduleActivity.Recreation))return false;
        if(j.Kind=="Eat")return Find(j.TargetId) is {Forbidden:false} food && FoodAllowed(p,food.DefinitionId);
        if(j.Kind=="Build")return Colony.Blueprints.Any(b=>b.Id==j.TargetId && (b.BuildableId=="roof" || !Entities.Any(e=>d.Entities[e.DefinitionId].Kind==EntityKind.Building && BuildingCells(e).Any(c=>c.Z==b.Z && BlueprintCells(b).Contains((c.X,c.Y))))));
        if(j.Kind=="Produce")return Autonomy.Bills.Any(b=>b.Id==j.TargetId && !b.Suspended && (b.WorkerId==0 || b.WorkerId==j.PawnId) && Find(b.BenchId) is {Forbidden:false});
        if(j.Kind is "Sow" or "Harvest")return Colony.Zones.Any(z=>z.Kind==ZoneKind.Growing && z.Cells.Contains(j.Cell));
        if(j.Kind is "Mine" or "Deconstruct" or "Clear")return Colony.Designations.Any(m=>m.Id==j.TargetId);
        if(j.Kind=="Haul")return Colony.Zones.Any(z=>z.Kind==ZoneKind.Stockpile && z.Cells.Contains(j.Cell) && j.Cargo.All(k=>ZoneAccepts(z,new Entity{DefinitionId=k.Key})));
        if(j.Kind=="Research")return Colony.ResearchProject!="" && Find(j.TargetId) is {Forbidden:false};
        if(j.Kind=="Sleep" && j.TargetId!=0)return Find(j.TargetId) is {Forbidden:false};
        return true;
    }
    private void AdvanceColony(Definitions d)
    {
        Colony.EnsureEntities(this,d);
        foreach(var missing in Autonomy.Jobs.Where(j=>Find(j.PawnId)==null).ToArray())Interrupt(missing.PawnId,d);
        foreach(var pawn in Entities.Where(e=>d.Entities[e.DefinitionId].Kind==EntityKind.Person).OrderBy(e=>e.Id).ToArray())
        {
            var p=Colony.People[pawn.Id];
            if(IsAway(pawn.Id)){p.Activity="Traveling expedition";continue;}
            p.Food=Math.Max(0,p.Food-.9);p.Rest=Math.Max(0,p.Rest-.65);p.Recreation=Math.Max(0,p.Recreation-.25);
            if(p.Food==0 && Tick%TicksPerHour==0)InflictWound(pawn.Id,"torso","Bruise",3,0,d);
            if(!CanAct(pawn.Id)){Interrupt(pawn.Id,d);p.Activity=Body(pawn.Id) is {Dead:true}?"Dead":Body(pawn.Id) is {Downed:true}?"Downed":"Withdrawn";continue;}
            if(p.Activity=="Fleeing" && Entities.Any(e=>Hostile(e.Id) && Distance(e,pawn)<=7 && HasLineOfSight(e,pawn,d)))continue;
            if(Slice.Casts.ContainsKey(pawn.Id) || Danger.Orders.ContainsKey(pawn.Id) && p.Drafted)continue;
            var job=Autonomy.Jobs.Find(j=>j.PawnId==pawn.Id);
            bool urgent=job!=null && job.Kind is not ("Eat" or "Sleep") && (p.Food<200 || p.Rest<100);
            if(job!=null && (!ValidJob(job,p,d) || urgent)){Interrupt(pawn.Id,d);job=null;}
            if(p.Drafted && job?.Kind is not ("Move" or "Equip" or "Rescue" or "Loot")){Interrupt(pawn.Id,d);p.Activity="Drafted";continue;}
            if(job==null)
            {
                if(Tick<p.RetryAt)continue;
                job=PickJob(pawn,p,d);
                if(job==null || !Start(job)){p.Activity=p.Food<450?"No reachable food":"Idle";p.RetryAt=Tick+300;continue;}
            }
            p.Activity=job.Kind;
            if(job.Kind is "Husbandry" or "Hunt" || job.Kind=="Tend" || job.Kind=="Rescue" && job.Destination>=0)
            {var patient=Find(job.TargetId);if(patient==null){Interrupt(pawn.Id,d);continue;}job.Cell=Index(patient.X,patient.Y,patient.Z);}
            int target=job.Cell;bool adjacent=job.Kind is "Build" or "Produce" or "Mine" or "Deconstruct" or "Research" or "Prepare" or "Ruin" or "Clear" or "Firefight" or "Repair" or "Refuel" or "Husbandry" or "Hunt" or "RegionalTrade" or "Institution" or "Campaign" || job.Kind=="Tend" && job.TargetId!=pawn.Id || job.Kind=="Rescue" && job.Destination>=0;
            if(job.Sources.Count>0){var item=Find(job.Sources[0])!;target=Index(item.X,item.Y,item.Z);adjacent=false;}
            int here=Index(pawn.X,pawn.Y,pawn.Z);
            bool arrived=adjacent?HorizontalNeighbors(target).Contains(here):here==target;
            if(!arrived)
            {
                if(Tick<Body(pawn.Id)!.NextMoveAt)continue;
                if(job.Path.Count==0 || !Neighbors(here).Contains(job.Path[0]) || !Passable(job.Path[0],p,d))
                {
                    var path=FindPath(pawn,p,target,adjacent,d);
                    if(path==null){Autonomy.PathFailures++;Interrupt(pawn.Id,d);p.Activity="No reachable work";p.RetryAt=Tick+300;continue;}job.Path=path;
                }
                if(job.Path.Count>0){MoveActor(pawn,job.Path[0]);job.Path.RemoveAt(0);ScheduleNextMovement(pawn.Id,d);}continue;
            }
            job.Path.Clear();
            if(job.Sources.Count>0)
            {
                var item=Find(job.Sources[0])!;int n=Math.Min(job.Amounts[0],item.Quantity);job.Cargo[item.DefinitionId]=job.Cargo.GetValueOrDefault(item.DefinitionId)+n;item.Quantity-=n;
                if(item.Quantity==0)Entities.Remove(item);
                // Claim retained until completion even after consuming the source; all cargo is accounted for.
                job.Sources.RemoveAt(0);job.Amounts.RemoveAt(0);continue;
            }
            if(job.Kind=="Produce" && Autonomy.Bills.Find(b=>b.Id==job.TargetId) is {} activeBill && Find(activeBill.BenchId) is {} activeBench && !BenchReady(activeBench,activeBill.RecipeId)){p.Activity="Production stopped: "+Physical.Devices.GetValueOrDefault(activeBench.Id,new()).Cause;continue;}
            if(HandleCampaignJob(job,pawn,d) || HandleInstitutionJob(job,pawn,d) || HandleRegionJob(job,pawn,d) || HandlePhysicalJob(job,pawn,d) || HandleSliceJob(job,pawn,d) || HandleMedicalJob(job,pawn,d))continue;
            if(job.Kind=="Sleep")
            {p.Rest=Math.Min(1000,p.Rest+(job.TargetId==0?2.5:4));if(p.Rest<980 && p.Food>200)continue;}
            else if(job.Kind=="Recreation"){p.Recreation=Math.Min(1000,p.Recreation+4);if(p.Recreation<950 && p.Food>200)continue;}
            else if(job.Kind=="Eat")
            {var food=Find(job.TargetId)!;food.Quantity--;if(food.Quantity==0)Entities.Remove(food);p.Food=Math.Min(1000,p.Food+(food.DefinitionId=="meal"?750:600));p.MealsEaten++;}
            else
            {
                job.WorkCredit+=Capacity(pawn.Id).Manipulation;
                if(job.WorkCredit<1)continue;job.WorkCredit-=1;
                if(job.Kind=="Build")
                {
                    var blueprint=Colony.Blueprints.Find(b=>b.Id==job.TargetId)!;
                    foreach(var supply in job.Cargo)blueprint.Delivered[supply.Key]=blueprint.Delivered.GetValueOrDefault(supply.Key)+supply.Value;
                    job.Cargo.Clear();blueprint.WorkProgress++;job.Work=blueprint.WorkProgress;
                }
                else job.Work++;
                int required=job.Kind switch {"Build"=>30+ColonyCatalog.Find(Colony.Blueprints.Find(b=>b.Id==job.TargetId)!.BuildableId)!.Cost,"Produce"=>25,"Clear"=>60,"Mine"=>60,"Deconstruct"=>40,"Research"=>30,"Sow"=>8,"Harvest"=>10,_=>1};
                if(job.Work<required)continue;
                FinishJob(job,pawn,d);
            }
            job.Cargo.Clear();Interrupt(pawn.Id,d);p.Activity="Idle";
        }
    }
    private void FinishJob(ColonyJob j,Entity pawn,Definitions d)
    {
        int here=Index(pawn.X,pawn.Y,pawn.Z);
        if(Danger.People.TryGetValue(pawn.Id,out var personality))personality.Meaning=Math.Min(1000,personality.Meaning+12);
        if(j.Kind=="Haul")
        {foreach(var item in j.Cargo)Drop(item.Key,item.Value,j.Cell,d);Autonomy.Hauled++;Colony.Designations.RemoveAll(m=>m.Kind=="Haul" && m.X==pawn.X && m.Y==pawn.Y);}
        if(j.Kind=="Sow")
        {var zone=Colony.Zones.First(z=>z.Kind==ZoneKind.Growing && z.Cells.Contains(j.Cell));Autonomy.Crops[j.Cell]=new(){Cell=j.Cell,PlantedAt=Tick,OutputId=zone.Crop=="Fungus"?"fungus":zone.Crop=="Herbs"?"f-herbs":"grain"};}
        if(j.Kind=="Harvest")
        {var crop=Autonomy.Crops[j.Cell];if(Slice.Enabled && Position(j.Cell).Z==2 && crop.OutputId=="grain")Slice.Tutorial.Completed.Add("adaptation");Drop(crop.OutputId,6,j.Cell,d);Autonomy.Crops.Remove(j.Cell);Autonomy.Harvested++;}
        if(j.Kind=="Clear")
        {Slice.Debris.Remove(j.Cell);Cells[j.Cell].TerrainId=Position(j.Cell).Z==0?"soil":"floor";foreach(var e in Entities.Where(e=>Index(e.X,e.Y,e.Z)==j.Cell))Slice.Buried.Remove(e.Id);Drop("blocks",3,here,d);Colony.Designations.RemoveAll(m=>m.Id==j.TargetId);}
        if(j.Kind=="Build")
        {
            var b=Colony.Blueprints.Find(b=>b.Id==j.TargetId)!;
            if(b.BuildableId=="roof")Slice.Roofs.Add(Index(b.X,b.Y,b.Z));
            else if(b.BuildableId=="bridge"){Cells[Index(b.X,b.Y,b.Z)].TerrainId="floor";Slice.BridgeFloors.Add(Index(b.X,b.Y,b.Z));}
            else if(b.BuildableId is "floor" or "woodfloor")foreach(var c in BlueprintCells(b))CellAt(c.X,c.Y,b.Z).TerrainId="floor";
            else
            {
                // A blocking footprint must not entomb a resident or delete items.
                var cells=BlueprintCells(b).Select(c=>Index(c.X,c.Y,b.Z)).ToHashSet();
                foreach(var e in Entities.Where(e=>cells.Contains(Index(e.X,e.Y,e.Z))).ToArray())
                {e.X=pawn.X;e.Y=pawn.Y;e.Z=pawn.Z;}
                Entities.Add(new(){Id=NextEntityId++,DefinitionId=b.BuildableId,Name=ColonyCatalog.Find(b.BuildableId)!.Name,X=b.X,Y=b.Y,Z=b.Z,Rotation=b.Rotation,MaterialId=b.MaterialId});
            }
            if(b.BuildableId=="stairs"){var stairs=Entities.Last(e=>e.DefinitionId=="stairs" && e.X==b.X && e.Y==b.Y && e.Z==b.Z);Slice.Links.Add(new(){OwnerId=stairs.Id,A=Index(b.X,b.Y,b.Z),B=Index(b.X,b.Y,b.Z+1)});}
            Colony.Blueprints.Remove(b);Autonomy.Built++;Record(pawn.Name+" built "+b.BuildableId+".");
        }
        if(j.Kind=="Produce")
        {var bill=Autonomy.Bills.Find(b=>b.Id==j.TargetId)!;var recipe=d.Document.Recipes.Find(r=>r.Id==bill.RecipeId)!;Drop(recipe.OutputId,recipe.OutputCount,here,d);RecordCraft(j,pawn,recipe,d,here);bill.Completed++;if(bill.CountMode)bill.Remaining=Math.Max(0,bill.Remaining-1);}
        if(j.Kind=="Mine")
        {string mined=Cells[j.Cell].TerrainId;Cells[j.Cell].TerrainId=Position(j.Cell).Z==0?"soil":"floor";if(Position(j.Cell).Z>0)Slice.Roofs.Add(j.Cell);if(!Physical.Enabled||mined!="sand")Drop("blocks",12,here,d);if(Physical.Enabled)Drop(mined=="sand"?"f-sand":"f-ore",mined=="sand"?6:3,here,d);Colony.Designations.RemoveAll(m=>m.Id==j.TargetId);}
        if(j.Kind=="Deconstruct")
        {
            var pos=Position(j.Cell);var building=Entities.Find(e=>d.Entities[e.DefinitionId].Kind==EntityKind.Building && BuildingCells(e).Contains(pos));
            if(building!=null){var catalog=ColonyCatalog.Find(building.DefinitionId);string material=MaterialResource(building.MaterialId);Drop(material,Math.Max(1,(catalog?.Cost??5)/2),here,d);Entities.Remove(building);RemoveDangerEntity(building.Id);RemoveSliceEntity(building.Id,d);Autonomy.Bills.RemoveAll(b=>b.BenchId==building.Id);}
            Colony.Designations.RemoveAll(m=>m.Id==j.TargetId);
        }
        if(j.Kind=="Research")Autonomy.Research[Colony.ResearchProject]=Math.Min(1000,Autonomy.Research.GetValueOrDefault(Colony.ResearchProject)+10);
    }
    private CommandResult ApplyMoveCommand(WorldCommand c,Definitions d)
    {
        var pawn=Find(c.EntityId);
        if(pawn==null || !Colony.People.TryGetValue(c.EntityId,out var p) || !p.Drafted || !Contains(c.X,c.Y,c.Z))return new(false,"Select a drafted settler and valid tile.");
        int cell=Index(c.X,c.Y,c.Z);var path=FindPath(pawn,p,cell,false,d);if(path==null)return new(false,"Destination is unreachable or outside the allowed area.");
        if(!CanAct(pawn.Id))return new(false,"This resident cannot move.");
        Danger.Orders.Remove(pawn.Id);Interrupt(pawn.Id,d);Start(new(){PawnId=pawn.Id,Kind="Move",Cell=cell,Path=path});return new(true,"Drafted move ordered.");
    }
    private CommandResult ApplyBillCommand(WorldCommand c,Definitions d)
    {
        if(c.Text=="add")
        {
            var bench=Find(c.EntityId);var recipe=d.Document.Recipes.Find(r=>r.Id==c.DefinitionId);
            if(recipe!=null && recipe.Id.StartsWith("f-",StringComparison.Ordinal) && !Physical.Enabled)return new(false,"Enable physical systems to use these manufacturing recipes.");
            if(bench==null || recipe==null || bench.DefinitionId is not ("stove" or "workbench" or "f-mill" or "f-smelter" or "f-kiln" or "f-loom" or "f-fermenter") || (Physical.Enabled?RecipeBench(recipe.Id)!=bench.DefinitionId:(bench.DefinitionId=="stove")!=(recipe.OutputId=="meal")))return new(false,"Select a compatible production bench and recipe.");
            if(Autonomy.Bills.Count>=256)return new(false,"Bill capacity reached.");
            var bill=new ProductionBill{Id=Colony.NextObjectId++,BenchId=bench.Id,RecipeId=recipe.Id};Autonomy.Bills.Add(bill);return new(true,"Bill added.",bill.Id);
        }
        var b=Autonomy.Bills.Find(b=>b.Id==c.EntityId);if(b==null)return new(false,"Bill no longer exists.");
        if(c.Text=="target" && c.Quantity is >=1 and <=1000)b.Target=c.Quantity;
        else if(c.Text=="mode" && c.Quantity is >=0 and <=2){b.Repeat=c.Quantity==2;b.CountMode=c.Quantity==1;}
        else if(c.Text=="remaining" && c.Quantity is >=0 and <=1000)b.Remaining=c.Quantity;
        else if(c.Text=="worker" && (c.Quantity==0 || Colony.People.ContainsKey(c.Quantity)))b.WorkerId=c.Quantity;
        else if(c.Text=="radius" && c.Quantity is >=1 and <=256)b.IngredientRadius=c.Quantity;
        else if(c.Text=="up") {int at=Autonomy.Bills.IndexOf(b);if(at>0){Autonomy.Bills.RemoveAt(at);Autonomy.Bills.Insert(at-1,b);}}
        else if(c.Text=="copy" && Autonomy.Bills.Count<256){Autonomy.Bills.Add(new(){Id=Colony.NextObjectId++,BenchId=b.BenchId,RecipeId=b.RecipeId,Target=b.Target,Repeat=b.Repeat,CountMode=b.CountMode,Remaining=b.Remaining,IngredientRadius=b.IngredientRadius,WorkerId=b.WorkerId,Suspended=true});}
        else if(c.Text=="repeat"){b.Repeat=c.Quantity!=0;b.CountMode=false;}
        else if(c.Text=="suspend")b.Suspended=c.Quantity!=0;
        else if(c.Text=="remove")Autonomy.Bills.Remove(b);
        else return new(false,"Invalid bill setting.");
        foreach(var j in Autonomy.Jobs.Where(j=>j.Kind=="Produce" && j.TargetId==b.Id).ToArray())Interrupt(j.PawnId,d);
        return new(true,"Bill updated.");
    }
    public void ValidateAutonomy(Definitions d)
    {
        if(Autonomy==null || Autonomy.Jobs==null || Autonomy.Claims==null || Autonomy.Bills==null || Autonomy.Crops==null || Autonomy.Research==null)throw new InvalidDataException("Missing autonomous state.");
        if(Autonomy.Jobs.Count>Colony.People.Count || Autonomy.Bills.Count>256 || Autonomy.Crops.Count>Cells.Count || Autonomy.Claims.Count>100000)throw new InvalidDataException("Autonomous capacity exceeded.");
        foreach(var b in Colony.Blueprints)
        {
            string material=MaterialResource(b.MaterialId);
            if(b.Delivered==null || b.Delivered.Any(k=>k.Key!=material || k.Value<1 || k.Value>ColonyCatalog.Find(b.BuildableId)!.Cost) || b.WorkProgress<0)throw new InvalidDataException("Invalid delivered construction materials.");
        }
        var owners=new HashSet<long>();
        foreach(var j in Autonomy.Jobs)
        {
            if(j==null || j.Kind is not ("Build" or "Deconstruct" or "Produce" or "Sow" or "Harvest" or "Eat" or "Sleep" or "Recreation" or "Haul" or "Mine" or "Research" or "Move" or "Tend" or "Rescue" or "PatientRest" or "Equip" or "Loot" or "Prepare" or "Ruin" or "Clear" or "Firefight" or "Husbandry" or "Repair" or "Refuel" or "Hunt" or "Provision" or "RegionalTrade" or "Institution" or "Campaign") || j.Claims==null || j.Cargo==null || j.Path==null || j.Sources==null || j.Amounts==null || !Colony.People.ContainsKey(j.PawnId) || !owners.Add(j.PawnId) || j.Cell<0 || j.Cell>=Cells.Count || !double.IsFinite(j.WorkCredit) || j.WorkCredit is <0 or >1 || j.Work<0 || j.Sources.Count!=j.Amounts.Count || j.Amounts.Any(n=>n<=0) || j.Path.Any(i=>i<0 || i>=Cells.Count) || j.Cargo.Any(k=>!d.Entities.ContainsKey(k.Key) || k.Value<=0) || j.Claims.Any(k=>Autonomy.Claims.GetValueOrDefault(k)!=j.PawnId))throw new InvalidDataException("Invalid job, path, cargo or claim.");
        }
        if(Autonomy.Claims.Any(c=>!Colony.People.ContainsKey(c.Value)))throw new InvalidDataException("Claim owner is missing.");
        if(Autonomy.Bills.Select(b=>b.Id).Distinct().Count()!=Autonomy.Bills.Count || Autonomy.Bills.Any(b=>b.Id<1 || b.Id>=Colony.NextObjectId || Find(b.BenchId)==null || !d.Document.Recipes.Any(r=>r.Id==b.RecipeId) || b.Target is <1 or >1000 || b.Completed<0 || b.Remaining is <0 or >1000 || b.IngredientRadius is <1 or >256 || b.WorkerId!=0 && !Colony.People.ContainsKey(b.WorkerId)))throw new InvalidDataException("Invalid production bill.");
        if(Autonomy.Crops.Any(c=>c.Key!=c.Value.Cell || c.Key<0 || c.Key>=Cells.Count || c.Value.Stress<0 || c.Value.GrowthTicks<0 || c.Value.GrowthTicks>Tick || c.Value.PlantedAt<0 || c.Value.PlantedAt>Tick || c.Value.OutputId is not ("grain" or "fungus" or "f-herbs")))throw new InvalidDataException("Invalid crop.");
        foreach(var p in Colony.People.Values)if(!double.IsFinite(p.Food+p.Rest+p.Recreation) || p.Food is <0 or >1000 || p.Rest is <0 or >1000 || p.Recreation is <0 or >1000)throw new InvalidDataException("Invalid needs.");
    }
    // Reproducible acceptance camp; player still sets priorities and changes every policy through normal UI.
    public void SetupSurvivalCamp(Definitions d)
    {
        int cx=Width/2,cy=Height/2;
        Apply(new(CommandKind.CreateZone,DefinitionId:"Growing",X:cx-8,Y:cy+5,Width:5,Height:4),d);
        Apply(new(CommandKind.CreateZone,DefinitionId:"Stockpile",X:cx+2,Y:cy+4,Width:6,Height:3),d);
        Drop("wood",180,Index(cx-3,cy+3),d);Drop("blocks",100,Index(cx-4,cy+3),d);
        Apply(new(CommandKind.CreateBlueprint,DefinitionId:"stove",X:cx-5,Y:cy-3,Text:"basalt"),d);
        Apply(new(CommandKind.CreateBlueprint,DefinitionId:"bed",X:cx-8,Y:cy-3,Text:"timber"),d);
        // A stove bill is added when the stove is built via UI or acceptance driver, not a hidden free meal source.
    }
}
