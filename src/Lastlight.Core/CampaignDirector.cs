namespace Lastlight.Core;
public sealed partial class World
{
 public bool CampaignPeace(long faction)=>Region.Treaties.Any(t=>t.Active&&t.FactionId==faction&&t.Kind is "Safe passage" or "Resource exchange" or "Scholarly access" or "Mutual defense" or "Sanctuary" or "Artifact restitution");
 private void CampaignIncident(string kind,string cause,string result,long faction=0)
 {Solar.History.Add(new(){Id=Solar.NextId++,Tick=Tick,Kind=kind,Cause=cause,Result=result,FactionId=faction});if(Solar.History.Count>64)Solar.History.RemoveAt(0);Solar.Incidents++;Chronicle("Director",kind+": "+cause+" -> "+result,faction,Region.HomeSiteId);}
 public string CampaignRecoveryReason()
 {
  if(Solar.Evacuees.Count>0)return "Residents evacuated; no new colony incidents";
  if(Danger.Raid.Phase!="Idle")return "Existing raid/recovery remains active";
  if(Tick<Solar.FlareUntil||Solar.FlareAnnounced&&Solar.NextFlareAt-Tick<TicksPerDay)return "Forecast solar hazard; no raid stacking";
  if(Colony.People.Keys.Any(id=>Body(id) is {Dead:false}&&(NeedsTreatment(id)||Body(id) is {Downed:true})))return "Injured/downed residents need treatment";
  if(Danger.History.Any(e=>e.Kind=="Death"&&Tick-e.Tick<TicksPerDay*3))return "Recent combat loss: three-day recovery window";
  if(Resources("meal").Total+Resources("grain").Total<Colony.People.Keys.Count(id=>Body(id) is {Dead:false})*2)return "Food security is below two rations per resident";
  return "";
 }
 private void AdvanceCampaignDirector(Definitions d)
 {
  if(Tick<Solar.NextIncidentAt)return;string recovery=CampaignRecoveryReason();if(recovery!=""){Solar.DirectorStatus=recovery;Solar.NextIncidentAt=Tick+TicksPerDay;return;}
  var dispute=Solar.Claims.Where(c=>c.State=="Disputed").Select(c=>Region.Factions.First(f=>f.Id==c.FactionId)).Where(f=>f.Grievance>=20&&!CampaignPeace(f.Id)&&f.Strength>=2&&f.Reserves.GetValueOrDefault("wood")>=4).OrderBy(f=>f.Id).FirstOrDefault();
  bool pressure=Solar.Director=="The Capricious Star"||Solar.Director=="The Chronicler"&&Solar.Incidents%2==0;
  if(pressure&&dispute!=null&&Danger.RaidsEnabled&&Solar.RaidIntensity>0)
  {
   int equipped=Colony.People.Keys.Count(id=>CanAct(id)&&Body(id)!.WeaponId!="unarmed");int defenses=Entities.Count(e=>e.DefinitionId is "barricade" or "trap" or "f-ward");int count=Math.Clamp(1+equipped/3+defenses/8,1,4);count=Math.Min(count,1+Solar.RaidIntensity/34);count=Math.Min(count,Math.Min(dispute.Strength,dispute.Reserves["wood"]/2));
   var result=BeginRaid(count,d);if(result.Accepted){dispute.Strength-=count;dispute.Reserves["wood"]-=count*2;CampaignIncident("Sanctuary conflict",dispute.Name+" has an unresolved claim, grievance and a funded force budget",count+" attackers warned; " + count*2+" wood and "+count+" strength withdrawn",dispute.Id);Solar.DirectorStatus="Funded claim conflict warned";}else Solar.DirectorStatus=result.Message;
  }
  else
  {
   var site=Region.Sites.Where(s=>!s.Discovered).OrderBy(s=>s.Id).FirstOrDefault();if(site!=null){site.Discovered=true;CampaignIncident("Opportunity","A regional archive testimony revealed an existing historical site",site.Name+" charted; no artifact or supplies spawned",site.FactionId);Solar.DirectorStatus="A real regional opportunity charted";}else{Solar.DirectorStatus=dispute==null?"No eligible hostile motive; recovery/social play continues":"Claim cannot override signed diplomacy, configured raids or current safety";CampaignIncident("Recovery","No eligible funded pressure",Solar.DirectorStatus);}
  }
  var random=new StableRandom(Solar.RandomState);int days=Solar.Director=="The Quiet Archivist"?7:Solar.Director=="The Capricious Star"?2+random.Next(5):4;Solar.RandomState=random.State;Solar.NextIncidentAt=Tick+TicksPerDay*days;
 }
}
