using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private void RunRegionInterfaceChecks()
 {
  void Require(bool v,string m){if(!v)throw new InvalidDataException("Region UI: "+m);}void Click(string name){var h=hits.LastOrDefault(h=>h.Text==name&&h.Action!=null);Require(h!=null,"Missing "+name);h!.Action!();}
  switch(frame)
  {
   case 2:Click("World");break;
   case 4:Require(world.Region.Enabled&&world.Region.Sites.Count==13,"Generated world not visible");Click("Next destination");Click("Factions");break;
   case 6:Click("Sign agreement");Require(world.Region.Treaties.Count==0,"Treaty signed without an envoy at counter");Click("Quests");break;
   case 8:Click("Accept contract");Require(world.Region.Quests.First().Status=="Accepted","Quest callback failed");break;
   case 10:Click("Select destination");Require(tab=="World","Quest destination navigation failed");break;
   case 12:Click("Provision and depart");Require(world.Region.Parties.Count==0,"Empty pantry permitted departure");Click("History");break;
   case 14:Require(hits.Any(h=>h.Text=="Inspect"),"Linked chronicle controls missing");break;
   case 16:foreach(var h in hits)Require(h.Bounds.Left>=0&&h.Bounds.Top>=0&&h.Bounds.Right<=UiWidth&&h.Bounds.Bottom<=UiHeight,"Control exceeds scaled bounds: "+h.Text);string file=Path.Combine(SaveStore.DefaultDirectory,"region-ui.lastlight");SaveStore.Write(file,world,CurrentView(),definitions);Require(SaveStore.Fingerprint(world)==SaveStore.Fingerprint(SaveStore.Read(file,definitions).World),"Regional UI save differs");if(screenshot!=null)CaptureImage(screenshot);Console.WriteLine("STAGE G UI PASS: regional map, destination selection, counter/supply validation, contract acceptance, linked chronicle, scaled bounds and persistence.");Exit();break;
  }
 }
}
