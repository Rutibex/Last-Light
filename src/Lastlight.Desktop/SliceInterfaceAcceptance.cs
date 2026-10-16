using Lastlight.Core;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private void RunSliceInterfaceChecks()
    {
        void Require(bool v,string m){if(!v)throw new InvalidDataException("Slice UI: "+m);}
        void Click(string name){var h=hits.LastOrDefault(h=>h.Text==name && h.Action!=null);Require(h!=null,"Missing "+name);h!.Action!();}
        switch(frame)
        {
            case 2: Click("Levels");break;
            case 4:Click(world.LevelName(1));Require(level==1,"Level callback failed");break;
            case 6:Click(world.LevelName(2));Require(level==2,"Deep callback failed");Click("Ruin");break;
            case 8:Require(hits.Count(h=>h.Text=="Fieldwork")==6,"Ruin controls missing");Click("Fieldwork");Require(!world.Slice.Ruin.EngineRestored,"Invalid restoration accepted");Click("Formulae");break;
            case 10:Require(hits.Count(h=>h.Text=="Prepare")==4 && hits.Count(h=>h.Text=="Cast")==4,"Four formula controls missing");Click("Prepare");Require(world.Slice.Formulae.Values.All(p=>p.Queue.Count==0),"Unlearned preparation accepted");Click("Tutorial");break;
            case 12:if(sliceChime!=null){sliceChime.Play(0,0,0);sliceAmbient!.Volume=0;sliceAmbient.Play();sliceAmbient.Stop();Console.WriteLine("AUDIO PASS: generated WAV assets decoded and OpenAL playback exercised.");}Click("Music on");Click("Sound on");Require(!world.Slice.MusicEnabled && !world.Slice.SoundEnabled,"Audio toggles failed");break;
            case 14:Click("Music off");Click("Sound off");Click("Z2");Require(level==0,"Level cycling failed");break;
            case 16:
                foreach(var h in hits)Require(h.Bounds.Left>=0 && h.Bounds.Right<=UiWidth && h.Bounds.Top>=0 && h.Bounds.Bottom<=UiHeight,"Control outside scaled viewport: "+h.Text);
                string file=Path.Combine(SaveStore.DefaultDirectory,"stage-e-ui.lastlight");SaveStore.Write(file,world,CurrentView(),definitions);var saved=SaveStore.Read(file,definitions);Require(SaveStore.Fingerprint(world)==SaveStore.Fingerprint(saved.World) && saved.View.OpenTab=="Tutorial","UI save differs");
                if(screenshot!=null)CaptureImage(screenshot);Console.WriteLine("STAGE E UI PASS: level navigation, ruin/preparation validation, four spell controls, audio toggles, scaled bounds and save/reload");Exit();break;
        }
    }
}
