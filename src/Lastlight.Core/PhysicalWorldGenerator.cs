namespace Lastlight.Core;
public static class PhysicalWorldGenerator
{
 public static World Create(Definitions d,int seed)
 {
  var w=VerticalWorldGenerator.Create(d,seed);w.EnablePhysical(d);int cx=w.Width/2,cy=w.Height/2;
  long Spawn(string id,int x,int y,int count=1){w.CellAt(x,y,0).TerrainId="soil";var result=w.Apply(new(CommandKind.Spawn,DefinitionId:id,X:x,Y:y,Quantity:count),d);if(!result.Accepted)throw new InvalidDataException(result.Message);return result.EntityId;}
  for(int y=cy-7;y<=cy-1;y++)for(int x=cx-23;x<=cx-17;x++)w.CellAt(x,y,0).TerrainId="soil";
  for(int y=cy-7;y<=cy-1;y++)for(int x=cx-23;x<=cx-17;x++)if(x==cx-23||x==cx-17||y==cy-7||y==cy-1)Spawn("wall",x,y);
  for(int y=cy-6;y<=cy-2;y++)for(int x=cx-22;x<=cx-18;x++)w.AddWater(w.Index(x,y),7,0,d);
  long gen=Spawn("generator",cx-14,cy-5);w.Physical.Devices[gen].Fuel=15;Spawn("conduit",cx-12,cy-5);Spawn("f-battery",cx-11,cy-5);Spawn("f-heater",cx-10,cy-5);Spawn("f-smelter",cx-10,cy-4);Spawn("f-kiln",cx-11,cy-4);Spawn("f-loom",cx-13,cy-1);Spawn("f-fermenter",cx-11,cy-1);
  Spawn("f-wheel",cx-16,cy-3);Spawn("f-shaft",cx-15,cy-3);Spawn("f-mill",cx-14,cy-3);Spawn("f-resonator",cx-14,cy+1);Spawn("f-channel",cx-13,cy+1);Spawn("f-ward",cx-12,cy+1);
  foreach(var supply in new[]{("f-ore",45),("f-sand",45),("fiber",45),("f-herbs",30),("f-parts",20)})Spawn(supply.Item1,cx-12,cy+3,supply.Item2);
  foreach(var id in w.Colony.People.Keys){w.Colony.People[id].Priorities[0]=1;w.Colony.People[id].Priorities[5]=2;w.Colony.People[id].Priorities[11]=2;}
  w.RefreshEngineering(d);w.Record("The observatory camp has a finite cistern and an engineering yard. Inspect Engineering for flow, climate, power and animal care.");w.Validate(d);return w;
 }
}
