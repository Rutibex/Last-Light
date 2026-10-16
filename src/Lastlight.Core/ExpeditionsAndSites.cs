namespace Lastlight.Core;
public sealed partial class World
{
 public List<int>? RegionalRoute(long siteId,int start=-1)
 {
  var site=Region.Sites.FirstOrDefault(s=>s.Id==siteId);if(site==null)return null;var home=Region.Sites.First(s=>s.Id==Region.HomeSiteId);if(start<0)start=home.Y*Region.Width+home.X;int goal=site.Y*Region.Width+site.X;
  var cost=new Dictionary<int,int>{{start,0}};var from=new Dictionary<int,int>{{start,-1}};var queue=new PriorityQueue<int,(int,int)>();queue.Enqueue(start,(0,start));
  while(queue.TryDequeue(out int at,out var priority)){if(priority.Item1!=cost[at])continue;if(at==goal){var route=new List<int>();for(int i=at;i!=start;i=from[i])route.Add(i);route.Reverse();return route;}int x=at%Region.Width,y=at/Region.Width;foreach(var (dx,dy) in new[]{(0,-1),(-1,0),(1,0),(0,1)}){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=Region.Width||ny>=Region.Height)continue;int next=ny*Region.Width+nx;var tile=Region.Tiles[next];int extra=tile.Road?1:tile.Terrain=="Highlands"?3:tile.Terrain=="Wetland"?2:1;int value=cost[at]+extra;if(!cost.TryGetValue(next,out int old)||value<old){cost[next]=value;from[next]=at;queue.Enqueue(next,(value,next));}}}return null;
 }
 public int RouteHours(long site)=>RegionalRoute(site)?.Sum(i=>Region.Tiles[i].Road?1:Region.Tiles[i].Terrain=="Highlands"?3:Region.Tiles[i].Terrain=="Wetland"?2:1)??0;
 private CommandResult BeginExpedition(WorldCommand c,Definitions d)
 {
  var site=Region.Sites.FirstOrDefault(s=>s.Id==c.TargetId);if(site==null||site.Id==Region.HomeSiteId)return new(false,"Choose a regional destination.");if(Region.Parties.Count>=32||Region.Parties.Count(p=>p.State is "Packing" or "Outbound" or "AtSite" or "Returning")>=3)return new(false,"Expedition capacity reached.");
  var members=c.DefinitionId.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(s=>long.TryParse(s,out long id)?id:0).Distinct().ToList();if(members.Count is <1 or >8||!members.Contains(c.EntityId)||members.Any(id=>!Colony.People.TryGetValue(id,out var p)||p.Drafted||!CanAct(id)||IsAway(id)||Region.Parties.Any(p=>p.State is "Packing" or "Outbound" or "AtSite" or "Returning"&&p.Members.Contains(id))))return new(false,"Choose capable undrafted residents who are not already on an expedition.");
  if(site.Kind=="Town"&&!TradeAccess(site.FactionId)&&!HasTreaty(site.FactionId,"Safe passage"))return new(false,"Faction entry is restricted; safe passage is required.");
  var route=RegionalRoute(site.Id);if(route==null||route.Count==0)return new(false,"No regional route.");var leader=Find(c.EntityId)!;int food=Math.Max(2*members.Count,c.Quantity*members.Count);bool shield=Solar.Enabled&&c.Rotation==1;if(food+10+members.Count*(shield?2:1)>members.Count*40)return new(false,"Requested cargo exceeds party carrying capacity.");
  var party=new Expedition{Id=Region.NextId++,DestinationId=site.Id,Members=members,HomeCells=members.ToDictionary(id=>id,id=>Index(Find(id)!.X,Find(id)!.Y,Find(id)!.Z)),Route=route,NextAt=Tick};
  var job=new ColonyJob{PawnId=leader.Id,Kind="Provision",TargetId=party.Id,Cell=Index(leader.X,leader.Y,leader.Z),Claims=members.Select(id=>"travel:"+id).ToList()};var provisions=new Dictionary<string,int>{{"meal",food},{"brass",10},{"medicine",members.Count}};if(shield)provisions["f-cloth"]=members.Count;if(!ReserveIngredients(job,provisions,d))return new(false,"Need reachable unclaimed meals, medicine, ten brass and any requested cloth shielding.");
  Interrupt(leader.Id,d);if(!Start(job))return new(false,"Provisioning claims are unavailable.");Region.Parties.Add(party);Chronicle("Expedition",leader.Name+" began provisioning an expedition to "+site.Name+".",site:site.Id,actor:leader.Id);return new(true,"Packing real supplies; residents remain home until collection completes.",party.Id);
 }
 private bool ValidRegionJob(ColonyJob job,PawnProfile profile)
 {
  if(profile.Drafted||job.Sources.Any(id=>Find(id)==null||Find(id)!.Forbidden))return false;
  if(job.Kind=="Provision")return Region.Parties.FirstOrDefault(p=>p.Id==job.TargetId) is {} party&&party.State=="Packing"&&party.Members.All(id=>CanAct(id)&&!Colony.People[id].Drafted);
  return Region.Traders.Any(t=>t.Id==job.TargetId&&t.State=="Visiting"&&TradeAccess(t.FactionId)&&Tick<t.LeavesAt);
 }
 private bool HandleRegionJob(ColonyJob job,Entity actor,Definitions d)
 {
  if(job.Kind is not ("Provision" or "RegionalTrade"))return false;
  if(job.Kind=="Provision")
  {var party=Region.Parties.First(p=>p.Id==job.TargetId);party.Cargo=new(job.Cargo);job.Cargo.Clear();Interrupt(actor.Id,d);foreach(long id in party.Members){Interrupt(id,d);Colony.People[id].Drafted=true;Colony.People[id].Activity="Traveling expedition";Region.Absent.Add(id);}party.State="Outbound";party.NextAt=Tick;Chronicle("Departure","The provisioned party departed for "+Region.Sites.First(s=>s.Id==party.DestinationId).Name+".",site:party.DestinationId,actor:actor.Id);Danger.PauseSerial++;return true;}
  var trader=Region.Traders.First(t=>t.Id==job.TargetId);int count=job.Work;int cost=TradePrice(job.DefinitionId,true)*count;if(trader.State!="Visiting"||trader.Cargo.GetValueOrDefault(job.DefinitionId)<count||job.Cargo.GetValueOrDefault("brass")<cost){Interrupt(actor.Id,d);return true;}trader.Cargo[job.DefinitionId]-=count;trader.Cargo["brass"]=trader.Cargo.GetValueOrDefault("brass")+cost;Drop(job.DefinitionId,count,Index(actor.X,actor.Y,actor.Z),d);job.Cargo.Clear();Region.TradeTransactions++;Chronicle("Trade",actor.Name+" exchanged delivered brass for trader goods.",trader.FactionId,Region.HomeSiteId,actor:actor.Id);Interrupt(actor.Id,d);return true;
 }
 private void AdvanceExpeditions(Definitions d)
 {
  foreach(var party in Region.Parties.OrderBy(p=>p.Id))
  {
   if(party.State=="Packing"){if(!Autonomy.Jobs.Any(j=>j.Kind=="Provision"&&j.TargetId==party.Id)){party.State="Cancelled";Chronicle("Expedition","Provisioning was cancelled; carried supplies were returned.",site:party.DestinationId);}continue;}
   if(party.State is not ("Outbound" or "Returning"))continue;
   foreach(long id in party.Members){var p=Colony.People[id];p.Food=Math.Max(0,p.Food-.9);p.Rest=Math.Max(250,p.Rest-.1);if(p.Food<450&&party.Cargo.GetValueOrDefault("meal")>0){party.Cargo["meal"]--;party.FoodUsed++;p.MealsEaten++;p.Food=Math.Min(1000,p.Food+750);}if(p.Food==0&&Tick%TicksPerHour==0)InflictWound(id,"torso","Bruise",3,0,d);}
   if(!party.Members.Any(id=>Body(id) is {Dead:false,Downed:false})){party.State="Stranded";Chronicle("Expedition","The party became stranded without a capable traveler.",site:party.DestinationId);continue;}
   if(Tick<party.NextAt)continue;if(party.RouteIndex<party.Route.Count){int cell=party.Route[party.RouteIndex++];var tile=Region.Tiles[cell];party.NextAt=Tick+TicksPerHour*(tile.Road?1:tile.Terrain=="Highlands"?3:tile.Terrain=="Wetland"?2:1);continue;}
   if(party.Returning){ReturnParty(party,d);continue;}var site=Region.Sites.First(s=>s.Id==party.DestinationId);if(site.Kind=="Town"&&!TradeAccess(site.FactionId)&&!HasTreaty(site.FactionId,"Safe passage")){party.Route=RegionalRoute(Region.HomeSiteId,site.Y*12+site.X)??[];party.RouteIndex=0;party.DestinationId=Region.HomeSiteId;party.NextAt=Tick;party.Returning=true;party.State="Returning";Chronicle("Diplomacy","Entry was refused after route permissions changed; the expedition is returning.",site.FactionId,site.Id);continue;}InstantiateRegionalSite(site,d);party.SiteId=site.Id;party.State="AtSite";site.Discovered=true;var pos=Position(site.AnchorCell);int member=0;foreach(long id in party.Members){Region.Absent.Remove(id);var actor=Find(id)!;actor.X=pos.X+member++%3;actor.Y=pos.Y;actor.Z=pos.Z;Colony.People[id].Activity="Expedition at "+site.Name;}Chronicle("Arrival","The party reached "+site.Name+"; its "+site.HistoryTag+" history shapes the site.",site.FactionId,site.Id,actor:party.Members[0]);Danger.PauseSerial++;
  }
 }
 private void InstantiateRegionalSite(RegionSite site,Definitions d)
 {
  if(site.Instantiated)return;if(site.Kind=="Retired settlement"){InstantiateLegacySite(site,d);return;}while(Levels<4){for(int row=0;row<Height;row++)for(int column=0;column<Width;column++)Cells.Add(new(){TerrainId="stone"});Levels++;}
  int x=4+(site.Slot%4)*28,y=4+(site.Slot/4)*28,z=3;for(int yy=y;yy<y+18;yy++)for(int xx=x;xx<x+22;xx++){CellAt(xx,yy,z).TerrainId="floor";Slice.Roofs.Add(Index(xx,yy,z));}
  long Spawn(string id,int sx,int sy,string name=""){var r=Apply(new(CommandKind.Spawn,DefinitionId:id,X:sx,Y:sy,Z:z),d);if(!r.Accepted)throw new InvalidDataException(r.Message);site.EntityIds.Add(r.EntityId);if(name!="")Find(r.EntityId)!.Name=name;return r.EntityId;}
  foreach(int cx in new[]{x+3,x+7,x+11,x+15,x+19})foreach(int cy in new[]{y+3,y+7,y+11,y+15})Spawn("column",cx,cy);
  site.AnchorCell=Index(x+2,y+1,z);site.MarketId=Spawn(site.Kind=="Town"?"study":"observatory-tablet",x+5,y+3,site.Kind=="Town"?"Regional trade counter":"Site survey tablet");
  foreach(var artifact in Region.Artifacts.Where(a=>a.SiteId==site.Id&&a.Custody=="Site"))artifact.LocalEntityId=Spawn(artifact.DefinitionId,x+14,y+7,artifact.Name);
  if(site.Kind=="University")Spawn("study",x+8,y+8,"Abandoned scholarly desk");if(site.Kind=="Medical vault")Danger.MedicalBeds.Add(Spawn("bed",x+8,y+8,"Vault treatment bed"));if(site.Kind=="Seed archive")for(int xx=x+7;xx<x+12;xx++)CellAt(xx,y+10,z).TerrainId="soil";
  if(site.Kind is "Machine monastery" or "Necropolis"){site.GuardianId=Spawn("vault-guardian",x+15,y+10,"Ancient site guardian");Slice.Hostiles.Add(site.GuardianId);Body(site.GuardianId)!.WeaponId="guardian-focus";}
  site.Instantiated=true;
 }
 private void ReturnParty(Expedition party,Definitions d)
 {
  foreach(long id in party.Members){Region.Absent.Remove(id);int cell=party.HomeCells[id];if(!Traversable(cell,d))cell=NearestTerrainCell(cell,d);MoveActor(Find(id)!,cell);Colony.People[id].Drafted=false;Colony.People[id].RetryAt=Tick;}
  int drop=Index(Find(party.Members[0])!.X,Find(party.Members[0])!.Y,Find(party.Members[0])!.Z);foreach(var cargo in party.Cargo.Where(k=>k.Value>0))Drop(cargo.Key,cargo.Value,drop,d);party.Cargo.Clear();
  foreach(long id in party.Artifacts.ToArray()){var artifact=Region.Artifacts.First(a=>a.Id==id);var at=Position(drop);var result=Apply(new(CommandKind.Spawn,DefinitionId:artifact.DefinitionId,X:at.X,Y:at.Y,Z:at.Z),d);if(!result.Accepted)throw new InvalidDataException(result.Message);artifact.LocalEntityId=result.EntityId;Find(result.EntityId)!.Name=artifact.Name;artifact.Custody="Colony";artifact.PartyId=0;artifact.SiteId=Region.HomeSiteId;Region.ArtifactTransfers++;Chronicle("ArtifactTransfer",artifact.Name+" returned to the refuge in the party's custody.",artifact.OwnerFactionId,Region.HomeSiteId,artifact.Id,party.Members[0]);}party.Artifacts.Clear();party.State="Returned";foreach(long id in party.Members)if(Body(id) is {Dead:false})Remember(id,"ExpeditionReturn","I returned from a regional expedition.",4,party.Members[0],3);Chronicle("Return","The expedition returned with its remaining real cargo.",site:Region.HomeSiteId,actor:party.Members[0]);Danger.PauseSerial++;
 }
 private CommandResult TakeRegionalArtifact(WorldCommand c,Definitions d)
 {
  var artifact=Region.Artifacts.FirstOrDefault(a=>a.Id==c.TargetId);var party=Region.Parties.FirstOrDefault(p=>p.State=="AtSite"&&p.Members.Contains(c.EntityId));var actor=Find(c.EntityId);if(artifact==null||party==null||actor==null||!CanAct(actor.Id)||artifact.Anchored||artifact.Custody!="Site"||artifact.SiteId!=party.SiteId||Find(artifact.LocalEntityId) is not {} item)return new(false,"Select a party member beside a recoverable site artifact.");if(actor.Z!=item.Z||Distance(actor,item)>1)return new(false,"Move the drafted member beside the artifact first.");if(party.Cargo.Values.Sum()+party.Artifacts.Count+1>party.Members.Count*40)return new(false,"Expedition carrying capacity is full.");var site=Region.Sites.First(s=>s.Id==party.SiteId);if(site.GuardianId!=0&&Body(site.GuardianId) is {Dead:false,Downed:false})return new(false,"The site's guardian must be disabled first.");
  bool permission=HasTreaty(artifact.OwnerFactionId,"Artifact restitution")||HasTreaty(artifact.OwnerFactionId,"Scholarly access")||Region.Quests.Any(q=>q.Status=="Accepted"&&q.ArtifactId==artifact.Id);if(!permission&&artifact.OwnerFactionId!=0){var f=Region.Factions.First(f=>f.Id==artifact.OwnerFactionId);f.Grievance=Math.Min(100,f.Grievance+20);f.Goodwill=Math.Max(-100,f.Goodwill-20);Chronicle("Claim","Taking a claimed artifact without permission damaged relations.",f.Id,site.Id,artifact.Id,actor.Id);}
  artifact.LocalEntityId=0;artifact.Custody="Party";artifact.PartyId=party.Id;Apply(new(CommandKind.Remove,item.Id),d);party.Artifacts.Add(artifact.Id);Region.ArtifactTransfers++;Remember(actor.Id,"RegionalDiscovery","I recovered "+artifact.Name+".",5,0,3);Chronicle("ArtifactTransfer",actor.Name+" recovered "+artifact.Name+" into expedition custody.",artifact.OwnerFactionId,site.Id,artifact.Id,actor.Id);return new(true,"Unique artifact secured; its claim and custody are recorded.");
 }
}
