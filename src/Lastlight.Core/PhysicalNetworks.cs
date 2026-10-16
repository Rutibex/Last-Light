namespace Lastlight.Core;
public sealed partial class World
{
 public bool NetworkAdjacent(Entity a,Entity b)=>a.Z==b.Z&&BuildingCells(a).Any(p=>BuildingCells(b).Any(q=>Math.Abs(p.X-q.X)+Math.Abs(p.Y-q.Y)<=(a.DefinitionId=="f-pump"||b.DefinitionId=="f-pump"?2:1)));
 public void RefreshEngineering(Definitions d){EnsurePhysical(d);RebuildRooms(d);AdvanceNetworks(d);}
 private static string NetworkKind(string id)=>id is "f-wheel" or "f-shaft" or "f-mill"?"Mechanical":id is "f-resonator" or "f-channel" or "f-ward" or "circle"?"Thaumic":id is "generator" or "conduit" or "sunlamp" or "f-pump" or "f-battery" or "f-heater" or "f-solar" or "f-smelter" or "f-kiln" or "f-filter"?"Electrical":"";
 private static double Demand(string id)=>id switch {"f-pump"=>4,"f-heater"=>4,"sunlamp"=>3,"f-smelter"=>6,"f-kiln"=>5,"f-filter"=>2,"f-mill"=>4,"f-ward"=>8,"circle"=>6,_=>0};
 private void AdvanceNetworks(Definitions d)
 {
  var oldCause=Physical.Devices.ToDictionary(k=>k.Key,k=>k.Value.Cause);foreach(var dev in Physical.Devices.Values){dev.Supplied=false;dev.Network=0;}
  var devices=Entities.Where(e=>Physical.Devices.ContainsKey(e.Id)&&NetworkKind(e.DefinitionId)!="").OrderBy(e=>e.Id).ToList();
  foreach(var e in devices)
  {var state=Physical.Devices[e.Id];int depth=WaterDepth(Index(e.X,e.Y,e.Z));if(depth>=2&&NetworkKind(e.DefinitionId)=="Electrical"&&!state.Fault){state.Fault=true;PhysicalNote("Outage","Wet electrical short: "+e.Name+". Drain the cell, then repair with machine parts.",Index(e.X,e.Y,e.Z));}if(depth>0)state.Condition=Math.Max(0,state.Condition-(1-MaterialProperties(e,d).CorrosionResistance)*.4);}
  Physical.Networks.Clear();var seen=new HashSet<long>();int number=1;
  foreach(var root in devices)
  {
   if(!seen.Add(root.Id))continue;string kind=NetworkKind(root.DefinitionId);var net=new NetworkState{Id=number++,Kind=kind};var queue=new Queue<Entity>();queue.Enqueue(root);
   while(queue.Count>0){var e=queue.Dequeue();net.Members.Add(e.Id);foreach(var other in devices)if(!seen.Contains(other.Id)&&NetworkKind(other.DefinitionId)==kind&&NetworkAdjacent(e,other)){seen.Add(other.Id);queue.Enqueue(other);}}
   bool fault=net.Members.Any(id=>Physical.Devices[id].Fault);foreach(long id in net.Members){var e=Find(id)!;var state=Physical.Devices[id];state.Network=net.Id;if(!state.Enabled||state.Fault)continue;net.Demand+=Demand(e.DefinitionId);
    if(e.DefinitionId=="generator"&&state.Fuel>0){net.Supply+=12*MaterialProperties(e,d).Conductivity;if(Tick%900==0)state.Fuel--;}
    if(e.DefinitionId=="f-solar")net.Supply+=SolarPowerFactor()*10*Math.Max(0,Math.Sin(Math.PI*(Tick%TicksPerDay/(double)TicksPerHour-6)/12));
    if(e.DefinitionId=="f-wheel"&&HorizontalNeighbors(Index(e.X,e.Y,e.Z)).Any(i=>WaterDepth(i)>=2))net.Supply+=8;
    if(e.DefinitionId=="f-resonator")net.Supply+=12*state.Condition/100*Math.Min(1,d.Document.Materials.First(m=>m.Id==(e.MaterialId!=""?e.MaterialId:"sun-brass")).Resonance/4);
    if(e.DefinitionId=="f-battery")net.Stored+=state.Charge;
   }
   double available=fault?0:net.Supply;
   if(kind=="Electrical"&&!fault)
   {
    double missing=Math.Max(0,net.Demand-available)*10;
    foreach(long id in net.Members.Where(id=>Find(id)!.DefinitionId=="f-battery"&&Physical.Devices[id].Enabled)){var state=Physical.Devices[id];double used=Math.Min(missing,state.Charge);state.Charge-=used;missing-=used;available+=used/10;}
    double excess=Math.Max(0,available-net.Demand)*10;
    foreach(long id in net.Members.Where(id=>Find(id)!.DefinitionId=="f-battery"&&Physical.Devices[id].Enabled)){var state=Physical.Devices[id];double fill=Math.Min(excess,900-state.Charge);state.Charge+=fill;excess-=fill;}
   }
   net.Cause=fault?"Wet/faulted component":net.Demand<=available?"Supplied":kind=="Mechanical"?"Insufficient waterwheel flow / broken shaft":kind=="Thaumic"?"Resonance overload":"Generation/storage below demand";
   if(kind=="Thaumic"&&net.Demand>net.Supply)foreach(long id in net.Members){var state=Physical.Devices[id];state.Condition=Math.Max(0,state.Condition-.5);if(state.Condition<40)state.Fault=true;}
   double remaining=available;foreach(long id in net.Members.Order()){var state=Physical.Devices[id];double demand=Demand(Find(id)!.DefinitionId);state.Supplied=state.Enabled&&!state.Fault&&remaining>=demand;if(state.Supplied)remaining-=demand;state.Cause=!state.Enabled?"Disabled":state.Fault?"Faulted; drain and repair":state.Supplied?"Supplied":net.Cause;
    if(demand>0&&oldCause.GetValueOrDefault(id)=="Supplied"&&!state.Supplied)PhysicalNote("Outage",Find(id)!.Name+": "+state.Cause+".",Index(Find(id)!.X,Find(id)!.Y,Find(id)!.Z));}
   net.Stored=net.Members.Where(id=>Find(id)!.DefinitionId=="f-battery").Sum(id=>Physical.Devices[id].Charge);Physical.Networks.Add(net);
  }
 }
 public bool BenchReady(Entity bench,string recipe)=>!Physical.Enabled||RecipeBench(recipe)==bench.DefinitionId&&(NetworkKind(bench.DefinitionId)==""||DevicePowered(bench.Id));
}
