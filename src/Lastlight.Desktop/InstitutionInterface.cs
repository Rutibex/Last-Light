using Lastlight.Core;
using Microsoft.Xna.Framework;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private bool institutionsPane=true,containmentPane;private int institutionKind,legacyYears=5;private long institutionId,institutionActor,institutionOther,institutionHost;
 private long SocietyActor(){var eligible=world.Colony.People.Keys.Where(id=>world.CanAct(id)&&!world.Colony.People[id].Drafted).Order().ToArray();if(!eligible.Contains(institutionActor))institutionActor=eligible.FirstOrDefault();return institutionActor;}
 private void CycleSocietyActor(bool other){var ids=world.Colony.People.Keys.Where(id=>world.CanAct(id)&&!world.Colony.People[id].Drafted).Order().ToArray();if(ids.Length==0)return;if(other)institutionOther=ids[(Array.IndexOf(ids,institutionOther)+1)%ids.Length];else institutionActor=ids[(Array.IndexOf(ids,institutionActor)+1)%ids.Length];}
 private void ChangeLegacyWorld(bool reclaim)
 {
  try{SaveStore.Write(Path.Combine(SaveStore.DefaultDirectory,"before-legacy-transition.lastlight"),world,CurrentView(),definitions);var next=reclaim?world.ReclaimLegacy(definitions):world.BeginSuccessor(definitions,legacyYears);var view=new ViewState{CameraX=next.Width/2f,CameraY=next.Height/2f,OpenTab="History"};SaveStore.Write(savePath,next,view,definitions);Restore(new(){World=next,View=view});institutionId=institutionActor=institutionOther=institutionHost=0;tool="";RebuildMinimap();status=reclaim?"Earlier settlement reclaimed; inspect the conversion in History.":"Successor founded. Visit the retired settlement through World.";}catch(Exception ex) when(ex is InvalidOperationException or IOException or InvalidDataException or UnauthorizedAccessException){status=ex.Message;}

 }
 private void DrawInstitutions(Rectangle p)
 {
  int x=p.X+18,y=p.Y+51;long actor=SocietyActor();Button(new(x,y,230,27),"Keeper: "+(world.Find(actor)?.Name??"None"),()=>CycleSocietyActor(false),scale:.65f);Button(new(x+240,y,230,27),"Other: "+(world.Find(institutionOther)?.Name??"Choose resident"),()=>CycleSocietyActor(true),scale:.65f);Button(new(x+480,y,165,27),containmentPane?"Institutions":"Containment",()=>containmentPane=!containmentPane,scale:.65f);y+=37;
  if(containmentPane){DrawContainment(p,x,y,actor);return;}
  string kind=World.InstitutionKinds[institutionKind];string expected=kind switch{"Library"=>"study","Guild"=>"workbench","Clinic"=>"bed",_=>"circle"};var hosts=world.Entities.Where(e=>e.DefinitionId==expected&&!e.Forbidden).OrderBy(e=>e.Id).ToArray();if(!hosts.Any(e=>e.Id==institutionHost))institutionHost=hosts.FirstOrDefault()?.Id??0;
  Button(new(x,y,140,27),kind,()=>{institutionKind=(institutionKind+1)%4;institutionHost=0;},scale:.65f);Button(new(x+150,y,250,27),"Host: "+(world.Find(institutionHost)?.Name??"Build "+expected),()=>{if(hosts.Length>0)institutionHost=hosts[(Array.FindIndex(hosts,e=>e.Id==institutionHost)+1)%hosts.Length].Id;},scale:.64f);Button(new(x+410,y,160,27),"Found institution",()=>{var r=Execute(new(CommandKind.InstitutionAction,actor,kind,TargetId:institutionHost,Text:"found"));if(r.Accepted)institutionId=r.EntityId;},scale:.64f);y+=38;
  var all=world.Society.Institutions;if(!all.Any(i=>i.Id==institutionId))institutionId=all.FirstOrDefault()?.Id??0;var chosen=all.FirstOrDefault(i=>i.Id==institutionId);
  if(chosen!=null)
  {Button(new(x,y,230,27),chosen.Kind+" #"+chosen.Id,()=>institutionId=all[(all.IndexOf(chosen)+1)%all.Count].Id,scale:.65f);Button(new(x+240,y,145,27),"Join keeper",()=>Execute(new(CommandKind.InstitutionAction,actor,TargetId:chosen.Id,Text:"join")),scale:.63f);Button(new(x+395,y,145,27),"Join other",()=>Execute(new(CommandKind.InstitutionAction,institutionOther,TargetId:chosen.Id,Text:"join")),scale:.63f);Button(new(x+550,y,155,27),"Apprenticeship",()=>Execute(new(CommandKind.InstitutionAction,actor,TargetId:institutionOther,Quantity:(int)chosen.Id,Text:"apprentice")),scale:.62f);y+=38;
   Paragraph(chosen.Status+" | "+chosen.Sessions+" sessions | members "+string.Join(", ",chosen.Members.Select(id=>world.Find(id)?.Name))+". Assign Study hours in Schedule; keeper and mentor must attend beside the host. Sessions improve permanent skills and meaning.",new(x,y,p.Width-36,48),Muted,.67f);y+=54;
  }
  Button(new(x,y,180,27),"Form partner bond",()=>Execute(new(CommandKind.InstitutionAction,actor,"Partners",TargetId:institutionOther,Text:"family")),scale:.62f);Button(new(x+190,y,150,27),"Recognize kin",()=>Execute(new(CommandKind.InstitutionAction,actor,"Kin",TargetId:institutionOther,Text:"family")),scale:.63f);y+=37;
  Label("Families: "+world.Society.Families.Count+" | Apprentices: "+world.Society.Apprentices.Count(a=>!a.Graduated)+" | Graduates: "+world.Society.Apprentices.Count(a=>a.Graduated),x,y,Cyan,.67f);y+=27;
  foreach(var a in world.Society.Apprentices.Skip(rowOffset).Take(2)){Label(Fit((world.Find(a.Apprentice)?.Name??"Resident")+" learns from "+world.Find(a.Mentor)?.Name+": "+a.Lessons+"/8 lessons"+(a.Graduated?" — graduated":""),p.Width-36,.65f),x,y,Text,.65f);y+=25;}
 }
 private void DrawContainment(Rectangle p,int x,int y,long actor)
 {
  var target=world.Entities.FirstOrDefault(e=>e.DefinitionId=="vault-guardian"&&world.Body(e.Id) is {Dead:false,Downed:true}&&!world.Society.Containments.Any(q=>q.EntityId==e.Id));Button(new(x,y,230,28),"Contain downed guardian",()=>Execute(new(CommandKind.InstitutionAction,actor,TargetId:target?.Id??0,Text:"contain")),hint:"Keeper must be beside a living, downed guardian. Build roof shielding and powered ward; provide light, suitable temperature and real grain.",scale:.65f);y+=38;
  var q=world.Society.Containments.Skip(rowOffset).FirstOrDefault();if(q==null){Paragraph("No specimens. Contain a downed vault guardian beside a capable keeper. Each requirement is measurable; failures accumulate escape progress.",new(x,y,p.Width-36,60),Muted,.7f);return;}
  Label("Specimen #"+q.EntityId+" | stability "+q.Stability+" | escape "+q.Escape+"/100 | knowledge "+q.Knowledge+" | "+(q.Escaped?"Lost / escaped":q.Warning),x,y,q.Escape>=60?Red:Cyan,.65f);y+=30;
  foreach(var requirement in world.ContainmentRequirements(q)){Label((requirement.Value?"Satisfied: ":"UNMET: ")+requirement.Key,x,y,requirement.Value?Cyan:Red,.68f);y+=25;}
  Button(new(x,y,160,28),"Deliver grain",()=>Execute(new(CommandKind.InstitutionAction,actor,TargetId:q.EntityId,Text:"feed")),scale:.65f);Button(new(x+170,y,185,28),"Study rate: "+q.StudyRate,()=>Execute(new(CommandKind.InstitutionAction,actor,TargetId:q.EntityId,Quantity:(q.StudyRate+1)%4,Text:"study")),hint:"0 recovers stability; 1–3 yield knowledge each attended, safe hour at a stability cost. Eight knowledge unlocks Patient Door. Knowledge reduces escape pressure.",scale:.65f);
 }
 private void DrawLegacyHistory(Rectangle p)
 {
  int x=p.X+18,y=p.Y+51;Label("Settlement covenant: "+world.Society.Covenant,x,y,Cyan,.75f);y+=31;
  for(int n=0;n<World.Covenants.Length;n++){string name=World.Covenants[n];Button(new(x+n*190,y,180,27),name,()=>Execute(new(CommandKind.InstitutionAction,DefinitionId:name,Text:"covenant")),scale:.64f);}y+=37;
  Paragraph("Open archive doubles library learning. Mutual care makes doctor work urgent when medical beds exist. Sacred custody honors stable containment. Daily practice brings pride; unmet promises bring disquiet.",new(x,y,p.Width-36,48),Muted,.66f);y+=54;
  if(world.Society.Legacy is {} archive){Paragraph(archive.Conversion,new(x,y,p.Width-36,50),Gold,.65f);y+=56;Button(new(x,y,190,27),"Locate retired settlement",()=>{regionalSite=archive.SiteId;solarWorldPane=false;tab="World";},scale:.63f);Button(new(x+200,y,210,27),"Reclaim unvisited site",()=>ChangeLegacyWorld(true),hint:"Replaces the active settlement with the archived map and its documented decay. A backup is saved before transition. Visit and reclaim are alternative routes in this slice.",scale:.64f);y+=37;}
  else{Button(new(x,y,205,27),"Elapsed years: "+legacyYears,()=>legacyYears=legacyYears>=20?1:legacyYears+1,scale:.65f);Button(new(x+215,y,245,27),"Retire and found successor",()=>ChangeLegacyWorld(false),hint:"Saves a backup and an embedded archive, then starts a successor. Return expeditions and resolve raids first. One retirement per campaign.",scale:.65f);y+=37;}
  foreach(var entry in world.Region.History.AsEnumerable().Reverse().Skip(rowOffset).Take(Math.Max(1,(p.Bottom-y-44)/24))){Label(Fit(entry.Kind+": "+entry.Text,p.Width-36,.64f),x,y,Text,.64f);y+=24;}
 }
}
