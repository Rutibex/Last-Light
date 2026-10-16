using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private long dangerCheckBed;
    private void RunDangerInterfaceChecks()
    {
        void Require(bool condition,string message){if(!condition)throw new InvalidDataException("Danger UI: "+message);}
        void Click(string text){var h=hits.LastOrDefault(h=>h.Text==text && h.Action!=null);Require(h!=null,"Missing control: "+text);h!.Action!();}
        long resident=world.Colony.People.Keys.First();
        switch(frame)
        {
            case 2:Click("Danger pause: ON");Require(!world.Danger.AutoPause,"Auto pause setting failed.");break;
            case 4:Click("Danger pause: OFF");Click("Scheduled raids: OFF");Require(world.Danger.RaidsEnabled,"Scheduled raids did not enable.");break;
            case 6:Click("Scheduled raids: ON");SelectEntity(resident);tab="";inspectTab="Health";break;
            case 8:Click("Medical details");Require(tab=="Medical","Health inspector did not open anatomy.");break;
            case 10:Click("X");inspectTab="Social";break;
            case 12:Click("Memories and relationships");Require(tab=="People","Social inspector did not open relationships.");break;
            case 14:
                var path=Path.Combine(SaveStore.DefaultDirectory,"stage-d-ui.lastlight");SaveStore.Write(path,world,CurrentView(),definitions);string before=SaveStore.Fingerprint(world);Restore(SaveStore.Read(path,definitions));
                Require(tab=="People" && before==SaveStore.Fingerprint(world),"People panel state did not persist.");break;
            case 16:
                Click("X");var bed=world.Entities.First(e=>e.DefinitionId=="bed");dangerCheckBed=bed.Id;SelectEntity(bed.Id);break;
            case 18:Click(world.Danger.MedicalBeds.Contains(dangerCheckBed)?"Medical bed: YES":"Medical bed: NO");Require(world.Danger.MedicalBeds.Contains(dangerCheckBed),"Medical bed designation failed.");SelectEntity(resident);inspectTab="Gear";break;
            case 20:Click("Equipment pickup");Require(tab=="Gear","Gear inspector did not open pickup panel.");break;
            case 22:Click("Load ammunition");Require(world.Autonomy.Jobs.Any(j=>j.PawnId==resident && j.Kind=="Equip"),"Actual gear callback did not reserve pickup.");break;
            case 24:Click("Danger");break;
            case 26:
                world.Validate(definitions);
                Require(hits.Where(h=>h.Action!=null).All(h=>h.Bounds.Right<=UiWidth && h.Bounds.Bottom<=UiHeight),"Danger controls extend off-screen.");
                if(screenshot!=null)CaptureImage(screenshot);
                Console.WriteLine("STAGE D UI PASS: danger settings, health/anatomy, relationships, medical bed, real gear job, persistence and bounds.");Exit();break;
        }
    }
}
