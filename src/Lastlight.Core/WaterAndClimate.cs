namespace Lastlight.Core;
public sealed partial class World
{
 private bool FluidOpen(int cell,Definitions d)
 {if(cell<0||cell>=Cells.Count||Cells[cell].TerrainId is "stone"||Sealed(cell))return false;var pos=Position(cell);return !Entities.Any(e=>BuildingCells(e).Contains(pos)&&(e.DefinitionId is "wall" or "column" or "sealed-door" or "f-pump" or "f-wheel" || e.DefinitionId is "door" or "f-floodgate" && !Physical.Devices.GetValueOrDefault(e.Id,new()).Open));}
 public void AddWater(int cell,int depth,double concentration,Definitions d)
 {if(!Physical.Enabled||cell<0||cell>=Cells.Count||depth<1||depth>7||!double.IsFinite(concentration)||concentration is <0 or >1||!FluidOpen(cell,d))throw new InvalidDataException("Invalid water input.");var water=Physical.Water.GetValueOrDefault(cell,new());int amount=Math.Min(depth,7-water.Depth);water.Depth+=amount;water.Contaminant+=amount*concentration;Physical.Water[cell]=water;Physical.InjectedWater+=amount;Physical.InjectedContaminant+=amount*concentration;}
 private int TransferWater(int from,int to,int maximum,Definitions d)
 {
  if(from==to||!Physical.Water.TryGetValue(from,out var a)||a.Depth==0||!FluidOpen(to,d))return 0;var b=Physical.Water.GetValueOrDefault(to,new());int n=Math.Min(maximum,Math.Min(a.Depth,7-b.Depth));if(n<=0)return 0;double mass=a.Contaminant*n/a.Depth;a.Depth-=n;a.Contaminant-=mass;b.Depth+=n;b.Contaminant+=mass;Physical.Water[to]=b;if(a.Depth==0)Physical.Water.Remove(from);return n;
 }
 private void EvaporateWater(int cell,int maximum)
 {if(!Physical.Water.TryGetValue(cell,out var f))return;int n=Math.Min(f.Depth,maximum);double mass=f.Contaminant*n/f.Depth;f.Depth-=n;f.Contaminant-=mass;Physical.Evaporated+=n;Physical.EvaporatedContaminant+=mass;if(f.Depth==0)Physical.Water.Remove(cell);}
 private void AdvanceWater(Definitions d)
 {
  // Each directed transfer mixes source mass conservatively. Active cells only; stable order and bounded rates.
  foreach(int cell in Physical.Water.Keys.Order().ToArray())
  {
   bool falling=Slice.Links.Any(l=>l.Enabled&&l.A==cell)||Cells[cell].TerrainId=="void";if(!falling&&!HorizontalNeighbors(cell).Any(n=>WaterDepth(cell)-WaterDepth(n)>=2))continue;
   if(!FluidOpen(cell,d))continue;
   foreach(var link in Slice.Links.Where(l=>l.Enabled&&l.A==cell))TransferWater(cell,link.B,2,d);
   var p=Position(cell);if(Cells[cell].TerrainId=="void"&&p.Z+1<Levels)TransferWater(cell,Index(p.X,p.Y,p.Z+1),2,d);
   foreach(int next in HorizontalNeighbors(cell).Order()){int difference=WaterDepth(cell)-WaterDepth(next);if(difference>=2)TransferWater(cell,next,1,d);}
  }
  foreach(var e in Entities.Where(e=>e.DefinitionId=="f-pump"&&DevicePowered(e.Id)).OrderBy(e=>e.Id))
  {int dx=e.Rotation%2==0?1:0,dy=e.Rotation%2==0?0:1;if(e.Rotation>=2){dx=-dx;dy=-dy;}if(Contains(e.X-dx,e.Y-dy,e.Z)&&Contains(e.X+dx,e.Y+dy,e.Z)){int amount=TransferWater(Index(e.X-dx,e.Y-dy,e.Z),Index(e.X+dx,e.Y+dy,e.Z),2,d);Physical.Devices[e.Id].Cause=amount>0?"Pumping 2 units/action maximum":"No inlet water or full outlet";}}
  foreach(var e in Entities.Where(e=>e.DefinitionId=="f-filter"&&DevicePowered(e.Id)))foreach(int cell in HorizontalNeighbors(Index(e.X,e.Y,e.Z)))if(Physical.Water.TryGetValue(cell,out var f)){double removed=f.Contaminant*.1;f.Contaminant-=removed;Physical.FilteredContaminant+=removed;}
  if(Tick%300==0)foreach(var e in Entities.Where(e=>Colony.People.ContainsKey(e.Id)&&!IsAway(e.Id)))
  {int cell=Index(e.X,e.Y,e.Z);if(WaterDepth(cell)>=5){InflictWound(e.Id,"torso","Bruise",2,0,d);if(Tick%TicksPerHour==300)PhysicalNote("Flood","Deep floodwater is injuring residents and blocking routes.",cell);}if(WaterDepth(cell)>0&&WaterConcentration(cell)>.2)InflictWound(e.Id,"torso","Poison",1,0,d);}
 }
 private void RebuildRooms(Definitions d)
 {
  var previous=Physical.Regions;var old=Physical.Rooms;var regions=new Dictionary<int,int>();var rooms=new Dictionary<int,RoomClimate>();
  var barriers=Entities.Where(e=>e.DefinitionId is "wall" or "column" or "sealed-door" or "f-pump" or "door" or "f-floodgate" or "f-vent").SelectMany(BuildingCells).Select(p=>Index(p.X,p.Y,p.Z)).ToHashSet();
  for(int start=0;start<Cells.Count;start++)
  {
   if(regions.ContainsKey(start)||barriers.Contains(start)||!d.Terrain[Cells[start].TerrainId].Walkable)continue;var queue=new Queue<int>();queue.Enqueue(start);regions[start]=start;int count=0,roof=0;double temp=0,smoke=0,insulation=0;int prior=0;
   while(queue.Count>0){int cell=queue.Dequeue();count++;if(Slice.Roofs.Contains(cell))roof++;if(previous.TryGetValue(cell,out int id)&&old.TryGetValue(id,out var climate)){temp+=climate.Temperature;smoke+=climate.Smoke;prior++;}foreach(int next in HorizontalNeighbors(cell))if(!regions.ContainsKey(next)&&!barriers.Contains(next)&&d.Terrain[Cells[next].TerrainId].Walkable){regions[next]=start;queue.Enqueue(next);}}
   var level=Position(start).Z;foreach(var e in Entities.Where(e=>e.Z==level&&e.DefinitionId=="wall")){var props=MaterialProperties(e,d);insulation+=props.Insulation;}
   rooms[start]=new(){Id=start,Level=level,Cells=count,Outdoors=level==0&&roof<count*.75,Temperature=prior>0?temp/prior:level==0?SolarAmbient(Physical.Weather):level==1?4:-4,Smoke=prior>0?smoke/prior:0,Insulation=Math.Min(.95,insulation/Math.Max(1,count))};
  }
  Physical.Regions=regions;Physical.Rooms=rooms;
 }
 public MaterialPhysicsDefinition MaterialProperties(Entity e,Definitions d)
 {string id=e.MaterialId!=""?e.MaterialId:e.DefinitionId is "wood" or "fiber" or "f-cloth" or "f-loom" or "f-fermenter" or "bed" or "table" or "stool" or "workbench"?"timber":e.DefinitionId is "brass" or "conduit" or "generator"||e.DefinitionId.StartsWith("f-",StringComparison.Ordinal)?"sun-brass":"basalt";return d.Document.MaterialPhysics.FirstOrDefault(m=>m.Id==id)??new(id,4,0,.4,.8,.1,0);}
 private void AdvanceHeatAndFire(Definitions d)
 {
  foreach(var room in Physical.Rooms.Values)
  {
   double ambient=room.Level==0?SolarAmbient(Physical.Weather):room.Level==1?4:-4;
   if(Slice.Ruin.EngineRestored&&Find(Slice.Ruin.EngineId) is {} engine&&Physical.Regions.GetValueOrDefault(Index(engine.X,engine.Y,engine.Z),-1)==room.Id)ambient+=12;
   room.Temperature+=(ambient-room.Temperature)*(room.Outdoors?.1:.008*(1-room.Insulation));room.Smoke*=room.Outdoors?.8:.995;
  }
  foreach(var e in Entities.Where(e=>Physical.Devices.ContainsKey(e.Id)))
  {
   if(!Physical.Regions.TryGetValue(Index(e.X,e.Y,e.Z),out int roomId))continue;var room=Physical.Rooms[roomId];var state=Physical.Devices[e.Id];
   if(e.DefinitionId=="f-heater"&&DevicePowered(e.Id))room.Temperature+=Math.Max(0,22-room.Temperature)*.04;
   if(e.DefinitionId is "hearth" or "torch"&&state.Enabled&&state.Fuel>0){room.Temperature+=e.DefinitionId=="hearth"?.2:.03;if(Tick%900==0)state.Fuel--;}
  }
  foreach(var ember in Slice.Effects.Where(e=>e.Kind=="Light"&&e.ExpiresAt>Tick))if(Physical.Regions.TryGetValue(ember.Cell,out int emberRoom))Physical.Rooms[emberRoom].Temperature+=Math.Max(0,14-Physical.Rooms[emberRoom].Temperature)*.03;
  foreach(var portal in Entities.Where(e=>e.DefinitionId is "door" or "f-vent" or "f-floodgate"))
  {var adjacent=HorizontalNeighbors(Index(portal.X,portal.Y,portal.Z)).Where(Physical.Regions.ContainsKey).Select(i=>Physical.Regions[i]).Distinct().Order().Take(2).ToArray();if(adjacent.Length!=2)continue;var a=Physical.Rooms[adjacent[0]];var b=Physical.Rooms[adjacent[1]];var state=Physical.Devices.GetValueOrDefault(portal.Id,new());double exchange=(a.Temperature-b.Temperature)*(portal.DefinitionId=="f-vent"||state.Open?.04:.002);exchange*=Math.Min(a.Cells,b.Cells);a.Temperature-=exchange/a.Cells;b.Temperature+=exchange/b.Cells;double gas=(a.Smoke-b.Smoke)*.02;a.Smoke-=gas;b.Smoke+=gas;}
  foreach(var pair in Physical.Fires.OrderBy(p=>p.Key).ToArray())
  {
   int cell=pair.Key;var fire=pair.Value;if(WaterDepth(cell)>0){EvaporateWater(cell,1);fire.Intensity-=2;}
   var fuel=Entities.Where(e=>Index(e.X,e.Y,e.Z)==cell&&MaterialProperties(e,d).Flammability>0).OrderBy(e=>e.Id).FirstOrDefault();
   if(fuel==null)fire.Intensity-=.08;else{fire.Intensity=Math.Min(5,fire.Intensity+.015);fire.Work++;if(fire.Work>=90){fire.Work=0;if(d.Entities[fuel.DefinitionId].Kind==EntityKind.ItemStack){fuel.Quantity--;if(fuel.Quantity==0)Apply(new(CommandKind.Remove,fuel.Id),d);}else Apply(new(CommandKind.Remove,fuel.Id),d);}}
   if(Physical.Regions.TryGetValue(cell,out int roomId)){Physical.Rooms[roomId].Temperature+=fire.Intensity*.12;Physical.Rooms[roomId].Smoke=Math.Min(20,Physical.Rooms[roomId].Smoke+fire.Intensity*.02);}
   if(fire.Intensity<=0){Physical.Fires.Remove(cell);Physical.Extinguished++;continue;}
   if(Tick%300==0){foreach(var e in Entities.Where(e=>Body(e.Id)!=null&&!IsAway(e.Id)&&Index(e.X,e.Y,e.Z)==cell).ToArray())InflictWound(e.Id,"torso","Burn",fire.Intensity*2,0,d);foreach(int next in HorizontalNeighbors(cell))if(Physical.Fires.Count<4096 && !Physical.Fires.ContainsKey(next)&&Entities.Any(e=>Index(e.X,e.Y,e.Z)==next&&MaterialProperties(e,d).Flammability>0)){Physical.Fires[next]=new(){Intensity=.5};break;}}
  }
  foreach(var room in Physical.Rooms.Values)room.Temperature=Math.Clamp(room.Temperature,-100,1000);
  if(Tick%300==0)foreach(var e in Entities.Where(e=>Colony.People.ContainsKey(e.Id)&&!IsAway(e.Id))){int cell=Index(e.X,e.Y,e.Z);if(Physical.Regions.TryGetValue(cell,out int roomId)){var room=Physical.Rooms[roomId];if(room.Temperature>48)InflictWound(e.Id,"torso","Burn",1,0,d);if(room.Smoke>3)InflictWound(e.Id,"left-lung","Poison",1,0,d);}}
 }
}
