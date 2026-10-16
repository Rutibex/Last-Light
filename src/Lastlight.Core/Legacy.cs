using System.Text.Json;
namespace Lastlight.Core;
public sealed partial class World
{
 private static T CopyLegacy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,JsonFormat.Options),JsonFormat.Options)!;
 public CommandResult CanRetire()=>Solar.Evacuees.Count>0?new(false,"Evacuated residents cannot found a successor from this vessel."):!Society.Enabled||Society.Legacy!=null?new(false,"This vertical slice supports one retirement per campaign."):Region.Parties.Any(p=>p.State is not ("Returned" or "Cancelled"))?new(false,"Return or cancel all expeditions before retirement."):Danger.Raid.Phase is "Warning" or "Active"?new(false,"Resolve the raid before retirement."):Slice.Casts.Count>0?new(false,"Finish active formula channels before retirement."):new(true,"Ready for a successor settlement.");
 public World BeginSuccessor(Definitions d,int elapsedYears=5)
 {
  var ready=CanRetire();if(!ready.Accepted)throw new InvalidOperationException(ready.Message);if(elapsedYears is <1 or >20)throw new ArgumentOutOfRangeException(nameof(elapsedYears));
  var old=CopyLegacy(this);foreach(long id in old.Colony.People.Keys.ToArray())old.Interrupt(id,d);old.PendingCommands.Clear();old.Validate(d);
  var next=InstitutionsWorldGenerator.Create(d,Seed+1);next.Tick=Tick+elapsedYears*30L*TicksPerDay;next.Region=CopyLegacy(Region);var retired=next.Region.Sites.First(s=>s.Id==Region.HomeSiteId);retired.Kind="Retired settlement";retired.Name="Old "+retired.Name;retired.HistoryTag="Retired under "+Society.Covenant;retired.Slot=13;
  foreach(var site in next.Region.Sites){site.Instantiated=false;site.AnchorCell=-1;site.MarketId=0;site.GuardianId=0;site.EntityIds.Clear();}
  foreach(var a in next.Region.Artifacts){if(a.Custody=="Colony"){a.Custody="Site";a.SiteId=retired.Id;}a.LocalEntityId=0;a.PartyId=0;}
  foreach(var trader in next.Region.Traders.Where(t=>t.State!="Returned")){var f=next.Region.Factions.First(f=>f.Id==trader.FactionId);foreach(var item in trader.Cargo)f.Reserves[item.Key]=Math.Min(10000,f.Reserves.GetValueOrDefault(item.Key)+item.Value);}
  next.Region.Parties.Clear();next.Region.Absent.Clear();next.Region.Traders.Clear();next.Region.RecordedDeaths.Clear();foreach(var entry in next.Region.History)entry.LocalActorId=0;foreach(var quest in next.Region.Quests.Where(q=>q.Status=="Accepted"))quest.Status="Failed";
  var home=new RegionSite{Id=next.Region.NextId++,Name="Successor Refuge",Kind="Colony",X=5,Y=6,Slot=0,Discovered=true,HistoryTag="Successor expedition founded after "+elapsedYears+" years"};next.Region.Sites.Add(home);next.Region.HomeSiteId=home.Id;
  if(next.Find(next.Slice.Ruin.EngineId) is {} engine)next.Region.Artifacts.Add(new(){Id=next.Region.NextId++,Name="Successor daylight engine",DefinitionId="relic",SiteId=home.Id,Custody="Colony",LocalEntityId=engine.Id,Anchored=true});
  next.Region.LastDay=next.Tick/TicksPerDay;next.Region.ObservedDangerTick=next.Tick;next.Region.ObservedCraftTick=next.Tick;next.Physical.LastDay=next.Tick/TicksPerDay;next.Danger.NextRaidAt=next.Tick+TicksPerDay*3;
  next.Society.LastRitualDay=next.Tick/TicksPerDay;next.Society.Legacy=new(){SiteId=retired.Id,RetiredAt=Tick,ElapsedYears=elapsedYears,Snapshot=JsonSerializer.Serialize(old,JsonFormat.Options),Conversion=LegacyConversion(elapsedYears)};
  CarrySolarIntoSuccessor(next);next.Chronicle("Retirement",retired.Name+" became a historical site. "+next.Society.Legacy.Conversion,site:retired.Id);next.Validate(d);return next;
 }
 private static string LegacyConversion(int years)=>years+" years elapsed: structures lose "+Math.Min(80,years*3)+"% condition, ordinary stacks retain "+Math.Max(0,100-years*10)+"%, unique artifacts retain identity; residents age, those aged 80+ die. Cisterns drain, fires die and stored fuel/charge expire. Prior deaths, wounds, families and claims persist. Visit or reclaim once.";
 private World ConvertedLegacy(Definitions d)
 {
  var archive=Society.Legacy??throw new InvalidOperationException("No retired settlement.");var old=JsonSerializer.Deserialize<World>(archive.Snapshot,JsonFormat.Options)??throw new InvalidDataException("Invalid legacy snapshot.");old.EnsureSolar();int years=archive.ElapsedYears;old.Tick=Tick;old.PendingCommands.Clear();
  var protectedIds=old.Region.Artifacts.Where(a=>a.Custody=="Colony").Select(a=>a.LocalEntityId).ToHashSet();
  foreach(var e in old.Entities.ToArray())
  {
   var kind=d.Entities[e.DefinitionId].Kind;if(kind==EntityKind.Building)e.Health=Math.Max(1,e.Health*(100-Math.Min(80,years*3))/100);
   if(kind==EntityKind.ItemStack&&!protectedIds.Contains(e.Id)){e.Quantity=e.Quantity*Math.Max(0,100-years*10)/100;if(e.Quantity==0)old.Apply(new(CommandKind.Remove,e.Id),d);}
   if(old.Colony.People.TryGetValue(e.Id,out var p)){p.Age=Math.Min(200,p.Age+years);p.Drafted=false;p.AllowedZoneId=0;if(p.Age>=80&&old.Body(e.Id) is {} body&&!body.Dead){body.Dead=true;body.Downed=true;body.DeathCause="Age during retirement";e.Health=0;}p.Food=700;p.Rest=700;}
  }
  old.Physical.Evaporated+=old.Physical.Water.Values.Sum(f=>f.Depth);old.Physical.EvaporatedContaminant+=old.Physical.Water.Values.Sum(f=>f.Contaminant);old.Physical.Water.Clear();old.Physical.Fires.Clear();foreach(var device in old.Physical.Devices.Values){device.Fuel=0;device.Charge=0;device.Condition*=1-Math.Min(.8,years*.03);device.Supplied=false;device.Cause="Retired infrastructure needs inspection";}old.Physical.Networks.Clear();old.RefreshEngineering(d);
  old.Physical.LastDay=Tick/TicksPerDay;old.Region.LastDay=Tick/TicksPerDay;old.Society.LastRitualDay=Tick/TicksPerDay;old.Danger.NextRaidAt=Tick+TicksPerDay*3;return old;
 }
 public World ReclaimLegacy(Definitions d)
 {
  var archive=Society.Legacy??throw new InvalidOperationException("No retired settlement.");if(archive.Visited||archive.Reclaimed)throw new InvalidOperationException("This site has already been visited or reclaimed; recover it through the expedition instead.");if(Region.Parties.Count>0||Region.Sites.Any(s=>s.Instantiated))throw new InvalidOperationException("Reclaim before sending successor expeditions; otherwise visit and recover through World.");
  var old=ConvertedLegacy(d);var previousArtifacts=old.Region.Artifacts;var previousSites=old.Region.Sites;old.Region=CopyLegacy(Region);long successor=old.Region.HomeSiteId;old.Region.HomeSiteId=archive.SiteId;old.Region.Sites.First(s=>s.Id==successor).Kind="Retired settlement";old.Region.Sites.First(s=>s.Id==archive.SiteId).Kind="Colony";
  foreach(var site in old.Region.Sites){var prior=previousSites.FirstOrDefault(p=>p.Id==site.Id);if(prior!=null){site.Instantiated=prior.Instantiated;site.AnchorCell=prior.AnchorCell;site.MarketId=prior.MarketId;site.GuardianId=prior.GuardianId;site.EntityIds=CopyLegacy(prior.EntityIds);}}
  foreach(var a in old.Region.Artifacts){var prior=previousArtifacts.FirstOrDefault(p=>p.Id==a.Id);if(prior!=null){a.Custody=prior.Custody;a.LocalEntityId=prior.LocalEntityId;a.PartyId=prior.PartyId;a.SiteId=prior.SiteId;}else if(a.Custody=="Colony"){a.Custody="Lost";a.LocalEntityId=0;}}
  old.Solar=CopyLegacy(Solar);foreach(var project in old.Solar.Projects){project.HostId=0;project.WorkerId=0;if(!project.Completed){project.Phase=0;project.Work=0;project.Active=false;}}old.Society.Legacy=CopyLegacy(archive);old.Society.Legacy.Reclaimed=true;old.Society.Legacy.Snapshot="";old.Chronicle("Reclaim","The earlier settlement was reclaimed. "+archive.Conversion,site:archive.SiteId);old.Validate(d);return old;
 }
 private void InstantiateLegacySite(RegionSite site,Definitions d)
 {
  var archive=Society.Legacy??throw new InvalidDataException("Missing retirement archive.");if(archive.SiteId!=site.Id||archive.Reclaimed)throw new InvalidDataException("Retired site is unavailable.");var old=ConvertedLegacy(d);while(Levels<7){for(int i=0;i<Width*Height;i++)Cells.Add(new(){TerrainId="stone"});Levels++;}
  int Offset(int cell)=>cell+4*Width*Height;for(int i=0;i<Width*Height*3;i++)Cells[Offset(i)]=CopyLegacy(old.Cells[i]);
  var map=archive.EntityMap;foreach(var e in old.Entities.Where(e=>e.Z<3)){var clone=CopyLegacy(e);clone.Id=NextEntityId++;clone.Z+=4;clone.MakerId=0;Entities.Add(clone);map[e.Id]=clone.Id;site.EntityIds.Add(clone.Id);}
  Colony.EnsureEntities(this,d);EnsureDanger(d);EnsureSlice(d);EnsurePhysical(d);
  var policies=new Dictionary<string,string>();foreach(var original in old.Colony.Policies){var clone=CopyLegacy(original);clone.Id="legacy-"+original.Id;policies[original.Id]=clone.Id;if(!Colony.Policies.Any(q=>q.Id==clone.Id))Colony.Policies.Add(clone);}
  foreach(var pair in map)
  {
   if(old.Body(pair.Key) is {} body){var b=CopyLegacy(body);b.EntityId=pair.Value;b.CarriedBy=0;b.NextMoveAt=Tick;b.NextAttackAt=Tick;foreach(var w in b.Wounds){w.Id=Danger.NextWoundId++;w.SourceId=map.GetValueOrDefault(w.SourceId);w.DoctorId=map.GetValueOrDefault(w.DoctorId);}Danger.Bodies[pair.Value]=b;}
   if(old.Colony.People.TryGetValue(pair.Key,out var p)){var profile=CopyLegacy(p);profile.EntityId=pair.Value;profile.AllowedZoneId=0;profile.Drafted=false;profile.Policies=profile.Policies.ToDictionary(k=>k.Key,k=>policies[k.Value]);Colony.People[pair.Value]=profile;Remember(pair.Value,"Legacy","I survived the retirement of our settlement.",5,0,30);}
  }
  foreach(var pair in map){var original=old.Find(pair.Key)!;Find(pair.Value)!.MakerId=map.GetValueOrDefault(original.MakerId);if(old.Danger.People.TryGetValue(pair.Key,out var social)){var clone=CopyLegacy(social);clone.EntityId=pair.Value;foreach(var memory in clone.Memories){memory.OtherId=map.GetValueOrDefault(memory.OtherId);memory.Cell=memory.Cell<Width*Height*3?Offset(memory.Cell):0;}clone.Relationships=social.Relationships.Where(k=>map.ContainsKey(k.Key)).ToDictionary(k=>map[k.Key],k=>{var r=CopyLegacy(k.Value);r.OtherId=map[k.Key];return r;});Danger.People[pair.Value]=clone;}if(old.Physical.Devices.TryGetValue(pair.Key,out var device)){var clone=CopyLegacy(device);clone.Id=pair.Value;clone.Network=0;Physical.Devices[pair.Value]=clone;}}
  foreach(long bed in old.Danger.MedicalBeds.Where(map.ContainsKey))Danger.MedicalBeds.Add(map[bed]);
  var institutionMap=new Dictionary<long,long>();foreach(var original in old.Society.Institutions.Where(i=>map.ContainsKey(i.HostId)&&map.ContainsKey(i.KeeperId))){var clone=CopyLegacy(original);clone.Id=Society.NextId++;institutionMap[original.Id]=clone.Id;clone.HostId=map[original.HostId];clone.KeeperId=map[original.KeeperId];clone.Members=original.Members.Where(map.ContainsKey).Select(id=>map[id]).ToList();clone.Learning=original.Learning.Where(k=>map.ContainsKey(k.Key)).ToDictionary(k=>map[k.Key],k=>k.Value);Society.Institutions.Add(clone);}
  foreach(var original in old.Society.Families.Where(f=>map.ContainsKey(f.First)&&map.ContainsKey(f.Second))){var clone=CopyLegacy(original);clone.First=map[original.First];clone.Second=map[original.Second];Society.Families.Add(clone);}
  foreach(var original in old.Society.Apprentices.Where(a=>map.ContainsKey(a.Mentor)&&map.ContainsKey(a.Apprentice)&&institutionMap.ContainsKey(a.InstitutionId))){var clone=CopyLegacy(original);clone.Mentor=map[original.Mentor];clone.Apprentice=map[original.Apprentice];clone.InstitutionId=institutionMap[original.InstitutionId];Society.Apprentices.Add(clone);}
  foreach(var original in old.Society.Containments.Where(q=>map.ContainsKey(q.EntityId)&&map.ContainsKey(q.KeeperId))){var clone=CopyLegacy(original);clone.EntityId=map[original.EntityId];clone.KeeperId=map[original.KeeperId];clone.Cell=Offset(original.Cell);Society.Containments.Add(clone);}
  foreach(var pair in old.Slice.Formulae.Where(k=>map.ContainsKey(k.Key)))Slice.Formulae[map[pair.Key]].Known=CopyLegacy(pair.Value.Known);
  foreach(long hostile in old.Slice.Hostiles.Where(map.ContainsKey))Slice.Hostiles.Add(map[hostile]);
  foreach(int roof in old.Slice.Roofs.Where(i=>i<Width*Height*3))Slice.Roofs.Add(Offset(roof));foreach(int debris in old.Slice.Debris.Where(i=>i<Width*Height*3))Slice.Debris.Add(Offset(debris));
  foreach(var link in old.Slice.Links.Where(l=>l.A<Width*Height*3&&l.B<Width*Height*3)){var clone=CopyLegacy(link);clone.A=Offset(link.A);clone.B=Offset(link.B);clone.OwnerId=map.GetValueOrDefault(link.OwnerId);Slice.Links.Add(clone);}
  foreach(var a in Region.Artifacts.Where(a=>a.SiteId==site.Id&&a.Custody=="Site")){var prior=old.Region.Artifacts.FirstOrDefault(p=>p.Id==a.Id);if(prior!=null&&map.TryGetValue(prior.LocalEntityId,out long id)){a.LocalEntityId=id;Find(id)!.Name=a.Name;}else {a.Custody="Lost";a.LocalEntityId=0;}}
  var anchor=old.Entities.FirstOrDefault(e=>old.Colony.People.ContainsKey(e.Id)&&old.CanAct(e.Id))??old.Entities.First(e=>d.Entities[e.DefinitionId].Kind==EntityKind.ItemStack);site.AnchorCell=Offset(old.Index(anchor.X,anchor.Y,anchor.Z));site.Instantiated=true;archive.Visited=true;Chronicle("LegacyEncounter","Successors encountered the surviving structures, people and artifacts of "+site.Name+". "+archive.Conversion,site:site.Id);
 }
}
