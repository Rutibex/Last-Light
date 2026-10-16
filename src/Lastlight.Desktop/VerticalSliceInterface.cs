using Lastlight.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private long sliceActor;
    private long SliceActor => world.Colony.People.ContainsKey(selectedId)?selectedId:world.Colony.People.ContainsKey(sliceActor)?sliceActor:world.Colony.People.Keys.First();
    private SoundEffect? sliceChime, sliceAlarm, sliceMagic, ambientSound;
    private SoundEffectInstance? sliceAmbient;
    private long audioNotice;
    private PhysicalNotice? lastPhysicalAudioNotice;
    private void LoadSliceAudio()
    {
        try {var path=Path.Combine(AppContext.BaseDirectory,"Assets");using var discovery=File.OpenRead(Path.Combine(path,"discovery.wav"));sliceChime=SoundEffect.FromStream(discovery);using var stream=File.OpenRead(Path.Combine(path,"ambient.wav"));ambientSound=SoundEffect.FromStream(stream);sliceAmbient=ambientSound.CreateInstance();using var alarm=File.OpenRead(Path.Combine(path,"danger.wav"));sliceAlarm=SoundEffect.FromStream(alarm);using var magic=File.OpenRead(Path.Combine(path,"formula.wav"));sliceMagic=SoundEffect.FromStream(magic);sliceAmbient.IsLooped=true;sliceAmbient.Volume=.12f;if(world.Slice.MusicEnabled && !smoke)sliceAmbient.Play();}
        catch(Exception ex) when(ex is IOException or NoAudioHardwareException){status="Audio unavailable; colony simulation ready.";Console.WriteLine("AUDIO FALLBACK: "+ex);}
    }
    private void UpdateSliceAudio()
    {
        var physicalNotice=world.Physical.Notices.LastOrDefault();if(lastPhysicalAudioNotice!=null && physicalNotice!=null && !ReferenceEquals(lastPhysicalAudioNotice,physicalNotice) && world.Slice.SoundEnabled && !smoke)(physicalNotice.Kind is "Fire" or "Outage" or "Flood" or "Harvest"?sliceAlarm:sliceChime)?.Play(.3f,0,0);lastPhysicalAudioNotice=physicalNotice;
        if(sliceAmbient!=null){sliceAmbient.Volume=world.Slice.Hostiles.Count>0?.2f:.12f;if(!world.Slice.MusicEnabled || smoke)sliceAmbient.Pause();else if(sliceAmbient.State!=SoundState.Playing)sliceAmbient.Resume();}
        long latest=world.Slice.Notices.LastOrDefault()?.Id??0;if(latest>audioNotice && audioNotice>0 && world.Slice.SoundEnabled && !smoke){var kind=world.Slice.Notices.Last().Kind;(kind is "Threat" or "Collapse" or "Miscast" or "Feedback"?sliceAlarm:kind is "Cast" or "Prepared"?sliceMagic:sliceChime)?.Play(.3f,0,0);}audioNotice=latest;
    }
    private void DisposeSliceAudio(){sliceAmbient?.Dispose();ambientSound?.Dispose();sliceChime?.Dispose();sliceAlarm?.Dispose();sliceMagic?.Dispose();}
    private void DrawSlicePanel(Rectangle p)
    {
        int x=p.X+16,y=p.Y+48;long actor=SliceActor;sliceActor=actor;var resident=world.Find(actor)!;
        var ids=world.Colony.People.Keys.Order().ToArray();
        Button(new(x,y,230,27),"Resident: "+resident.Name,()=>{sliceActor=ids[(Array.IndexOf(ids,actor)+1)%ids.Length];selection.Clear();SelectEntity(sliceActor);},scale:.65f);
        y+=38;
        if(!world.Slice.Enabled){Paragraph("This earlier settlement retains its original map. Start a new game to explore the three-elevation observatory.",new(x,y,p.Width-32,80),Muted,.8f);return;}
        if(tab=="Levels")
        {
            for(int z=0;z<world.Levels;z++){int target=z;Button(new(x,y,240,30),world.LevelName(z),()=>{level=target;RebuildMinimap();},level==z,scale:.75f);y+=38;}
            Paragraph("Stairs connect matching cells on adjacent elevations. The field tablet releases the vault stairs; the archive releases the deep stairs. Bridge the three-tile vault rift with timber. Natural rock, walls and columns support roofs within four tiles; bridge spans need an anchor within three tiles. Unsupported structures warn for two game hours before collapse. Clear debris to rescue buried residents.",new(x+260,p.Y+95,p.Width-292,150),Text,.72f);
            Label("Unstable: "+world.Slice.UnstableSince.Count+" | Collapses: "+world.Slice.Collapses,x,y+12,Gold,.75f);
        }
        else if(tab=="Formulae")
        {
            var profile=world.Slice.Formulae[actor];
            Label($"Memory {world.MemoryUsed(actor,definitions)}/{profile.Capacity} | strain {profile.Strain:0} | failure {world.CastFailure(actor):P0}",x+245,p.Y+55,Cyan,.67f);
            foreach(var f in definitions.Document.Formulae)
            {
                bool known=profile.Known.Contains(f.Id);string id=f.Id;
                Label(Fit(f.Name,p.Width-300,.66f),x,y,known?Text:Muted,.66f);
                Label($"{(known?"Known":"Unlearned")} | memory {f.MemoryCost} | prepared {profile.Prepared.Count(i=>i.FormulaId==id)} | "+string.Join(", ",f.Reagents.Select(k=>k.Value+" "+k.Key))+ $" | risk {Math.Clamp(.06+(profile.Strain+f.Strain)/350-profile.Skill*.003,.02,.4):P0}",x,y+21,Muted,.56f);
                Button(new(p.Right-255,y,105,27),profile.Queue.Contains(id)?"Cancel queue":"Prepare",()=>Execute(new(CommandKind.PrepareFormula,actor,id,Text:profile.Queue.Contains(id)?"cancel":"")),hint:"Study desk, Research work and physical reagents required.",scale:.57f);
                Button(new(p.Right-140,y,105,27),"Cast",()=>{sliceActor=actor;SelectEntity(actor);tool="cast:"+id;tab="";status="Draft the caster, then click a target within range. Preparation is spent at channel start.";},hint:"Draft, choose Cast, then click a same-elevation target.",scale:.65f);y+=58;
            }
            Label("Light / doorway seal / living wound support / bounded item hauling. Interruptions spend the preparation.",x,p.Bottom-47,Gold,.58f);
        }
        else if(tab=="Ruin")
        {
            var r=world.Slice.Ruin;
            (string Label,string Action,long Id)[] objects=[("Surface field tablet","survey",r.TabletId),("Keeper's archive","archive",r.FolioId),("Deep cache","cache",r.CacheId),("Release machine hatch","open",r.DoorId),("Inspect daylight engine","engine",r.EngineId),("Restore engine (lens, 12 brass, 10 blocks)","repair",r.EngineId)];
            foreach(var o in objects){var item=o;Label(item.Label,x,y+5,Text,.65f);Button(new(p.Right-245,y,95,26),"Locate",()=>{Focus(item.Id);sliceActor=actor;},scale:.62f);Button(new(p.Right-140,y,105,26),"Fieldwork",()=>Execute(new(CommandKind.SliceAction,actor,TargetId:item.Id,Text:item.Action)),hint:"Undrafted worker follows reachable stairs and collects repair materials.",scale:.62f);y+=33;}
            Label($"Knowledge: {r.EngineKnowledge} | light engine {(r.EngineRestored?"restored":"dormant")} | ward {(r.DefenseComplete?"defeated":r.WardWarning?"awakening":"dormant")}",x,y+10,Gold,.64f);
        }
        else
        {
            string[] hints=["Keep the stove supplied and grow surface food.","Survey the surface field tablet using Ruin > Fieldwork.","Build three bridges across the vault's central rift.","Read the vault folio to learn all four formulae.","Queue a formula at a study desk using Formulae.","Recover the memory-glass lens from the deep cache.","Release the hatch, inspect the engine, then restore it.","Draft a prepared caster and cast on an eligible target.","Prepare weapons before restoration; defend against the warder.","Create a grain growing zone in the illuminated deep garden."];
            for(int i=0;i<World.TutorialSteps.Length;i++){string step=World.TutorialSteps[i].Id;Label((world.Slice.Tutorial.Completed.Contains(step)?"[done] ":"[    ] ")+hints[i],x,y+i*23,world.Slice.Tutorial.Completed.Contains(step)?Cyan:Text,.58f);}
            Button(new(p.Right-240,p.Y+49,100,27),"Music "+(world.Slice.MusicEnabled?"on":"off"),()=>Execute(new(CommandKind.TutorialAction,Quantity:world.Slice.MusicEnabled?0:1,Text:"music")),scale:.6f);
            Button(new(p.Right-130,p.Y+49,100,27),"Sound "+(world.Slice.SoundEnabled?"on":"off"),()=>Execute(new(CommandKind.TutorialAction,Quantity:world.Slice.SoundEnabled?0:1,Text:"sound")),scale:.6f);
        }
    }
    private void DrawSliceOverlay(int x,int y)
    {
        int cell=world.Index(x,y,level);var box=new Rectangle(x*32,y*32,32,32);
        if(world.Slice.UnstableSince.ContainsKey(cell))Outline(box,Red,3);
        if(world.Slice.Debris.Contains(cell)){Fill(new(x*32+5,y*32+8,17,13),Muted);Outline(box,Red,2);}
        if(world.Slice.Roofs.Contains(cell))Fill(new(x*32+2,y*32+2,5,2),Muted);
        foreach(var link in world.Slice.Links.Where(l=>l.A==cell || l.B==cell)){Outline(new(x*32+7,y*32+7,18,18),link.Enabled?Cyan:Gold,2);for(int i=0;i<3;i++)Fill(new(x*32+9,y*32+10+i*5,14,2),link.Enabled?Cyan:Gold);}
        foreach(var effect in world.Slice.Effects.Where(e=>e.Cell==cell)){Outline(new(x*32+3,y*32+3,26,26),effect.Kind=="Light"?Gold:Cyan,2);}
    }
}
