using Lastlight.Core;
using Microsoft.Xna.Framework;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private bool regionalShielding;
 private long regionalSite,regionalParty,regionalQuest,regionalFaction;
 private string regionalAgreement="Safe passage",regionalGood="medicine";
 private int partySize=2;
 private void ChooseRegionDefaults(){regionalSite=world.Region.Sites.Any(s=>s.Id==regionalSite)?regionalSite:world.Region.Sites.First(s=>s.Kind=="University").Id;regionalParty=world.Region.Parties.Any(p=>p.Id==regionalParty)?regionalParty:world.Region.Parties.LastOrDefault()?.Id??0;regionalQuest=world.Region.Quests.Any(q=>q.Id==regionalQuest)?regionalQuest:world.Region.Quests[0].Id;regionalFaction=world.Region.Factions.Any(f=>f.Id==regionalFaction)?regionalFaction:world.Region.Factions[1].Id;}
 private long[] ProposedCrew(){var chosen=selection.Where(id=>world.Colony.People.ContainsKey(id)&&world.CanAct(id)&&!world.Colony.People[id].Drafted).Order().ToArray();return (chosen.Length>0?chosen:world.Colony.People.Keys.Where(id=>world.CanAct(id)&&!world.Colony.People[id].Drafted).Order().ToArray()).Take(partySize).ToArray();}
 private long ExpeditionActor(Expedition party)=>party.Members.Contains(selectedId)&&world.CanAct(selectedId)?selectedId:party.Members.FirstOrDefault(id=>world.CanAct(id));
 private void DrawRegionalWorld(Rectangle p)
 {
  ChooseRegionDefaults();int x=p.X+18,y=p.Y+54;var map=new Rectangle(x,y,216,216);
  foreach(var tile in world.Region.Tiles){var rect=new Rectangle(map.X+tile.X*18,map.Y+tile.Y*18,17,17);Fill(rect,tile.Terrain=="Highlands"?new Color(96,99,105):tile.Terrain=="Wetland"?new Color(56,101,120):new Color(112,116,77));if(tile.Road)Fill(new(rect.X+6,rect.Y+6,5,5),Gold);}
  foreach(var site in world.Region.Sites){var rect=new Rectangle(map.X+site.X*18+3,map.Y+site.Y*18+3,11,11);Outline(rect,site.Id==regionalSite?Text:site.Kind=="Town"?Cyan:Gold,2);long id=site.Id;hits.Add(new(rect,()=>regionalSite=id,site.Discovered?site.Name:"Unsurveyed historical site",false));}
  x+=240;var selected=world.Region.Sites.First(s=>s.Id==regionalSite);Label(Fit(selected.Name+" | "+selected.Kind,p.Width-290,.8f),x,y,Cyan,.8f);y+=32;
  Paragraph(selected.HistoryTag+" | "+world.Region.Tiles[selected.Y*12+selected.X].Terrain+" | climate "+world.Region.Tiles[selected.Y*12+selected.X].Climate+" C | culture "+world.Region.Factions[world.Region.Tiles[selected.Y*12+selected.X].Culture-1].Name+" | route "+world.RouteHours(selected.Id)+" game hours; cost depends on terrain and roads. Region seed "+world.Seed+".",new(x,y,p.Width-290,56),Muted,.69f);y+=63;
  Button(new(x,y,185,27),"Next destination",()=>{var sites=world.Region.Sites.Where(s=>s.Kind!="Colony").ToArray();regionalSite=sites[(Array.FindIndex(sites,s=>s.Id==regionalSite)+1)%sites.Length].Id;},scale:.65f);
  Button(new(x+195,y,185,27),"Party size: "+partySize,()=>partySize=partySize>=Math.Min(5,world.Colony.People.Count)?1:partySize+1,scale:.65f);y+=37;
  Button(new(x,y,230,29),"Provision and depart",()=>{var members=ProposedCrew();if(members.Length==0){status="Choose capable undrafted residents.";return;}var result=Execute(new(CommandKind.RegionAction,members[0],string.Join(',',members),TargetId:regionalSite,Quantity:2,Rotation:regionalShielding?1:0,Text:"depart"));if(result.Accepted)regionalParty=result.EntityId;},hint:"Real two meals and one medicine per person, plus ten brass; shift-click residents on the map for preferred crew.",scale:.69f);if(world.Solar.Enabled)Button(new(x+240,y,200,29),"Cloth shields: "+(regionalShielding?"on":"off"),()=>regionalShielding=!regionalShielding,hint:"Packing requires one real cloth per member; a flare consumes it to prevent travel burns.",scale:.63f);y+=40;
  Label(Fit("Next crew: "+string.Join(", ",ProposedCrew().Select(id=>world.Find(id)!.Name))+" | shift-click residents on the map to choose",p.Width-290,.61f),x,y,Muted,.61f);y+=25;
  var party=world.Region.Parties.FirstOrDefault(a=>a.Id==regionalParty);if(party!=null)
  {Label($"Expedition #{party.Id}: {party.State} | cargo {party.Cargo.Values.Sum()}/{party.Members.Count*40}",x,y,Gold,.7f);y+=30;
   Button(new(x,y,125,26),"Locate party",()=>{if(party.State=="AtSite")Focus(party.Members[0]);else status="Party is "+party.State+"; progress "+party.RouteIndex+"/"+party.Route.Count;},scale:.6f);
   Button(new(x+135,y,130,26),"Travel selected",()=>Execute(new(CommandKind.RegionAction,TargetId:party.Id,Quantity:(int)regionalSite,Text:"travel")),scale:.6f);Button(new(x+275,y,110,26),"Return home",()=>Execute(new(CommandKind.RegionAction,TargetId:party.Id,Text:"return")),scale:.6f);y+=35;
   var artifact=world.Region.Artifacts.FirstOrDefault(a=>a.SiteId==party.SiteId&&a.Custody=="Site");if(artifact!=null){Button(new(x,y,165,26),"Locate artifact",()=>Focus(artifact.LocalEntityId),scale:.62f);Button(new(x+175,y,210,26),"Recover artifact",()=>Execute(new(CommandKind.RegionAction,ExpeditionActor(party),TargetId:artifact.Id,Text:"take")),hint:"Move a drafted party member beside it; contracts or scholarly access settle permission.",scale:.62f);}
   if(party.State=="AtSite"){Button(new(x,y+36,125,26),"Unpack medicine",()=>Execute(new(CommandKind.RegionAction,ExpeditionActor(party),"medicine",TargetId:party.Id,Quantity:1,Text:"unpack")),scale:.62f);Button(new(x+135,y+36,125,26),"Unpack meal",()=>Execute(new(CommandKind.RegionAction,ExpeditionActor(party),"meal",TargetId:party.Id,Quantity:1,Text:"unpack")),hint:"Place supplies for eating and normal medicine work; undraft workers at the site.",scale:.62f);Button(new(x+270,y+36,140,26),"Pack nearby goods",()=>Execute(new(CommandKind.RegionAction,ExpeditionActor(party),regionalGood,TargetId:party.Id,Quantity:1,Text:"pack")),hint:"Uses the goods selected in Factions; adjacent, allowed, unclaimed stacks only.",scale:.59f);}
  }
  var trader=world.Region.Traders.FirstOrDefault(t=>t.State=="Visiting");if(trader!=null)Button(new(p.X+18,p.Bottom-60,215,28),"Buy visitor medicine",()=>Execute(new(CommandKind.RegionAction,world.Colony.People.Keys.FirstOrDefault(id=>world.CanAct(id)&&!world.Colony.People[id].Drafted),"medicine",Quantity:1,TargetId:trader.Id,Text:"visitor-buy")),hint:"Worker carries real brass to the visiting trader.",scale:.62f);
 }
 private void DrawRegionalFactions(Rectangle p)
 {
  ChooseRegionDefaults();int x=p.X+18,y=p.Y+54;var faction=world.Region.Factions.First(f=>f.Id==regionalFaction);
  Button(new(x,y,360,28),Fit(faction.Name,340,.67f),()=>regionalFaction=world.Region.Factions[(world.Region.Factions.IndexOf(faction)+1)%world.Region.Factions.Count].Id,scale:.67f);y+=38;
  Label($"Goodwill {faction.Goodwill} | trust {faction.Trust} | compatibility {faction.Compatibility} | grievance {faction.Grievance}",x,y,Cyan,.66f);y+=30;Label($"People {faction.Population} | strength {faction.Strength} | objective {faction.Objective}",x,y,Text,.7f);y+=27;
  Label("Envoy: "+world.Region.People.First(p=>p.FactionId==faction.Id&&p.Role=="Envoy").Name,x,y,Muted,.65f);y+=24;
  Label(Fit("Reserves: "+string.Join(", ",faction.Reserves.Select(k=>k.Key+" "+k.Value)),p.Width-40,.62f),x,y,Muted,.62f);y+=35;
  Label("Active terms: "+string.Join(", ",world.Region.Treaties.Where(t=>t.FactionId==faction.Id&&t.Active).Select(t=>t.Kind)),x,y,Gold,.66f);y+=36;
  Button(new(x,y,220,28),regionalAgreement,()=>regionalAgreement=World.TreatyKinds[(Array.IndexOf(World.TreatyKinds,regionalAgreement)+1)%World.TreatyKinds.Length],scale:.66f);
  var party=world.Region.Parties.LastOrDefault(a=>a.State=="AtSite"&&world.Region.Sites.First(s=>s.Id==a.SiteId).FactionId==faction.Id);long actor=party==null?0:ExpeditionActor(party);
  Button(new(x+230,y,170,28),"Sign agreement",()=>Execute(new(CommandKind.RegionAction,actor,regionalAgreement,TargetId:party?.Id??0,Text:"treaty")),hint:"Negotiate beside the faction's town counter with adequate goodwill/trust.",scale:.65f);Button(new(x+410,y,160,28),"Pay tribute",()=>Execute(new(CommandKind.RegionAction,actor,TargetId:party?.Id??0,Text:"pay")),scale:.65f);y+=40;
  string[] goods=["medicine","meal","grain","f-parts","wood"];Button(new(x,y,220,28),"Goods: "+regionalGood,()=>regionalGood=goods[(Array.IndexOf(goods,regionalGood)+1)%goods.Length],scale:.67f);Button(new(x+230,y,130,28),"Buy one",()=>Execute(new(CommandKind.RegionAction,actor,regionalGood,Quantity:1,TargetId:party?.Id??0,Text:"buy")),scale:.67f);Button(new(x+370,y,130,28),"Sell one",()=>Execute(new(CommandKind.RegionAction,actor,regionalGood,Quantity:1,TargetId:party?.Id??0,Text:"sell")),scale:.67f);y+=41;
  Paragraph("Trade uses actual party cargo and faction reserves. Access and routes depend on relations and signed terms. Tribute promises three brass every seven days. Independent ancient guardians and scavengers are separate from faction treaties.",new(x,y,p.Width-40,76),Muted,.68f);
 }
 private void DrawRegionalQuests(Rectangle p)
 {
  ChooseRegionDefaults();int x=p.X+18,y=p.Y+54;var q=world.Region.Quests.First(a=>a.Id==regionalQuest);
  Button(new(x,y,390,29),q.Name,()=>regionalQuest=world.Region.Quests[(world.Region.Quests.IndexOf(q)+1)%world.Region.Quests.Count].Id,scale:.75f);y+=41;
  Label(q.Kind+" | "+q.Status+" | claimant: "+world.Region.Factions.First(f=>f.Id==q.FactionId).Name,x,y,Cyan,.68f);y+=33;
  Paragraph(q.Kind=="Recovery"?"Recover the unique lens from the fallen college, then deliver it at the claimant's town counter. Accepting grants recovery permission. Custody and restitution remain linked to the original artifact identity.":q.Kind=="Aid"?"Bring six packed meals to the claimant's town counter. Relief and rewards use actual inventories.":"Visit the fallen college, then report your survey at the claimant's town counter.",new(x,y,p.Width-38,80),Text,.74f);y+=91;
  Label("Reward: "+string.Join(", ",q.Reward.Select(k=>k.Value+" "+k.Key))+(q.Status=="Accepted"?" | due game day "+(q.Deadline/World.TicksPerDay+1):" | twelve days after acceptance"),x,y,Gold,.68f);y+=37;
  Button(new(x,y,130,28),"Accept contract",()=>Execute(new(CommandKind.RegionAction,TargetId:q.Id,Quantity:1,Text:"quest")),scale:.63f);Button(new(x+140,y,130,28),"Decline contract",()=>Execute(new(CommandKind.RegionAction,TargetId:q.Id,Quantity:0,Text:"quest")),scale:.63f);Button(new(x+280,y,140,28),"Select destination",()=>{regionalSite=q.SiteId;tab="World";},scale:.63f);
  var party=world.Region.Parties.LastOrDefault(a=>a.State=="AtSite"&&world.Region.Sites.First(s=>s.Id==a.SiteId).FactionId==q.FactionId);Button(new(x+430,y,155,28),"Fulfill contract",()=>Execute(new(CommandKind.RegionAction,party==null?0:ExpeditionActor(party),TargetId:party?.Id??0,Quantity:(int)q.Id,Text:"complete")),hint:"At the claimant's counter with required artifact/supplies.",scale:.63f);
 }
 private void DrawRegionalHistory(Rectangle p)
 {
  int x=p.X+18,y=p.Y+53;Label($"Regional chronicle | {world.Region.ArtifactTransfers} custody transfers | {world.Region.TradeTransactions} trades",x,y,Cyan,.73f);y+=34;
  foreach(var entry in world.Region.History.AsEnumerable().Reverse().Skip(rowOffset).Take(Math.Max(1,(p.Height-130)/40)))
  {var h=entry;Label(Fit($"Year {h.Year} / tick {h.Tick} | {h.Kind}: {h.Text}",p.Width-130,.61f),x,y,Text,.61f);Button(new(p.Right-100,y-3,75,24),"Inspect",()=>{if(h.LocalActorId!=0&&world.Find(h.LocalActorId)!=null)Focus(h.LocalActorId);else if(h.SiteId!=0){regionalSite=h.SiteId;tab="World";}else if(h.FactionId!=0){regionalFaction=h.FactionId;tab="Factions";}},hint:"Linked person, site, faction or artifact references.",scale:.6f);y+=40;}
 }
}
