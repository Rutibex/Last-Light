namespace Lastlight.Core;
public sealed class Institution {public long Id{get;set;}public string Kind{get;set;}="Library";public long HostId{get;set;}public long KeeperId{get;set;}public List<long> Members{get;set;}=[];public int Sessions{get;set;}public Dictionary<long,int> Learning{get;set;}=new();public string Status{get;set;}="Awaiting study hours";}
public sealed class FamilyBond {public long First{get;set;}public long Second{get;set;}public string Kind{get;set;}="Partners";public long Since{get;set;}}
public sealed class Apprenticeship {public long Mentor{get;set;}public long Apprentice{get;set;}public long InstitutionId{get;set;}public int Lessons{get;set;}public bool Graduated{get;set;}}
public sealed class Containment {public long EntityId{get;set;}public long KeeperId{get;set;}public int Cell{get;set;}public int Feed{get;set;}public int Stability{get;set;}=100;public int Escape{get;set;}public int Knowledge{get;set;}public int StudyRate{get;set;}public long LastFeedAt{get;set;}public bool Escaped{get;set;}public string Warning{get;set;}="";}
public sealed class LegacyArchive {public long SiteId{get;set;}public string Snapshot{get;set;}="";public long RetiredAt{get;set;}public int ElapsedYears{get;set;}public bool Visited{get;set;}public bool Reclaimed{get;set;}public string Conversion{get;set;}="";public Dictionary<long,long> EntityMap{get;set;}=new();}
public sealed class InstitutionState {public bool Initialized{get;set;}public bool Enabled{get;set;}public long NextId{get;set;}=1;public string Covenant{get;set;}="Open archive";public long LastRitualDay{get;set;}=-1;public List<Institution> Institutions{get;set;}=[];public List<FamilyBond> Families{get;set;}=[];public List<Apprenticeship> Apprentices{get;set;}=[];public List<Containment> Containments{get;set;}=[];public LegacyArchive? Legacy{get;set;}}
public sealed partial class World
{
 public InstitutionState Society{get;set;}=new();
 public static readonly string[] Covenants=["Open archive","Mutual care","Sacred custody"];
 public static readonly string[] InstitutionKinds=["Library","Guild","Clinic","Shrine"];
 public void RemoveInstitutionEntity(long id){var removed=Society.Institutions.Where(i=>i.HostId==id||i.KeeperId==id).Select(i=>i.Id).ToHashSet();Society.Institutions.RemoveAll(i=>removed.Contains(i.Id));foreach(var i in Society.Institutions){i.Members.Remove(id);i.Learning.Remove(id);}Society.Apprentices.RemoveAll(a=>a.Mentor==id||a.Apprentice==id||removed.Contains(a.InstitutionId));Society.Families.RemoveAll(f=>f.First==id||f.Second==id);Society.Containments.RemoveAll(q=>q.KeeperId==id);foreach(var q in Society.Containments.Where(q=>q.EntityId==id))q.Escaped=true;}
 public void EnsureInstitutions(){Society.Initialized=true;EnsureSolar();}
 public void EnableInstitutions(){EnsureInstitutions();Society.Enabled=true;}
 public bool IsContained(long id)=>Society.Containments.Any(c=>c.EntityId==id&&!c.Escaped&&Find(id)!=null);
 public CommandResult ApplyInstitutionCommand(WorldCommand c,Definitions d)
 {
  if(c.Text=="enable"){if(!Region.Enabled)return new(false,"Enable the living region first.");EnableInstitutions();Chronicle("Institution","Institutions enabled for the existing settlement.",site:Region.HomeSiteId);return new(true,"Institutions enabled; existing map and residents retained.");}
  if(!Society.Enabled)return new(false,"Institutions are disabled in this older scenario.");
  bool Resident(long id)=>Colony.People.ContainsKey(id)&&CanAct(id)&&!Colony.People[id].Drafted;
  CommandResult Yes(string text,long id=0){Chronicle("Institution",text,actor:c.EntityId,site:Region.HomeSiteId);return new(true,text,id);}
  if(c.Text=="covenant"){if(!Covenants.Contains(c.DefinitionId))return new(false,"Unknown covenant.");Society.Covenant=c.DefinitionId;return Yes("Covenant adopted: "+c.DefinitionId+".");}
  if(c.Text=="found")
  {
   if(!Resident(c.EntityId)||Find(c.TargetId) is not {} host||host.Forbidden||!InstitutionKinds.Contains(c.DefinitionId)||Society.Institutions.Count>=12||Society.Institutions.Any(i=>i.HostId==host.Id))return new(false,"Choose a capable keeper and an unassigned institution building (limit twelve).");
   string expected=c.DefinitionId switch{"Library"=>"study","Guild"=>"workbench","Clinic"=>"bed",_=>"circle"};if(host.DefinitionId!=expected||FindPath(Find(c.EntityId)!,Colony.People[c.EntityId],Index(host.X,host.Y,host.Z),true,d)==null)return new(false,"Institution requires a reachable "+expected+".");
   var i=new Institution{Id=Society.NextId++,Kind=c.DefinitionId,HostId=host.Id,KeeperId=c.EntityId,Members=[c.EntityId]};Society.Institutions.Add(i);return Yes(i.Kind+" founded by "+Find(c.EntityId)!.Name+".",i.Id);
  }
  if(c.Text=="join"){var i=Society.Institutions.FirstOrDefault(i=>i.Id==c.TargetId);if(i==null||!Resident(c.EntityId))return new(false,"Choose a capable resident and institution.");if(!i.Members.Contains(c.EntityId))i.Members.Add(c.EntityId);return Yes(Find(c.EntityId)!.Name+" joined the "+i.Kind+"; assign Study hours for sessions.");}
  if(c.Text=="family")
  {if(Society.Families.Count>=200||!Resident(c.EntityId)||!Resident(c.TargetId)||c.EntityId==c.TargetId||c.DefinitionId is not ("Partners" or "Kin")||Society.Families.Any(f=>f.First==c.EntityId&&f.Second==c.TargetId||f.First==c.TargetId&&f.Second==c.EntityId)||c.DefinitionId=="Partners"&&(Colony.People[c.EntityId].Age<18||Colony.People[c.TargetId].Age<18||Society.Families.Any(f=>f.Kind=="Partners"&&(f.First==c.EntityId||f.Second==c.EntityId||f.First==c.TargetId||f.Second==c.TargetId))))return new(false,"Choose two eligible residents without this existing bond; adult partners are exclusive.");Society.Families.Add(new(){First=c.EntityId,Second=c.TargetId,Kind=c.DefinitionId,Since=Tick});Remember(c.EntityId,"Family","We formed a lasting "+c.DefinitionId+" bond.",6,c.TargetId,30);Remember(c.TargetId,"Family","We formed a lasting "+c.DefinitionId+" bond.",6,c.EntityId,30);return Yes(Find(c.EntityId)!.Name+" and "+Find(c.TargetId)!.Name+" became "+c.DefinitionId+".");}
  if(c.Text=="apprentice")
  {var i=Society.Institutions.FirstOrDefault(i=>i.Id==c.Quantity);if(Society.Apprentices.Count>=200||i==null||!Resident(c.EntityId)||!Resident(c.TargetId)||c.EntityId==c.TargetId||Society.Apprentices.Any(a=>a.Apprentice==c.TargetId&&!a.Graduated)||!i.Members.Contains(c.EntityId)||!i.Members.Contains(c.TargetId)||Colony.People[c.EntityId].Skills[InstitutionSkill(i)]<=Colony.People[c.TargetId].Skills[InstitutionSkill(i)])return new(false,"Mentor and apprentice must be members; mentor needs a higher skill and apprentice no active placement.");Society.Apprentices.Add(new(){Mentor=c.EntityId,Apprentice=c.TargetId,InstitutionId=i.Id});return Yes("Apprenticeship agreed between "+Find(c.EntityId)!.Name+" and "+Find(c.TargetId)!.Name+".");}
  return ApplyContainmentCommand(c,d);
 }
 private int InstitutionSkill(Institution i)=>i.Kind switch{"Library"=>15,"Guild"=>11,"Clinic"=>2,_=>4};
 private ColonyJob? PickInstitutionJob(Entity pawn,PawnProfile p,Definitions d)
 {
  if(!Society.Enabled)return null;var i=Society.Institutions.FirstOrDefault(i=>i.Members.Contains(pawn.Id)&&Find(i.HostId) is {Forbidden:false}&&CanAct(i.KeeperId));if(i==null)return null;var host=Find(i.HostId)!;int cell=Index(host.X,host.Y,host.Z);if(FindPath(pawn,p,cell,true,d)==null)return null;return new(){PawnId=pawn.Id,Kind="Institution",TargetId=i.Id,Cell=cell,Claims=["institution:"+i.Id+":"+pawn.Id]};
 }
 private bool ValidInstitutionJob(ColonyJob j)=>Society.Enabled&&Society.Institutions.Any(i=>i.Id==j.TargetId&&i.Members.Contains(j.PawnId)&&Find(i.HostId) is {Forbidden:false}&&CanAct(i.KeeperId))&&Colony.People[j.PawnId].Schedule[Tick%TicksPerDay/TicksPerHour]==ScheduleActivity.Study;
 private bool HandleInstitutionJob(ColonyJob j,Entity pawn,Definitions d)
 {
  if(j.Kind!="Institution")return false;var i=Society.Institutions.First(i=>i.Id==j.TargetId);var keeper=Find(i.KeeperId)!;var host=Find(i.HostId)!;
  if(keeper.Z!=host.Z||Distance(keeper,host)>3){i.Status="Keeper must attend beside the host";return true;}
  j.Work++;i.Status="Session in progress";if(j.Work<60)return true;
  int skill=InstitutionSkill(i);int credit=Society.Covenant=="Open archive"&&i.Kind=="Library"?2:1;i.Learning[pawn.Id]=i.Learning.GetValueOrDefault(pawn.Id)+credit;
  if(i.Learning[pawn.Id]>=4){Colony.People[pawn.Id].Skills[skill]=Math.Min(20,Colony.People[pawn.Id].Skills[skill]+1);i.Learning[pawn.Id]-=4;}
  var a=Society.Apprentices.FirstOrDefault(a=>a.Apprentice==pawn.Id&&a.InstitutionId==i.Id&&!a.Graduated);
  if(a!=null&&Find(a.Mentor) is {} mentor&&CanAct(mentor.Id)&&mentor.Z==host.Z&&Distance(mentor,host)<=3&&Colony.People[mentor.Id].Schedule[Tick%TicksPerDay/TicksPerHour]==ScheduleActivity.Study){a.Lessons++;if(a.Lessons%2==0)Colony.People[pawn.Id].Skills[skill]=Math.Min(Colony.People[mentor.Id].Skills[skill],Colony.People[pawn.Id].Skills[skill]+1);if(a.Lessons>=8){a.Graduated=true;Chronicle("Graduation",pawn.Name+" completed an apprenticeship.",actor:pawn.Id,site:Region.HomeSiteId);Remember(pawn.Id,"Graduation","My mentor taught me a lasting skill.",8,a.Mentor,30);}}
  i.Sessions++;Danger.People[pawn.Id].Meaning=Math.Min(1000,Danger.People[pawn.Id].Meaning+45);Remember(pawn.Id,"Institution","I took part in our "+i.Kind+".",3,0,2);Interrupt(pawn.Id,d);return true;
 }
 private void AdvanceInstitutions(Definitions d)
 {
  EnsureInstitutions();if(!Society.Enabled)return;AdvanceContainment(d);long day=Tick/TicksPerDay;if(day==Society.LastRitualDay)return;Society.LastRitualDay=day;
  if(day>0&&day%30==0)foreach(var p in Colony.People.Values.Where(p=>Body(p.EntityId) is {Dead:false}))p.Age=Math.Min(200,p.Age+1);
  foreach(var id in Colony.People.Keys.Where(id=>CanAct(id)).ToArray())
  {bool respected=Society.Covenant switch{"Open archive"=>Society.Institutions.Any(i=>i.Kind=="Library"&&i.Members.Contains(id)),"Mutual care"=>Entities.Any(e=>Danger.MedicalBeds.Contains(e.Id)),_=>Society.Containments.Any(c=>!c.Escaped&&c.Stability>=60)};Remember(id,"Covenant",respected?"Our covenant is practiced.":"Our covenant has no living practice.",respected?4:-3,0,2);if(respected&&Society.Covenant=="Mutual care")Colony.People[id].Priorities[2]=1;}
  foreach(var f in Society.Families){if(Body(f.First) is {Dead:true}||Body(f.Second) is {Dead:true}){long survivor=Body(f.First) is {Dead:false}?f.First:f.Second;if(Body(survivor) is {Dead:false})Remember(survivor,"Bereavement","A member of my family has died.",-8,0,7);continue;}if(Find(f.First) is {} first&&Find(f.Second) is {} second&&!IsAway(first.Id)&&!IsAway(second.Id)&&first.Z==second.Z&&Distance(first,second)<=8){Danger.People[first.Id].Company=Math.Min(1000,Danger.People[first.Id].Company+100);Danger.People[second.Id].Company=Math.Min(1000,Danger.People[second.Id].Company+100);}}
 }
}
public static class InstitutionsWorldGenerator {public static World Create(Definitions d,int seed){var w=LivingWorldGenerator.Create(d,seed);w.EnableInstitutions();return w;}}
