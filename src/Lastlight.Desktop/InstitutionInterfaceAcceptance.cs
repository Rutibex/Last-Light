using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private void RunInstitutionInterfaceChecks()
 {
  void Require(bool v,string m){if(!v)throw new InvalidDataException("Institutions UI: "+m);}void Click(string name){var h=hits.LastOrDefault(h=>h.Text==name&&h.Action!=null);Require(h!=null,"Missing "+name);h!.Action!();}
  switch(frame)
  {
   case 2:Click("Assign");break;
   case 4:Require(world.Society.Enabled,"Institutions factory missing");world.Colony.People[SocietyActor()].Skills[15]=18;Click("Found institution");Require(world.Society.Institutions.Count==1,"Found callback failed");CycleSocietyActor(true);CycleSocietyActor(true);world.Colony.People[institutionOther].Skills[15]=1;break;
   case 6:Click("Join other");Click("Apprenticeship");Click("Form partner bond");Require(world.Society.Apprentices.Count==1&&world.Society.Families.Count==1,"Membership/family callbacks failed");Click("Containment");break;
   case 8:Click("Contain downed guardian");Require(world.Society.Containments.Count==0,"Absent specimen allowed capture");Click("History");break;
   case 10:Click("Mutual care");Require(world.Society.Covenant=="Mutual care","Covenant callback failed");Click("Retire and found successor");Require(world.Society.Legacy!=null&&world.Region.Sites.Count==14,"Retirement callback failed");break;
   case 12:Click("Locate retired settlement");Require(tab=="World"&&regionalSite==world.Society.Legacy!.SiteId,"Legacy navigation failed");Click("History");break;
   case 14:foreach(var h in hits)Require(h.Bounds.Left>=0&&h.Bounds.Top>=0&&h.Bounds.Right<=UiWidth&&h.Bounds.Bottom<=UiHeight,"Control exceeds scaled bounds: "+h.Text);string file=Path.Combine(SaveStore.DefaultDirectory,"institutions-ui.lastlight");SaveStore.Write(file,world,CurrentView(),definitions);Require(SaveStore.Fingerprint(world)==SaveStore.Fingerprint(SaveStore.Read(file,definitions).World),"Legacy UI roundtrip differs");if(screenshot!=null)CaptureImage(screenshot);Console.WriteLine("STAGE H UI PASS: institution founding/membership, apprenticeship, adult family, invalid containment, covenant adoption, archived successor transition, legacy destination, scaled bounds and persistence.");Exit();break;
  }
 }
}
