namespace Lastlight.Core;
public sealed partial class World
{
 public void ValidatePhysical(Definitions d)
 {
  var p=Physical;bool Cell(int i)=>i>=0&&i<Cells.Count;bool F(double v,double min,double max)=>double.IsFinite(v)&&v>=min&&v<=max;
  if(p==null||!p.Initialized||p.EcologyRandom==0||p.Water==null||p.Fires==null||p.Regions==null||p.Rooms==null||p.Devices==null||p.Networks==null||p.Animals==null||p.PastureUse==null||p.Crafts==null||p.Notices==null||p.Water.Count>Cells.Count||p.Fires.Count>4096||p.Notices.Count>128||p.Crafts.Count>128||!F(p.Population,0,20)||!F(p.Weather,-40,60)||p.LastDay<0||p.LastDay>Tick/TicksPerDay||p.HarvestFailures<0||p.Births<0||p.Products<0||p.Extinguished<0||p.Repairs<0)throw new InvalidDataException("Invalid physical simulation state.");
  if(!F(p.InjectedWater,0,1e9)||!F(p.InjectedContaminant,0,1e9)||!F(p.Evaporated,0,1e9)||!F(p.EvaporatedContaminant,0,1e9)||!F(p.FilteredContaminant,0,1e9))throw new InvalidDataException("Invalid fluid ledger.");
  foreach(var pair in p.Water)if(!Cell(pair.Key)||pair.Value==null||pair.Value.Depth is <1 or >7||!F(pair.Value.Contaminant,0,pair.Value.Depth+1e-7))throw new InvalidDataException("Invalid water depth or mass.");
  if(p.Enabled&&(Math.Abs(p.Water.Values.Sum(f=>f.Depth)+p.Evaporated-p.InjectedWater)>1e-6||Math.Abs(p.Water.Values.Sum(f=>f.Contaminant)+p.FilteredContaminant+p.EvaporatedContaminant-p.InjectedContaminant)>1e-6))throw new InvalidDataException("Water/contaminant mass conservation failed.");
  foreach(var pair in p.Fires)if(!Cell(pair.Key)||pair.Value==null||!F(pair.Value.Intensity,.00001,5)||pair.Value.Work is <0 or >90)throw new InvalidDataException("Invalid fire.");
  foreach(var pair in p.Rooms)if(!Cell(pair.Key)||pair.Value==null||pair.Value.Id!=pair.Key||pair.Value.Level<0||pair.Value.Level>=Levels||pair.Value.Cells<1||!F(pair.Value.Temperature,-100,1000)||!F(pair.Value.Smoke,0,20)||!F(pair.Value.Insulation,0,1))throw new InvalidDataException("Invalid room climate.");
  if(p.Regions.Any(pair=>!Cell(pair.Key)||!p.Rooms.ContainsKey(pair.Value)))throw new InvalidDataException("Invalid climate region.");
  foreach(var pair in p.Devices)if(pair.Value==null||pair.Value.Id!=pair.Key||Find(pair.Key) is not {} e||!PhysicalDevices.Contains(e.DefinitionId)||!F(pair.Value.Charge,0,900)||!F(pair.Value.Condition,0,100)||pair.Value.Fuel is <0 or >1000||pair.Value.Network<0||pair.Value.Cause==null)throw new InvalidDataException("Invalid engineering device.");
  foreach(var net in p.Networks)if(net==null||net.Id<1||net.Kind is not ("Electrical" or "Mechanical" or "Thaumic")||net.Members==null||net.Members.Distinct().Count()!=net.Members.Count||net.Members.Any(id=>!p.Devices.ContainsKey(id))||!F(net.Supply,0,1e6)||!F(net.Demand,0,1e6)||!F(net.Stored,0,1e9))throw new InvalidDataException("Invalid network graph.");
  foreach(var pair in p.Animals)if(pair.Value==null||Find(pair.Key) is not {DefinitionId:"grazer"}||pair.Value.Id!=pair.Key||!F(pair.Value.Nutrition,0,100)||!F(pair.Value.Disease,0,100)||pair.Value.Training is <0 or >3||pair.Value.Taming is <0 or >100||pair.Value.NextProduct<0||pair.Value.NextBirth<0||pair.Value.Request is not ("" or "tame" or "train" or "feed" or "collect"))throw new InvalidDataException("Invalid husbandry state.");
  if(p.PastureUse.Any(pair=>!Cell(pair.Key)||!F(pair.Value,0,6))||p.Notices.Any(n=>n==null||n.Tick<0||n.Tick>Tick||!Cell(n.Cell)||n.Text==null||n.Text.Length>512)||p.Crafts.Any(c=>c==null||c.Tick<0||c.Tick>Tick||c.Maker<1||c.Maker>=NextEntityId||!d.Document.Recipes.Any(r=>r.Id==c.Recipe)||!d.Document.Materials.Any(m=>m.Id==c.Material)||c.Quality is <1 or >100))throw new InvalidDataException("Invalid ecology/history record.");
 }
}
