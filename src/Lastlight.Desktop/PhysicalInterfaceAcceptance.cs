using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
 private void RunPhysicalInterfaceChecks()
 {
  void Require(bool v,string m){if(!v)throw new InvalidDataException("Physical UI: "+m);}void Click(string name){var h=hits.LastOrDefault(h=>h.Text==name&&h.Action!=null);Require(h!=null,"Missing "+name);h!.Action!();}
  switch(frame)
  {
   case 2:Click("Engineering");break;
   case 4:Require(world.Physical.Enabled,"New physical scenario disabled");Click("Climate");break;
   case 6:Click("Networks");break;
   case 8:Click("Disable");Require(!world.Physical.Devices[engineeringDevice].Enabled,"Device toggle failed");break;
   case 10:Click("Enable");Click("Repair");Require(!world.Physical.Devices[engineeringDevice].Fault,"Repair broke a healthy device");Click("Industry");break;
   case 12:Click("Husbandry");break;
   case 14:Click("tame");Require(world.Physical.Animals[engineeringAnimal].Request=="tame","Taming request failed");break;
   case 16:Click("Overlay: Water");Require(physicalOverlay=="Heat","Overlay cycle failed");break;
   case 18:foreach(var h in hits)Require(h.Bounds.Right<=UiWidth&&h.Bounds.Bottom<=UiHeight&&h.Bounds.Left>=0&&h.Bounds.Top>=0,"Control outside scaled viewport: "+h.Text);string file=Path.Combine(SaveStore.DefaultDirectory,"physical-ui.lastlight");SaveStore.Write(file,world,CurrentView(),definitions);Require(SaveStore.Fingerprint(world)==SaveStore.Fingerprint(SaveStore.Read(file,definitions).World),"Engineering save differs");if(screenshot!=null)CaptureImage(screenshot);Console.WriteLine("STAGE F UI PASS: engineering pages, device toggles, husbandry requests, overlays, scaled bounds and persistence.");Exit();break;
  }
 }
}
