namespace Lastlight.Core;
public sealed partial class World
{
 private ColonyJob? PickPhysicalJob(Entity actor,PawnProfile profile,string category,Definitions d)
 {
  if(!Physical.Enabled)return null;int here=Index(actor.X,actor.Y,actor.Z);
  if(category=="Firefight")foreach(int cell in Physical.Fires.Keys.Order())if(!Autonomy.Claims.ContainsKey("fire:"+cell)&&FindPath(actor,profile,cell,true,d)!=null)return new(){PawnId=actor.Id,Kind="Firefight",Cell=cell,Claims=["fire:"+cell]};
  if(category is "Construct" or "Haul")foreach(var pair in Physical.Devices.OrderBy(p=>p.Key))
  {var e=Find(pair.Key);if(e==null||e.Forbidden||Autonomy.Claims.ContainsKey("device:"+e.Id)||FindPath(actor,profile,Index(e.X,e.Y,e.Z),true,d)==null)continue;bool repair=category=="Construct"&&pair.Value.Enabled&&pair.Value.Fault&&WaterDepth(Index(e.X,e.Y,e.Z))<2;bool fuel=category=="Haul"&&e.DefinitionId is "generator" or "hearth" or "torch"&&pair.Value.Enabled&&pair.Value.Fuel<2;
   if(!repair&&!fuel)continue;var job=new ColonyJob{PawnId=actor.Id,Kind=repair?"Repair":"Refuel",TargetId=e.Id,Cell=Index(e.X,e.Y,e.Z),Claims=["device:"+e.Id]};if(ReserveIngredients(job,new(){{repair?"f-parts":"wood",repair?1:5}},d))return job;}
  if(category=="Hunt")foreach(var mark in Colony.Designations.Where(m=>m.Kind=="Hunt").OrderBy(m=>m.Id)){var prey=Entities.FirstOrDefault(e=>e.DefinitionId=="grazer"&&e.X==mark.X&&e.Y==mark.Y&&e.Z==mark.Z&&Body(e.Id) is {Dead:false});if(prey!=null&&!Autonomy.Claims.ContainsKey("animal:"+prey.Id)&&FindPath(actor,profile,Index(prey.X,prey.Y,prey.Z),true,d)!=null)return new(){PawnId=actor.Id,Kind="Hunt",TargetId=prey.Id,Cell=Index(prey.X,prey.Y,prey.Z),Claims=["animal:"+prey.Id]};}
  if(category=="Handle")foreach(var pair in Physical.Animals.OrderBy(p=>p.Key))
  {var a=pair.Value;var e=Find(pair.Key);if(e==null||e.Forbidden||Body(e.Id) is not {Dead:false,Downed:false}||Autonomy.Claims.ContainsKey("animal:"+e.Id)||FindPath(actor,profile,Index(e.X,e.Y,e.Z),true,d)==null)continue;var policy=Colony.Animals[e.Id];if(policy.HandlerId!=0&&policy.HandlerId!=actor.Id)continue;
   string task=a.Request!=""?a.Request:a.Tame&&a.Nutrition<65?"feed":a.Tame&&Tick>=a.NextProduct?"collect":"";if(task==""||task=="collect"&&Tick<a.NextProduct||task=="train"&&a.Training>=3)continue;
   var job=new ColonyJob{PawnId=actor.Id,Kind="Husbandry",DefinitionId=task,TargetId=e.Id,Cell=Index(e.X,e.Y,e.Z),Claims=["animal:"+e.Id]};if(task is "feed" or "tame" or "train"&&!ReserveIngredients(job,new(){{"grain",1}},d))continue;return job;
  }
  return null;
 }
 private bool ValidPhysicalJob(ColonyJob job,PawnProfile p)
 {if(!Physical.Enabled||p.Drafted||job.Sources.Any(id=>Find(id)==null||Find(id)!.Forbidden))return false;int work=job.Kind=="Firefight"?0:job.Kind=="Hunt"?7:job.Kind=="Husbandry"?5:job.Kind=="Repair"?8:13;if(p.Priorities[work]==0)return false;return job.Kind=="Firefight"?Physical.Fires.ContainsKey(job.Cell):job.Kind is "Husbandry" or "Hunt"?Body(job.TargetId) is {Dead:false,Downed:false}:Find(job.TargetId)!=null;}
 private bool HandlePhysicalJob(ColonyJob job,Entity actor,Definitions d)
 {
  if(job.Kind is not ("Firefight" or "Husbandry" or "Repair" or "Refuel" or "Hunt"))return false;
  if(job.Kind=="Hunt"){TryAttack(actor.Id,job.TargetId,d);return true;}
  if(job.Kind=="Husbandry"){var target=Find(job.TargetId);if(target==null){Interrupt(actor.Id,d);return true;}job.Cell=Index(target.X,target.Y,target.Z);if(!HorizontalNeighbors(job.Cell).Contains(Index(actor.X,actor.Y,actor.Z)))return true;}
  job.WorkCredit+=Capacity(actor.Id).Manipulation;if(job.WorkCredit<1)return true;job.WorkCredit--;job.Work++;
  if(job.Kind=="Firefight")
  {if(Physical.Fires.TryGetValue(job.Cell,out var fire)){int source=HorizontalNeighbors(Index(actor.X,actor.Y,actor.Z)).FirstOrDefault(i=>WaterDepth(i)>0,-1);if(source>=0){EvaporateWater(source,1);fire.Intensity-=1;}else fire.Intensity-=.06;if(fire.Intensity<=0){Physical.Fires.Remove(job.Cell);Physical.Extinguished++;PhysicalNote("Response","A firefighter extinguished the blaze.",job.Cell);Interrupt(actor.Id,d);}}return true;}
  if(job.Work<(job.Kind=="Husbandry"?60:45))return true;
  if(job.Kind=="Repair")
  {var state=Physical.Devices[job.TargetId];if(WaterDepth(job.Cell)>=2){Interrupt(actor.Id,d);return true;}state.Fault=false;state.Condition=100;Physical.Repairs++;PhysicalNote("Response","Repaired "+Find(job.TargetId)!.Name+" using machine parts.",job.Cell);}
  else if(job.Kind=="Refuel")Physical.Devices[job.TargetId].Fuel+=job.Cargo.GetValueOrDefault("wood")*3;
  else
  {var a=Physical.Animals[job.TargetId];if(job.DefinitionId=="tame"){a.Taming=Math.Min(100,a.Taming+40);a.Tame=a.Taming>=100;if(a.Tame)a.Request="";}if(job.DefinitionId=="feed")a.Nutrition=Math.Min(100,a.Nutrition+40);if(job.DefinitionId=="train")a.Training=Math.Min(3,a.Training+1);if(job.DefinitionId=="collect"&&Tick>=a.NextProduct){Drop("fiber",4+a.Training,Index(actor.X,actor.Y,actor.Z),d);a.NextProduct=Tick+TicksPerDay;Physical.Products++;}if(job.DefinitionId!="tame"||a.Tame)a.Request="";}
  job.Cargo.Clear();Interrupt(actor.Id,d);return true;
 }
 private void AdvanceHusbandry(Definitions d)
 {
  foreach(var pair in Physical.Animals.OrderBy(p=>p.Key).ToArray())
  {
   var a=pair.Value;var e=Find(a.Id);if(e==null)continue;var body=Body(a.Id)!;
   if(body.Dead){if(!a.DeathCounted){a.DeathCounted=true;Physical.Population=Math.Max(0,Physical.Population-1);Drop("f-meat",6,Index(e.X,e.Y,e.Z),d);Colony.Designations.RemoveAll(m=>m.Kind=="Hunt"&&m.X==e.X&&m.Y==e.Y&&m.Z==e.Z);}continue;}
   a.Nutrition=Math.Max(0,a.Nutrition-.04);int cell=Index(e.X,e.Y,e.Z);
   if(Tick%150==0&&!Autonomy.Claims.ContainsKey("animal:"+e.Id)&&!body.Downed)
   {if(Cells[cell].TerrainId=="soil"&&WaterDepth(cell)<3&&Physical.PastureUse.GetValueOrDefault(cell)<6){Physical.PastureUse[cell]=Physical.PastureUse.GetValueOrDefault(cell)+1;a.Nutrition=Math.Min(100,a.Nutrition+6);}else{int next=HorizontalNeighbors(cell).FirstOrDefault(i=>Traversable(i,d)&&Cells[i].TerrainId=="soil"&&Physical.PastureUse.GetValueOrDefault(i)<6,-1);if(next>=0)MoveActor(e,next);}}
   if(WaterConcentration(cell)>.2)a.Disease=Math.Min(100,a.Disease+.08);else if(a.Nutrition>50)a.Disease=Math.Max(0,a.Disease-.025);
   if(Tick%TicksPerHour==0){if(a.Nutrition==0)InflictWound(e.Id,"torso","Bruise",4,0,d);if(a.Disease>30)InflictWound(e.Id,"torso","Poison",2,0,d);}
   if(Physical.Population>=3&&a.Tame&&a.Nutrition>=70&&a.Disease<10&&Tick>=a.NextBirth&&Physical.Animals.Count(x=>Body(x.Key) is {Dead:false})<16 && Physical.Animals.Values.Count(x=>x.Tame&&!x.DeathCounted)<8&&Physical.Animals.Values.Any(other=>other.Id!=a.Id&&other.Tame&&other.Nutrition>=70&&Find(other.Id) is {} mate&&mate.Z==e.Z&&Distance(e,mate)<=6))
   {int birth=HorizontalNeighbors(cell).FirstOrDefault(i=>Traversable(i,d)&&WaterDepth(i)<3,-1);if(birth>=0){var pos=Position(birth);var result=Apply(new(CommandKind.Spawn,DefinitionId:"grazer",X:pos.X,Y:pos.Y,Z:pos.Z),d);if(result.Accepted){var child=Physical.Animals[result.EntityId];child.Tame=true;child.Nutrition=70;child.NextBirth=Tick+TicksPerDay*7;a.NextBirth=Tick+TicksPerDay*7;Physical.Births++;Physical.Population=Math.Min(20,Physical.Population+.5);PhysicalNote("Ecology","A well-fed grazer pair produced a calf.",birth);}}}
  }
  long day=Tick/TicksPerDay;if(day>Physical.LastDay){Physical.LastDay=day;foreach(int key in Physical.PastureUse.Keys.ToArray()){Physical.PastureUse[key]=Math.Max(0,Physical.PastureUse[key]-3);if(Physical.PastureUse[key]==0)Physical.PastureUse.Remove(key);}Physical.Population=Math.Min(20,Physical.Population+.15);
   if(Physical.Population>=6&&Physical.Animals.Count(x=>Body(x.Key) is {Dead:false})<16&&Physical.Animals.Values.Count(a=>!a.Tame&&!a.DeathCounted)<12){var random=new StableRandom(Physical.EcologyRandom);for(int attempt=0;attempt<32;attempt++){int x=random.Next(Width),y=random.Next(Height),cell=Index(x,y);if(Cells[cell].TerrainId=="soil"&&Traversable(cell,d)&&WaterDepth(cell)<3&&!Entities.Any(e=>e.X==x&&e.Y==y&&e.Z==0)){var result=Apply(new(CommandKind.Spawn,DefinitionId:"grazer",X:x,Y:y),d);if(result.Accepted){Physical.Population-=1;break;}}}Physical.EcologyRandom=random.State;}
   foreach(var e in Entities.Where(e=>d.Entities[e.DefinitionId].Kind==EntityKind.ItemStack).ToArray()){var prop=MaterialProperties(e,d);if(prop.Decay>0){e.Health=Math.Max(0,e.Health-(int)Math.Ceiling(prop.Decay*10));if(e.Health==0&&!Autonomy.Claims.ContainsKey("item:"+e.Id))Apply(new(CommandKind.Remove,e.Id),d);}}}
 }
 private void RecordCraft(ColonyJob j,Entity actor,RecipeDefinition recipe,Definitions d,int cell)
 {
  if(!Physical.Enabled)return;string material=recipe.OutputId is "padded-armor" or "f-cloth"?"f-woven-cloth":recipe.OutputId is "lens" or "f-glass"?"f-memory-glass":recipe.OutputId is "f-ingot" or "f-parts"?"f-copper":"timber";
  int quality=Math.Clamp(40+Colony.People[actor.Id].Skills[11]*3+(int)(d.Document.Materials.First(m=>m.Id==material).Hardness*2+MaterialProperties(new Entity{DefinitionId=recipe.OutputId,MaterialId=material},d).Toughness),1,100);Physical.Crafts.Add(new(){Tick=Tick,Maker=actor.Id,Recipe=recipe.Id,Material=material,Quality=quality,Name=actor.Name+"'s "+d.Entities[recipe.OutputId].Name});if(Physical.Crafts.Count>128)Physical.Crafts.RemoveAt(0);
  if(d.Document.Weapons.Any(x=>x.Id==recipe.OutputId)||recipe.OutputId is "padded-armor" or "lens")foreach(var output in Entities.Where(e=>e.DefinitionId==recipe.OutputId&&Index(e.X,e.Y,e.Z)==cell)){output.MaterialId=material;output.CraftQuality=quality;output.MakerId=actor.Id;output.Name=actor.Name+"'s "+d.Entities[recipe.OutputId].Name;}
 }
}
