using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private void RunColonyInterfaceChecks()
    {
        void Click(string name)
        {var h=hits.LastOrDefault(h=>h.Text==name && h.Action!=null);if(h==null)throw new InvalidDataException("Missing colony control: "+name);h.Action!();}
        var bill=world.Autonomy.Bills.First(b=>b.BenchId==selectedId);
        if(frame==2)Click("Until X stock");
        if(frame==4){if(!bill.CountMode)throw new InvalidDataException("Bill count mode failed.");Click("Do X times");}
        if(frame==6){if(!bill.Repeat)throw new InvalidDataException("Bill forever mode failed.");Click("Forever");}
        if(frame==8)Click("Suspend");
        if(frame==10){if(!bill.Suspended)throw new InvalidDataException("Bill suspend failed.");Click("Resume");}
        if(frame==12)Click("Worker: anyone");
        if(frame==14){if(bill.WorkerId==0)throw new InvalidDataException("Bill worker restriction failed.");Click("Ingredient radius: 256");}
        if(frame==16){if(bill.IngredientRadius!=10)throw new InvalidDataException("Ingredient radius failed.");Click("Copy");}
        if(frame==18)
        {
            if(world.Autonomy.Bills.Count(b=>b.BenchId==selectedId)!=2 || !world.Autonomy.Bills.Last().Suspended)throw new InvalidDataException("Independent suspended bill copy failed.");
            string path=Path.Combine(SaveStore.DefaultDirectory,"stage-c-bills-ui.lastlight");SaveStore.Write(path,world,CurrentView(),definitions);
            string fingerprint=SaveStore.Fingerprint(world);Restore(SaveStore.Read(path,definitions));
            if(tab!="Bills")throw new InvalidDataException("Bill panel was not restored.");
            if(fingerprint!=SaveStore.Fingerprint(world))throw new InvalidDataException("Bill UI persistence failed.");
        }
        if(frame==20)
        {
            if(hits.Where(h=>h.Action!=null).Any(h=>h.Bounds.Right>UiWidth || h.Bounds.Bottom>UiHeight))throw new InvalidDataException("Bill control is off-screen.");
            if(screenshot!=null)CaptureImage(screenshot);
            Console.WriteLine("STAGE C UI PASS: count/stock/forever, suspend, worker, radius, copy, persistence and control bounds.");Exit();
        }
    }
}
