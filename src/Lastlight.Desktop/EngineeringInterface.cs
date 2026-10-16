using Lastlight.Core;
using Microsoft.Xna.Framework;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private string engineeringPage="Water",physicalOverlay="Water";
 private long engineeringDevice,engineeringAnimal;
 private void DrawEngineeringPanel(Rectangle p)
 {
  int x=p.X+16,y=p.Y+48;string[] pages=["Water","Climate","Networks","Industry","Husbandry"];
  for(int i=0;i<pages.Length;i++){string page=pages[i];Button(new(x+i*112,y,106,27),page,()=>{engineeringPage=page;rowOffset=0;},engineeringPage==page,scale:.63f);}y+=38;
  if(!world.Physical.Enabled){Paragraph("Earlier saves retain their prior behavior. Enabling physical systems introduces finite water, temperature, crop stress, power demand, fire and animal nutrition.",new(x,y,p.Width-40,75),Text,.75f);Button(new(x,y+85,250,29),"Enable physical systems",()=>Execute(new(CommandKind.PhysicalAction,Text:"enable")),scale:.7f);return;}
  Button(new(p.Right-255,p.Y+48,220,27),"Overlay: "+physicalOverlay,()=>physicalOverlay=physicalOverlay=="Water"?"Heat":physicalOverlay=="Heat"?"Networks":"Water",scale:.66f);
  if(engineeringPage=="Water")
  {
   Label($"Water {world.Physical.Water.Values.Sum(f=>f.Depth)} | evaporated {world.Physical.Evaporated:0} | input {world.Physical.InjectedWater:0}",x,y,Cyan,.74f);y+=29;
   Label($"Contaminant mass {world.Physical.Water.Values.Sum(f=>f.Contaminant):0.00} | filtered {world.Physical.FilteredContaminant:0.00}",x,y,Muted,.69f);y+=35;
   Paragraph("Water depth is 0-7. Depth 4 blocks ordinary routes; 5 injures residents. Mixing preserves contaminant mass. Closed gates, walls and pump casings block passive flow. Pumps move up to two units/action from the rotated inlet to outlet; they need electrical supply; their terminals reach two tiles. Filters retain removed contaminant in the ledger. Open stairs let water fall to the next elevation.",new(x,y,p.Width-38,96),Text,.72f);y+=107;
   DrawEngineeringDeviceList(p,x,y,"water");
  }
  else if(engineeringPage=="Climate")
  {
   Label($"Outside {world.Physical.Weather:0} C | active fires {world.Physical.Fires.Count} | harvest failures {world.Physical.HarvestFailures}",x,y,Gold,.72f);y+=32;
   foreach(var room in world.Physical.Rooms.Values.Where(r=>r.Level==level).OrderBy(r=>r.Id).Skip(rowOffset).Take(4)){Label($"Region {room.Id} | {room.Cells} tiles | {room.Temperature:0.0} C | smoke {room.Smoke:0.0} | {(room.Outdoors?"outside":"enclosed")}",x,y,Text,.66f);y+=25;}
   Paragraph("Doors and vents exchange room heat; insulation reduces heat loss. Heaters require power. Fueled hearths use physically delivered wood. Fire consumes combustible stacks/buildings and generates smoke. Firefight workers use nearby water or beat out a blaze. Grain needs 8-38 C, usable light, clean moisture and unflooded roots. Three hours of continuous stress kills a planting.",new(x,y+10,p.Width-38,100),Muted,.68f);
  }
  else if(engineeringPage=="Networks")
  {
   foreach(var net in world.Physical.Networks.Skip(rowOffset).Take(3)){Label($"{net.Kind} #{net.Id}: {net.Cause}",x,y,net.Cause=="Supplied"?Cyan:Gold,.72f);Label($"Supply {net.Supply:0.0} | demand {net.Demand:0.0} | stored {net.Stored:0} | members "+string.Join(",",net.Members),x,y+23,Muted,.6f);y+=51;}
   DrawEngineeringDeviceList(p,x,y,"network");
  }
  else if(engineeringPage=="Industry")
  {
   Label("Grain > flour > meals | ore > ingots > parts | fiber > cloth > armor",x,y,Cyan,.71f);y+=30;
   Label("Fungus > ferment + herbs > medicine | sand > glass > lens",x,y,Text,.69f);y+=29;
   Paragraph("Select a production bench and open Bills. Recipes require compatible benches; powered machinery pauses work on outage while preserving held ingredients. Construct workers repair dry faults using one machine part. Haulers carry five wood to refuel a device. Material choice changes combustion, insulation, corrosion and network performance.",new(x,y,p.Width-38,86),Muted,.68f);y+=96;
   var benches=world.Entities.Where(e=>e.DefinitionId is "workbench" or "stove" or "f-mill" or "f-smelter" or "f-kiln" or "f-loom" or "f-fermenter").OrderBy(e=>e.Id).ToArray();
   for(int i=0;i<Math.Min(4,benches.Length);i++){var bench=benches[i];Button(new(x+i*200,y,190,28),Fit(bench.Name,180,.6f),()=>{SelectEntity(bench.Id);tab="Bills";},scale:.6f);}y+=40;
   var craft=world.Physical.Crafts.LastOrDefault();if(craft!=null)Label(Fit($"Last craft: {craft.Name} | quality {craft.Quality} | {craft.Material}",p.Width-38,.62f),x,y,Gold,.62f);
  }
  else
  {
   var animals=world.Physical.Animals.Keys.Order().ToArray();if(animals.Length==0){Label("No local grazers.",x,y,Muted);return;}if(!animals.Contains(engineeringAnimal))engineeringAnimal=animals[0];long id=engineeringAnimal;var a=world.Physical.Animals[id];
   Button(new(x,y,260,28),world.Find(id)!.Name+" #"+id,()=>engineeringAnimal=animals[(Array.IndexOf(animals,id)+1)%animals.Length],scale:.7f);y+=40;
   Label($"Nutrition {a.Nutrition:0}% | disease {a.Disease:0}% | {(a.Tame?"tame":"wild")} | training {a.Training}/3",x,y,Cyan,.72f);y+=35;
   string[] actions=["tame","train","feed","collect"];for(int i=0;i<actions.Length;i++){string action=actions[i];Button(new(x+i*135,y,125,28),action,()=>Execute(new(CommandKind.PhysicalAction,id,Text:action)),scale:.7f);}y+=44;
   Paragraph("Handlers carry grain for taming, training and feed. Grazers consume finite pasture, which regrows daily. Well-fed tame pairs reproduce on a seven-day cooldown within an sixteen-animal local budget. Contaminated water causes disease; clean conditions and nutrition support recovery. Training improves collected fiber yield. Overhunting lowers the regional population reserve.",new(x,y,p.Width-38,100),Text,.71f);
   Label($"Regional reserve {world.Physical.Population:0.0} | births {world.Physical.Births} | products {world.Physical.Products}",x,y+110,Gold,.7f);
  }
 }
 private void DrawEngineeringDeviceList(Rectangle p,int x,int y,string filter)
 {
  var devices=world.Physical.Devices.Keys.Where(id=>world.Find(id)!=null&&(filter!="water"||world.Find(id)!.DefinitionId is "f-pump" or "f-filter" or "f-floodgate")).Order().ToArray();if(devices.Length==0)return;
  if(!devices.Contains(engineeringDevice))engineeringDevice=devices[0];long id=engineeringDevice;var e=world.Find(id)!;var dev=world.Physical.Devices[id];
  Button(new(x,y,230,26),Fit(e.Name+" #"+id,217,.6f),()=>engineeringDevice=devices[(Array.IndexOf(devices,id)+1)%devices.Length],scale:.6f);
  Button(new(x+240,y,85,26),"Locate",()=>Focus(id),scale:.62f);Button(new(x+335,y,110,26),dev.Enabled?"Disable":"Enable",()=>Execute(new(CommandKind.PhysicalAction,id,Text:"toggle")),scale:.62f);Button(new(x+455,y,110,26),"Gate/door",()=>Execute(new(CommandKind.PhysicalAction,id,Text:"gate")),scale:.6f);Button(new(x+575,y,90,26),"Repair",()=>Execute(new(CommandKind.PhysicalAction,id,Text:"repair")),scale:.62f);
  Label(Fit($"{dev.Cause} | fuel {dev.Fuel} | charge {dev.Charge:0} | condition {dev.Condition:0}%",p.Width-38,.62f),x,y+35,dev.Fault?Red:Muted,.62f);
 }
 private void DrawPhysicalNetworkEdges()
 {if(!world.Physical.Enabled||physicalOverlay!="Networks")return;foreach(var network in world.Physical.Networks)for(int a=0;a<network.Members.Count;a++)for(int b=a+1;b<network.Members.Count;b++){var first=world.Find(network.Members[a]);var second=world.Find(network.Members[b]);if(first==null||second==null||first.Z!=level||second.Z!=level)continue;if(world.NetworkAdjacent(first,second))Stroke(new(first.X*32+16,first.Y*32+16),new(second.X*32+16,second.Y*32+16),network.Kind=="Electrical"?Gold:network.Kind=="Mechanical"?Muted:new Color(170,138,206),2);}}
 private void DrawPhysicalOverlay(int x,int y)
 {
  if(!world.Physical.Enabled)return;int cell=world.Index(x,y,level);
  if(physicalOverlay=="Water"&&world.WaterDepth(cell)>0){Fill(new(x*32+1,y*32+2,30,28),new Color(65,136,190,110));Label(world.WaterDepth(cell).ToString(),x*32+12,y*32+9,world.WaterConcentration(cell)>.2?Red:Text,.5f);}
  if(physicalOverlay=="Heat"&&world.Physical.Regions.ContainsKey(cell)){double t=world.TemperatureAt(cell);if(t<8||t>38)Fill(new(x*32,y*32,32,32),t<8?new Color(70,120,210,70):new Color(220,90,55,70));}
  if(world.Physical.Fires.ContainsKey(cell)){Fill(new(x*32+9,y*32+5,13,24),Gold);Fill(new(x*32+13,y*32+8,7,22),Red);}
  if(physicalOverlay=="Networks")foreach(var e in world.Entities.Where(e=>e.X==x&&e.Y==y&&e.Z==level&&world.Physical.Devices.ContainsKey(e.Id))){var s=world.Physical.Devices[e.Id];Outline(new(x*32+2,y*32+2,28,28),s.Fault?Red:s.Supplied?Cyan:Gold,2);}
 }
}
