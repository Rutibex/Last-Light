namespace Lastlight.Core;
public sealed class FluidCell {public int Depth {get;set;} public double Contaminant {get;set;}}
public sealed class FireState {public double Intensity {get;set;}=1;public int Work {get;set;}}
public sealed class RoomClimate {public int Id {get;set;} public int Level {get;set;} public int Cells {get;set;} public bool Outdoors {get;set;} public double Temperature {get;set;} public double Smoke {get;set;} public double Insulation {get;set;}}
public sealed class DeviceState {public long Id {get;set;} public bool Enabled {get;set;}=true;public bool Fault {get;set;} public bool Open {get;set;} public bool Supplied {get;set;} public double Charge {get;set;} public double Condition {get;set;}=100;public int Network {get;set;} public int Fuel {get;set;} public string Cause {get;set;}="Disconnected";}
public sealed class NetworkState {public int Id {get;set;}public string Kind {get;set;}="Electrical";public List<long> Members {get;set;}=[];public double Supply {get;set;} public double Demand {get;set;} public double Stored {get;set;} public string Cause {get;set;}="";}
public sealed class HusbandryState {public long Id {get;set;} public double Nutrition {get;set;}=100;public bool Tame {get;set;}public int Taming {get;set;}public int Training {get;set;}public double Disease {get;set;}public long NextProduct {get;set;}public long NextBirth {get;set;} public string Request {get;set;}="";public bool DeathCounted {get;set;}}
public sealed class CraftRecord {public long Tick {get;set;}public long Maker {get;set;}public string Recipe {get;set;}="";public string Material {get;set;}="";public string Name {get;set;}="";public int Quality {get;set;}}
public sealed class PhysicalNotice {public long Tick {get;set;} public string Kind {get;set;}="";public string Text {get;set;}="";public int Cell {get;set;}}
public sealed class PhysicalState
{
 public bool Initialized {get;set;}public bool Enabled {get;set;}public Dictionary<int,FluidCell> Water {get;set;}=new();public Dictionary<int,FireState> Fires {get;set;}=new();public Dictionary<int,int> Regions {get;set;}=new();public Dictionary<int,RoomClimate> Rooms {get;set;}=new();public Dictionary<long,DeviceState> Devices {get;set;}=new();public List<NetworkState> Networks {get;set;}=[];public Dictionary<long,HusbandryState> Animals {get;set;}=new();public Dictionary<int,double> PastureUse {get;set;}=new();public List<CraftRecord> Crafts {get;set;}=[];public List<PhysicalNotice> Notices {get;set;}=[];public ulong EcologyRandom {get;set;}public double Population {get;set;}=8;public long LastDay {get;set;}public int HarvestFailures {get;set;}public int Births {get;set;}public int Products {get;set;}public int Extinguished {get;set;}public int Repairs {get;set;}public double Evaporated {get;set;}public double EvaporatedContaminant {get;set;}public double FilteredContaminant {get;set;}public double InjectedWater {get;set;}public double InjectedContaminant {get;set;}public double Weather {get;set;}=14;
}
public sealed partial class World
{
 public PhysicalState Physical {get;set;}=new();
 public static readonly string[] PhysicalDevices=["door","circle","generator","conduit","sunlamp","f-floodgate","f-pump","f-battery","f-heater","f-vent","f-solar","f-shaft","f-wheel","f-mill","f-resonator","f-channel","f-ward","f-smelter","f-kiln","f-loom","f-fermenter","f-filter","hearth","torch"];
 public void EnsurePhysical(Definitions d)
 {
  if(!Physical.Initialized){Physical.Initialized=true;Physical.EcologyRandom=(ulong)(uint)Seed+813;}
  foreach(var e in Entities){if(PhysicalDevices.Contains(e.DefinitionId)&&!Physical.Devices.ContainsKey(e.Id))Physical.Devices[e.Id]=new(){Id=e.Id};if(e.DefinitionId=="grazer"&&!Physical.Animals.ContainsKey(e.Id))Physical.Animals[e.Id]=new(){Id=e.Id,NextProduct=Tick+TicksPerDay,NextBirth=Tick+TicksPerDay*7};}
  foreach(long id in Physical.Devices.Keys.Where(id=>Find(id)==null).ToArray())Physical.Devices.Remove(id);
  foreach(long id in Physical.Animals.Keys.Where(id=>Find(id)==null).ToArray())Physical.Animals.Remove(id);
 }
 public void EnablePhysical(Definitions d)
 {
  EnsurePhysical(d);if(Physical.Enabled)return;Physical.Enabled=true;
  if(!Slice.Enabled)foreach(var crop in Autonomy.Crops.Values)crop.GrowthTicks=Tick-crop.PlantedAt;
  for(int i=0;i<Cells.Count;i++)if(Cells[i].TerrainId=="water")Physical.Water[i]=new(){Depth=7};
  Physical.InjectedWater=Physical.Water.Values.Sum(w=>w.Depth);RebuildRooms(d);AdvanceNetworks(d);
  PhysicalNote("Engineering","Physical systems enabled. Existing water is a finite reservoir; networks now supply devices and crops require a viable climate.",0);
 }
 private void PhysicalNote(string kind,string text,int cell)
 {Physical.Notices.Add(new(){Tick=Tick,Kind=kind,Text=text,Cell=cell});if(Physical.Notices.Count>128)Physical.Notices.RemoveAt(0);Record(text);if(kind is "Flood" or "Fire" or "Outage" or "Harvest")Danger.PauseSerial++;}
 public int WaterDepth(int cell)=>Physical.Enabled?Physical.Water.GetValueOrDefault(cell)?.Depth??0:0;
 public double WaterConcentration(int cell)=>Physical.Water.TryGetValue(cell,out var f)&&f.Depth>0?f.Contaminant/f.Depth:0;
 public double TemperatureAt(int cell)=>Physical.Enabled&&Physical.Regions.TryGetValue(cell,out int region)?Physical.Rooms[region].Temperature:WarmthAt(cell);
 public double MoistureAt(int cell)=>Math.Min(7,WaterDepth(cell)+HorizontalNeighbors(cell).Select(WaterDepth).DefaultIfEmpty().Max()+(Position(cell).Z==0?2:0));
 public bool DevicePowered(long id)=>!Physical.Enabled || Physical.Devices.TryGetValue(id,out var s)&&s.Enabled&&!s.Fault&&s.Supplied;
 public string CropFailure(CropState crop)
 {
  if(!Physical.Enabled)return "";double t=TemperatureAt(crop.Cell);if(WaterDepth(crop.Cell)>=4)return "Flooded roots";if(WaterConcentration(crop.Cell)>.2 || HorizontalNeighbors(crop.Cell).Any(i=>WaterConcentration(i)>.2))return "Contaminated irrigation";if(t<(crop.OutputId=="fungus"?2:8))return "Too cold";if(t>38)return "Too hot";if(MoistureAt(crop.Cell)<1)return "Dry substrate";if(LightAt(crop.Cell)<(crop.OutputId=="fungus"?5:60))return "Insufficient light";return "";
 }
 public string MaterialResource(string id)=>id switch {"timber"=>"wood","sun-brass"=>"brass","f-copper"=>"f-ingot","f-memory-glass"=>"f-glass","f-woven-cloth"=>"f-cloth","f-iron"=>"f-ingot",_=>"blocks"};
 public string RecipeBench(string id)=>id switch {"f-make-flour"=>"f-mill","f-smelt"=>"f-smelter","f-make-glass" or "f-lens"=>"f-kiln","f-make-cloth"=>"f-loom","f-make-ferment"=>"f-fermenter","simple-meal" or "fungus-meal" or "f-flour-meal" or "f-meat-meal"=>"stove",_=>"workbench"};
 private void AdvancePhysical(Definitions d)
 {
  EnsurePhysical(d);if(!Physical.Enabled)return;if(Tick%300==0){RebuildRooms(d);AdvanceNetworks(d);}
  AdvanceWater(d);AdvanceHeatAndFire(d);AdvanceHusbandry(d);
  foreach(var crop in Autonomy.Crops.Values.OrderBy(c=>c.Cell)){string cause=CropFailure(crop);if(!Slice.Enabled&&cause=="")crop.GrowthTicks+=30;if(cause!=""){crop.Stress+=30;if(crop.Stress>=TicksPerHour*3){Autonomy.Crops.Remove(crop.Cell);Physical.HarvestFailures++;PhysicalNote("Harvest","Crop failed: "+cause+". Inspect temperature, water and network supply.",crop.Cell);break;}}else crop.Stress=Math.Max(0,crop.Stress-60);}
 }
 private CommandResult ApplyPhysicalCommand(WorldCommand c,Definitions d)
 {
  if(c.Text=="enable"){EnablePhysical(d);return new(true,"Physical systems enabled.");}if(!Physical.Enabled)return new(false,"Enable physical systems first.");EnsurePhysical(d);
  if(c.Text is "toggle" or "gate" or "repair")
  {if(!Physical.Devices.TryGetValue(c.EntityId,out var state))return new(false,"Select an engineering device.");if(c.Text=="repair"){if(!state.Fault&&state.Condition>=100)return new(false,"This device does not need repair.");state.Fault=true;return new(true,"Repair requested; Construct workers need machine parts.");}if(c.Text=="gate"){if(Find(c.EntityId) is not {DefinitionId:"door" or "f-floodgate"})return new(false,"Select a door or floodgate.");state.Open=!state.Open;return new(true,state.Open?"Gate open.":"Gate closed.");}state.Enabled=!state.Enabled;return new(true,state.Enabled?"Device enabled.":"Device disabled.");}
  if(c.Text=="ignite"){var e=Find(c.EntityId);if(Physical.Fires.Count>=4096 || e==null||MaterialProperties(e,d).Flammability<=0)return new(false,"Select combustible material.");int cell=Index(e.X,e.Y,e.Z);Physical.Fires[cell]=new();PhysicalNote("Fire","Combustible material ignited. Firefighters and water can respond.",cell);return new(true,"Fire started.");}
  if(c.Text=="fuel")
  {if(!Physical.Devices.TryGetValue(c.EntityId,out var dev)||Find(c.EntityId) is not {DefinitionId:"generator" or "hearth" or "torch"})return new(false,"Select a fueled device.");dev.Cause="Fuel requested";return new(true,"Haulers will carry wood to refuel this device.");}
  if(c.Text is "tame" or "train" or "collect" or "feed")
  {if(!Physical.Animals.TryGetValue(c.EntityId,out var animal)||Body(c.EntityId) is not {Dead:false})return new(false,"Select a living grazer.");if(c.Text is "train" or "collect" && !animal.Tame)return new(false,"Tame the animal first.");animal.Request=c.Text;return new(true,"Handler job requested.");}
  return new(false,"Unknown physical action.");
 }
 public IEnumerable<ColonyAlert> PhysicalAlerts()
 {
  if(!Physical.Enabled)yield break;if(Physical.Fires.Count>0)yield return new("Active fire","Firefighters, water and noncombustible construction limit spread.","Critical");if(Physical.Networks.Any(n=>n.Demand>0&&n.Cause!="Supplied"))yield return new("Network interruption","Inspect graph supply, demand, charge, fuel and wet faults.","Critical");if(Autonomy.Crops.Values.FirstOrDefault(c=>CropFailure(c)!="") is {} crop)yield return new("Crop stress",CropFailure(crop)+" at "+Position(crop.Cell)+".","Warning");if(Physical.Animals.Values.Any(a=>a.Nutrition<30&&!a.DeathCounted))yield return new("Hungry animals","Feed grain or restore grazing access.","Warning");
 }
}
