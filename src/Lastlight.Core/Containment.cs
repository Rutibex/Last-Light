namespace Lastlight.Core;
public sealed partial class World
{
 private CommandResult ApplyContainmentCommand(WorldCommand c,Definitions d)
 {
  if(c.Text=="contain")
  {
   if(Society.Containments.Count>=16||!CanAct(c.EntityId)||!Colony.People.ContainsKey(c.EntityId)||Find(c.TargetId) is not {} target||target.DefinitionId!="vault-guardian"||Body(target.Id) is not {Dead:false,Downed:true}||Find(c.EntityId) is not {} keeper||keeper.Z!=target.Z||Distance(keeper,target)>2||Society.Containments.Any(q=>q.EntityId==target.Id))return new(false,"A capable keeper must stand beside a living downed vault guardian; recapture is not supported.");
   Interrupt(target.Id,d);Danger.Orders.Remove(target.Id);Body(target.Id)!.Captured=true;Slice.Hostiles.Remove(target.Id);Society.Containments.Add(new(){EntityId=target.Id,KeeperId=c.EntityId,Cell=Index(target.X,target.Y,target.Z),LastFeedAt=Tick});Chronicle("Containment",target.Name+" entered containment; satisfy requirements before escape progress reaches 100.",actor:c.EntityId,site:Region.HomeSiteId);Danger.PauseSerial++;return new(true,"Containment established; inspect unmet requirements immediately.");
  }
  var q=Society.Containments.FirstOrDefault(q=>q.EntityId==c.TargetId&&!q.Escaped);if(q==null)return new(false,"Select an active containment.");var e=Find(q.EntityId);var k=Find(c.EntityId);if(e==null||k==null||!CanAct(k.Id)||!Colony.People.ContainsKey(k.Id)||k.Z!=e.Z||Distance(k,e)>2)return new(false,"A capable resident must stand beside containment.");
  if(c.Text=="feed")
  {var feed=Entities.Where(s=>s.DefinitionId=="grain"&&!s.Forbidden&&!Autonomy.Claims.ContainsKey("item:"+s.Id)&&s.Z==k.Z&&Distance(s,k)<=2).OrderBy(s=>s.Id).FirstOrDefault();if(feed==null||q.Feed>=6)return new(false,"Need unclaimed grain beside keeper; six rations maximum.");feed.Quantity--;if(feed.Quantity==0)Entities.Remove(feed);q.Feed++;return new(true,"One real grain ration delivered.");}
  if(c.Text=="study"){if(c.Quantity is <0 or >3)return new(false,"Study rate must be 0–3.");q.KeeperId=c.EntityId;q.StudyRate=c.Quantity;return new(true,"Containment study rate set; higher yield consumes stability.");}
  return new(false,"Unknown institution/containment action.");
 }
 public Dictionary<string,bool> ContainmentRequirements(Containment q)
 {
  var p=Position(q.Cell);var e=Find(q.EntityId);return new(){{"Light >=60",LightAt(q.Cell)>=60},{"Temperature 10–30 C",TemperatureAt(q.Cell) is >=10 and <=30},{"Feed ration",q.Feed>0},{"Roof shielding",Slice.Roofs.Contains(q.Cell)},{"Powered resonant grounding",e!=null&&Entities.Any(w=>w.DefinitionId=="f-ward"&&w.Z==p.Z&&Distance(w,e)<=3&&DevicePowered(w.Id))}};
 }
 private void AdvanceContainment(Definitions d)
 {
  foreach(var q in Society.Containments.Where(q=>!q.Escaped).ToArray())
  {
   var e=Find(q.EntityId);if(e==null||Body(q.EntityId) is {Dead:true}){q.Escaped=true;q.Warning="Specimen lost or dead; no study yield";continue;}
   if(Tick-q.LastFeedAt>=TicksPerHour*6){q.Feed=Math.Max(0,q.Feed-1);q.LastFeedAt=Tick;}
   int unmet=ContainmentRequirements(q).Count(r=>!r.Value);q.Warning=unmet>0?unmet+" unmet requirements":q.Stability<40?"Overstudy: stability below 40":"Stable";
   if(Tick%TicksPerHour!=0)continue;
   bool attended=Find(q.KeeperId) is {} keeper&&CanAct(keeper.Id)&&!Colony.People[keeper.Id].Drafted&&keeper.Z==e.Z&&Distance(keeper,e)<=2;
   if(unmet==0&&attended&&q.StudyRate>0){q.Knowledge=Math.Min(10000,q.Knowledge+q.StudyRate);q.Stability=Math.Max(0,q.Stability-q.StudyRate*7);if(q.Knowledge>=8&&!Slice.Formulae[q.KeeperId].Known.Contains("door")){Slice.Formulae[q.KeeperId].Known.Add("door");Chronicle("Discovery","Containment study recovered the Patient Door countermeasure.",actor:q.KeeperId,site:Region.HomeSiteId);}}
   else if(unmet==0&&q.StudyRate==0)q.Stability=Math.Min(100,q.Stability+8);
   int pressure=unmet*8+(q.Stability<40?(40-q.Stability)/2:0);q.Escape=Math.Clamp(q.Escape+pressure-(unmet==0&&q.Stability>=40?12:Math.Min(3,q.Knowledge/8)),0,100);
   if(q.Escape>=60){q.Warning="Escape imminent: "+q.Escape+"/100; stop study and repair requirements";Danger.PauseSerial++;}
   if(q.Escape>=100){q.Escaped=true;Body(e.Id)!.Captured=false;Slice.Hostiles.Add(e.Id);Chronicle("ContainmentFailure",e.Name+" escaped after unmet requirements or excessive study exhausted stability.",actor:q.KeeperId,site:Region.HomeSiteId);Danger.PauseSerial++;}
  }
 }
}
