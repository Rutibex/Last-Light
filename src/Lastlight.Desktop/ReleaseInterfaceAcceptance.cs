using Lastlight.Core;
using System.Text.Json;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private void FocusControl(int delta)
    {
        var available=hits.Where(h=>h.Action!=null&&!h.Paint).ToList();
        if(available.Count==0)return;
        keyboardFocus=keyboardFocus<0 ? (delta<0 ? available.Count-1 : 0) : (keyboardFocus+delta+available.Count)%available.Count;
        keyboardFocusText=available[keyboardFocus].Text;
        status="Keyboard: "+keyboardFocusText+". Enter activates; Ctrl+arrows changes focus.";
    }
    private bool ActivateFocusedControl()
    {
        var available=hits.Where(h=>h.Action!=null&&!h.Paint).ToList();
        if(keyboardFocus<0||keyboardFocus>=available.Count||available[keyboardFocus].Text!=keyboardFocusText)return false;
        var action=available[keyboardFocus].Action;keyboardFocus=-1;keyboardFocusText="";action?.Invoke();return true;
    }
    private void RunReleaseInterfaceChecks()
    {
        void Require(bool value,string message){if(!value)throw new InvalidDataException("Release UI: "+message);}
        if(frame>=3)foreach(var hit in hits)Require(hit.Bounds.Left>=0&&hit.Bounds.Top>=0&&hit.Bounds.Right<=UiWidth&&hit.Bounds.Bottom<=UiHeight,"Bounds: "+hit.Text);
        if(frame==2){tab="Menu";keyboardFocus=-1;keyboardFocusText="";}
        if(frame==4)
        {
            int count=hits.Count(h=>h.Action!=null&&!h.Paint);
            for(int n=0;n<count&&!keyboardFocusText.StartsWith("High contrast:",StringComparison.Ordinal);n++)FocusControl(1);
            Require(keyboardFocusText.StartsWith("High contrast:",StringComparison.Ordinal),"Keyboard cannot reach contrast control");
            Require(ActivateFocusedControl()&&settings.HighContrast,"Keyboard contrast activation failed");
        }
        if(frame==6)
        {
            settings.Validate();var restored=JsonSerializer.Deserialize<InterfaceSettings>(JsonSerializer.Serialize(settings,JsonFormat.Options),JsonFormat.Options)!;restored.Validate();
            Require(restored.HighContrast&&restored.Scale==settings.Scale&&restored.Keys.SequenceEqual(settings.Keys),"Preferences roundtrip differs");
            keyboardFocus=-1;keyboardFocusText="";FocusControl(-1);Require(keyboardFocus==hits.Count(h=>h.Action!=null&&!h.Paint)-1,"Initial reverse focus skipped the last control");
            keyboardFocus=-1;keyboardFocusText="";FocusControl(1);string before=keyboardFocusText;FocusControl(-1);FocusControl(1);Require(before==keyboardFocusText,"Reverse focus navigation differs");
            // A rebuilt page must not invoke a stale action under the same numerical index.
            keyboardFocusText="obsolete control";Require(!ActivateFocusedControl(),"Stale focus action invoked");keyboardFocus=-1;keyboardFocusText="";
        }
        if(frame==8)
        {
            if(screenshot!=null)CaptureImage(screenshot);
            Console.WriteLine($"STAGE J UI PASS: {UiWidth}x{UiHeight}, scale {settings.Scale}, native draw/hit bounds, keyboard focus/activation, stale-action guard, contrast and preference persistence.");Exit();
        }
    }
}
