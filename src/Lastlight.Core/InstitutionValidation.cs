using System.Text.Json;
namespace Lastlight.Core;
public sealed partial class World
{
 public void ValidateInstitutions(Definitions d)
 {
  var s=Society;if(s==null||!s.Initialized||s.NextId<1||s.LastRitualDay< -1||s.LastRitualDay>Tick/TicksPerDay||!Covenants.Contains(s.Covenant)||s.Institutions==null||s.Families==null||s.Apprentices==null||s.Containments==null||s.Institutions.Count>24||s.Families.Count>400||s.Apprentices.Count>400||s.Containments.Count>32)throw new InvalidDataException("Invalid institutions state.");
  bool Person(long id)=>Colony.People.ContainsKey(id);var ids=new HashSet<long>();
  foreach(var i in s.Institutions)if(i==null||i.Id<1||i.Id>=s.NextId||!ids.Add(i.Id)||!InstitutionKinds.Contains(i.Kind)||Find(i.HostId)==null||!Person(i.KeeperId)||i.Members==null||i.Members.Distinct().Count()!=i.Members.Count||i.Members.Any(id=>!Person(id))||i.Sessions<0||i.Learning==null||i.Learning.Any(k=>!i.Members.Contains(k.Key)||k.Value is <0 or >3)||i.Status==null||i.Status.Length>128)throw new InvalidDataException("Invalid institution or membership.");
  if(s.Institutions.Select(i=>i.HostId).Distinct().Count()!=s.Institutions.Count)throw new InvalidDataException("Duplicate institution host.");
  foreach(var f in s.Families)if(f==null||!Person(f.First)||!Person(f.Second)||f.First==f.Second||f.Kind is not ("Partners" or "Kin")||f.Since<0||f.Since>Tick)throw new InvalidDataException("Invalid family bond.");
  if(s.Families.Select(f=>(Math.Min(f.First,f.Second),Math.Max(f.First,f.Second))).Distinct().Count()!=s.Families.Count)throw new InvalidDataException("Duplicate family bond.");
  foreach(var a in s.Apprentices)if(a==null||!Person(a.Mentor)||!Person(a.Apprentice)||a.Mentor==a.Apprentice||!s.Institutions.Any(i=>i.Id==a.InstitutionId&&i.Members.Contains(a.Mentor)&&i.Members.Contains(a.Apprentice))||a.Lessons is <0 or >8||a.Graduated!=(a.Lessons==8))throw new InvalidDataException("Invalid apprenticeship.");
  if(s.Apprentices.Where(a=>!a.Graduated).Select(a=>a.Apprentice).Distinct().Count()!=s.Apprentices.Count(a=>!a.Graduated))throw new InvalidDataException("Duplicate apprentice placement.");
  foreach(var q in s.Containments)if(q==null||q.EntityId<1||q.EntityId>=NextEntityId||!Person(q.KeeperId)||q.Cell<0||q.Cell>=Cells.Count||q.Feed is <0 or >6||q.Stability is <0 or >100||q.Escape is <0 or >100||q.Knowledge is <0 or >10000||q.StudyRate is <0 or >3||q.LastFeedAt<0||q.LastFeedAt>Tick||q.Warning==null||q.Warning.Length>128||!q.Escaped&&(Find(q.EntityId)?.DefinitionId!="vault-guardian"||Body(q.EntityId) is not {Captured:true}))throw new InvalidDataException("Invalid containment.");
  if(s.Containments.Select(q=>q.EntityId).Distinct().Count()!=s.Containments.Count)throw new InvalidDataException("Duplicate containment.");
  if(s.Legacy is {} archive)
  {
   if(!Region.Sites.Any(site=>site.Id==archive.SiteId)||archive.RetiredAt<0||archive.RetiredAt>Tick||archive.ElapsedYears is <1 or >20||archive.Visited&&archive.Reclaimed||archive.Snapshot==null||archive.Snapshot.Length>16*1024*1024||archive.Conversion==null||archive.Conversion.Length>512||archive.EntityMap==null||archive.EntityMap.Any(k=>k.Key<1||k.Value<1||k.Value>=NextEntityId))throw new InvalidDataException("Invalid retirement archive.");
   if(!archive.Reclaimed){try{var old=JsonSerializer.Deserialize<World>(archive.Snapshot,JsonFormat.Options)??throw new InvalidDataException("Empty legacy snapshot.");if(old.Society==null||old.Society.Legacy!=null||old.Width!=Width||old.Height!=Height||old.Tick!=archive.RetiredAt)throw new InvalidDataException("Invalid nested legacy snapshot.");old.EnsureSolar();old.Validate(d);}catch(JsonException ex){throw new InvalidDataException("Invalid legacy JSON.",ex);}}
  }
 }
}
