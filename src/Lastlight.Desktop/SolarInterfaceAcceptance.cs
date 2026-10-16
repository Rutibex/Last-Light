using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private void RunSolarInterfaceChecks()
 {
  void Require(bool v,string m){if(!v)throw new InvalidDataException("Solar UI: "+m);}void Click(string name){var h=hits.LastOrDefault(h=>h.Text==name&&h.Action!=null);Require(h!=null,"Missing "+name);h!.Action!();}
  if(frame>=2)foreach(var hit in hits)Require(hit.Bounds.Left>=0&&hit.Bounds.Top>=0&&hit.Bounds.Right<=UiWidth&&hit.Bounds.Bottom<=UiHeight,"Scaled bounds: "+hit.Text);
  switch(frame)
  {
   case 2:Require(world.Solar.Enabled&&tab=="World","Solar campaign not visible");Click("Next mode");Click("Apply campaign mode");Require(world.Solar.Mode=="Slow Dusk"&&world.Solar.NextBoundary==World.TicksPerDay*12,"Mode/rules callback failed");break;
   case 4:Click("Projects");break;
   case 6:Click("Commission project");Require(world.Solar.Projects.All(p=>!p.Active),"Unrestored engine bypassed finale prerequisites");Click("Politics");break;
   case 8:Click("Accept scholarly patron");Require(world.Solar.Patrons.Count==0,"Remote/phantom patron aid accepted");Click("Director");break;
   case 10:Click("The Chronicler");Require(world.Solar.Director=="The Capricious Star","Director callback failed");Click("Raids: 35");Require(world.Solar.RaidIntensity==60,"Independent setting callback failed");break;
   case 12:Click("Regional map");Require(!solarWorldPane,"Map toggle failed");break;
   case 14:Click("Solar campaign");break;
   case 15:Click("Observations");break;
   case 16:string file=Path.Combine(SaveStore.DefaultDirectory,"solar-ui.lastlight");SaveStore.Write(file,world,CurrentView(),definitions);var loaded=SaveStore.Read(file,definitions);Require(SaveStore.Fingerprint(world)==SaveStore.Fingerprint(loaded.World)&&loaded.View.SolarWorldPane&&loaded.View.SolarPage=="Observations","Campaign/view save differs");if(screenshot!=null)CaptureImage(screenshot);Console.WriteLine("STAGE I UI PASS: explicit modes/rules, finale prerequisite rejection, no remote patron aid, director/independent settings, regional map toggle, campaign view persistence and scaled bounds.");Exit();break;
  }
 }
}
