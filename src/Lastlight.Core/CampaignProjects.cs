namespace Lastlight.Core;
public sealed partial class World
{
 public Dictionary<string,int> ProjectIngredients(string id,int phase)=>id switch
 {
  "Sun"=>phase switch{0=>new(){{"f-glass",2},{"brass",4}},1=>new(){{"f-parts",8},{"brass",8}},_=>new(){{"f-parts",8},{"f-glass",4},{"brass",8}}},
  "Vessel"=>phase switch{0=>new(){{"f-glass",2},{"brass",4}},1=>new(){{"f-parts",10},{"f-cloth",6}},_=>new(){{"meal",12},{"medicine",4},{"brass",8}}},
  "Ark"=>phase switch{0=>new(){{"wood",20},{"blocks",20}},1=>new(){{"f-parts",8},{"f-glass",3}},_=>new(){{"meal",12},{"medicine",4},{"f-cloth",4}}},
  "Federation"=>phase switch{0=>new(){{"wood",12},{"brass",4}},1=>new(){{"f-parts",6},{"f-glass",2}},_=>new(){{"meal",10},{"medicine",3}}},
  _=>phase switch{0=>new(){{"wood",20},{"f-glass",2}},1=>new(){{"f-parts",6},{"brass",4}},_=>new(){{"f-glass",3},{"medicine",2}}}
 };
 public List<string> ProjectMissing(CampaignProject project)
 {
  var missing=new List<string>();void Need(bool v,string text){if(!v)missing.Add(text);}var host=Find(project.HostId);int people=Colony.People.Keys.Count(id=>Body(id) is {Dead:false}&&!Solar.Evacuees.Contains(id));
  Need(host!=null&&!host.Forbidden,"An allowed project host");if(host==null)return missing;
  Need(Solar.Finale==""||Solar.Finale==project.Id,"One selected campaign finale");
  bool library=Society.Institutions.Any(i=>i.Kind=="Library"&&i.Sessions>=4&&Find(i.HostId)!=null&&CanAct(i.KeeperId));
  switch(project.Id)
  {
   case "Sun":Need(host.Id==Slice.Ruin.EngineId&&Slice.Ruin.EngineRestored,"Restored observatory engine");Need(Solar.Stage>=3,"Scattered Refuges reached");Need(library,"Staffed library with four sessions");Need(Physical.Crafts.Any(c=>c.Recipe=="f-make-parts"),"Parts made through the crafting chain");break;
   case "Vessel":Need(host.DefinitionId=="workbench","A workbench for the vessel assembly");Need(Solar.Stage>=2,"Red Convulsions reached");Need(library,"Staffed library with four sessions");Need(Region.Sites.Any(s=>s.Kind=="Machine monastery"&&s.Instantiated),"Surveyed machine monastery");Need(Physical.Crafts.Any(c=>c.Recipe=="f-make-cloth"),"Cloth made through husbandry/crafting");break;
   case "Ark":Need(host.DefinitionId=="study"&&host.Z is 1 or 2,"An underground study desk");Need(Solar.Stage>=2,"Red Convulsions reached");Need(RefugeCapacity()>=people,"A dry, warm bed for every living resident");Need(Entities.Any(e=>e.DefinitionId=="f-heater"&&e.Z==host.Z&&DevicePowered(e.Id)),"A supplied underground heater");Need(Autonomy.Crops.Values.Any(c=>c.OutputId=="fungus"&&Position(c.Cell).Z==host.Z&&CropFailure(c)==""&&c.GrowthTicks>=TicksPerDay),"An established viable underground fungal crop");Need(Slice.Formulae.Values.Any(f=>f.Known.Contains("ember")),"Learned Ember countermeasure");break;
   case "Federation":Need(host.DefinitionId=="study","A study desk for refuge councils");Need(Solar.Stage>=1,"Long Twilight reached");Need(library,"Staffed library with four sessions");var allies=FederationAllies();Need(allies.Count>=3,"Three trusted Sanctuary allies");if(project.Phase==2)Need(allies.Take(3).All(f=>f.Reserves.GetValueOrDefault("grain")>=12&&f.Reserves.GetValueOrDefault("f-parts")>=2),"Each ally funds twelve grain and two parts");break;
   case "Archive":Need(host.DefinitionId=="study"&&Society.Institutions.Any(i=>i.Kind=="Library"&&i.HostId==host.Id&&i.Sessions>=8),"A library host with eight real sessions");Need(Society.Apprentices.Any(a=>a.Graduated),"A graduated apprentice");Need(Slice.Formulae.Values.SelectMany(f=>f.Known).Distinct().Count()>=3,"Three learned formulae to preserve");Need(Region.Sites.Any(s=>s.Kind!="Colony"&&s.Instantiated),"An actual regional field survey");Need(RefugeCapacity()>=people,"Dry, warm beds for continuing refuge play");break;
  }
  if(Solar.FlareUntil>Tick)Need(Slice.Roofs.Contains(Index(host.X,host.Y,host.Z)),"Roof the project during the forecast flare");return missing;
 }
 private List<RegionFaction> FederationAllies()=>Region.Factions.Where(f=>f.Trust>=10&&HasTreaty(f.Id,"Sanctuary")).OrderBy(f=>f.Id).ToList();
 private ColonyJob? PickCampaignJob(Entity pawn,PawnProfile p,Definitions d)
 {
  if(!Solar.Enabled)return null;var project=Solar.Projects.FirstOrDefault(p=>p.Active&&!p.Completed&&p.WorkerId==pawn.Id);if(project==null)return null;var missing=ProjectMissing(project);if(missing.Count>0){project.Status=string.Join("; ",missing);return null;}var host=Find(project.HostId)!;int cell=Index(host.X,host.Y,host.Z);if(Autonomy.Claims.ContainsKey("campaign:"+project.Id)||FindPath(pawn,p,cell,true,d)==null)return null;
  var job=new ColonyJob{PawnId=pawn.Id,Kind="Campaign",TargetId=host.Id,DefinitionId=project.Id,Cell=cell,Claims=["campaign:"+project.Id]};if(!ReserveIngredients(job,ProjectIngredients(project.Id,project.Phase),d)){project.Status="Reachable unclaimed materials: "+string.Join(", ",ProjectIngredients(project.Id,project.Phase).Select(k=>k.Value+" "+k.Key));return null;}return job;
 }
 private bool ValidCampaignJob(ColonyJob j)=>Solar.Enabled&&Solar.Projects.Any(p=>p.Id==j.DefinitionId&&p.Active&&!p.Completed&&p.HostId==j.TargetId&&p.WorkerId==j.PawnId)&&Find(j.TargetId) is {Forbidden:false};
 private bool HandleCampaignJob(ColonyJob j,Entity pawn,Definitions d)
 {
  if(j.Kind!="Campaign")return false;var project=Solar.Projects.First(p=>p.Id==j.DefinitionId);var missing=ProjectMissing(project);if(missing.Count>0){project.Status=string.Join("; ",missing);return true;}
  project.Status="Working on phase "+(project.Phase+1)+"/3";j.WorkCredit+=Capacity(pawn.Id).Manipulation;if(j.WorkCredit<1)return true;j.WorkCredit-=1;project.Work=Math.Min(90,project.Work+1);if(project.Work<90)return true;
  var required=ProjectIngredients(project.Id,project.Phase);if(required.Any(k=>j.Cargo.GetValueOrDefault(k.Key)<k.Value)){project.Status="Material delivery incomplete";return true;}
  foreach(var item in required){j.Cargo[item.Key]-=item.Value;if(j.Cargo[item.Key]==0)j.Cargo.Remove(item.Key);project.Spent[item.Key]=project.Spent.GetValueOrDefault(item.Key)+item.Value;}
  if(project.Id=="Federation"&&project.Phase==2)foreach(var ally in FederationAllies().Take(3)){ally.Reserves["grain"]-=12;ally.Reserves["f-parts"]-=2;var refuge=Solar.Refuges.First(r=>r.FactionId==ally.Id);refuge.Level=3;refuge.PartsSpent+=2;Chronicle("Federation",ally.Name+" funded its pledged common refuge stores.",ally.Id);}
  project.Work=0;project.Phase++;Chronicle("CampaignProject",project.Id+" phase "+project.Phase+" completed using delivered physical materials.",actor:pawn.Id,site:Region.HomeSiteId);Interrupt(pawn.Id,d);
  if(project.Phase<3)return true;project.Active=false;project.Completed=true;project.Status="Completed";Solar.Finale=project.Id;Solar.CompletedAt=Tick;foreach(var other in Solar.Projects.Where(p=>p.Active)){other.Active=false;other.Status="Another finale completed";if(other.WorkerId!=0)Interrupt(other.WorkerId,d);}
  if(project.Id=="Sun"){Solar.SunRestored=true;Solar.NextFlareAt=0;Solar.FlareUntil=0;foreach(var forecast in Solar.Forecasts)forecast.Resolved=true;UpdateSolarStage();}
  if(project.Id=="Vessel"){foreach(long id in Colony.People.Keys.Where(id=>Body(id) is {Dead:false})){Interrupt(id,d);Danger.Orders.Remove(id);Solar.Evacuees.Add(id);Colony.People[id].Drafted=false;Colony.People[id].Activity="Aboard evacuation vessel";}}
  if(project.Id=="Archive")Solar.PreservedKnowledge=Slice.Formulae.Values.SelectMany(f=>f.Known).Concat(Physical.Crafts.Select(c=>c.Recipe)).Distinct().Order().ToList();
  foreach(long id in Colony.People.Keys.Where(id=>Body(id) is {Dead:false}))Remember(id,"Finale","We completed the "+FinaleNames[Array.IndexOf(FinaleIds,project.Id)]+".",10,0,30);
  Chronicle("Finale",FinaleNames[Array.IndexOf(FinaleIds,project.Id)]+" completed. "+(project.Id=="Vessel"?Solar.Evacuees.Count+" living residents boarded with their persistent bodies and equipment.":project.Id=="Sun"?"Regional luminosity and climate restored; normal hazards remain.":"The settlement may continue: maintain real food, warmth, power and diplomacy."),actor:pawn.Id,site:Region.HomeSiteId);Danger.PauseSerial++;return true;
 }
 public void RemoveCampaignEntity(long id){foreach(var project in Solar.Projects.Where(p=>p.HostId==id||p.WorkerId==id)){project.HostId=0;project.WorkerId=0;project.Active=false;project.Status="Host or worker removed; recommission to continue";}Solar.Evacuees.Remove(id);}
}
