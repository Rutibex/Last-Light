using Lastlight.Core;
using Microsoft.Xna.Framework;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private void DrawMedicalPanel(Rectangle panel)
    {
        var entity=world.Find(selectedId);var body=world.Body(selectedId);
        if(entity==null || body==null){Label("Select a resident or creature.",panel.X+18,panel.Y+56,Muted);return;}
        int x=panel.X+17,y=panel.Y+49;var cap=world.Capacity(entity.Id);
        Label(entity.Name+(body.Dead?" - DEAD":body.Downed?" - DOWNED":""),x,y,Cyan,.83f);
        Label($"Blood {body.Blood:0}% | Pain {cap.Pain:P0} | Bleeding {world.BleedingPerHour(entity.Id):0.0}/hour",x,y+27,Text,.68f);
        string survival=double.IsPositiveInfinity(world.BleedingHoursRemaining(entity.Id))?"No active bleeding":$"Estimated bleeding survival: {world.BleedingHoursRemaining(entity.Id):0.0} hours";
        Label(survival,x,y+49,world.BleedingPerHour(entity.Id)>0?Red:Muted,.66f);
        Label($"Move {cap.Moving:P0} | Hands {cap.Manipulation:P0} | Sight {cap.Sight:P0} | Breath {cap.Breathing:P0} | Conscious {cap.Consciousness:P0}",x,y+72,Text,.65f);
        Label($"Diagnostic confidence {body.DiagnosticConfidence:P0} | Scars {body.Scars.Count}",x,y+95,Muted,.64f);
        int leftWidth=(panel.Width-55)/2, top=y+128;
        Label("ANATOMY (damage follows connected parts)",x,top,Cyan,.68f);
        int row=top+25;
        foreach(var part in Anatomy.Parts(body.AnatomyId))
        {
            double loss=body.Wounds.Where(w=>w.Part==part.Id).Sum(w=>w.Severity)+body.Scars.GetValueOrDefault(part.Id);
            if(row>panel.Bottom-70)break;
            Label($"{part.Id,-12} {Math.Max(0,part.Capacity-loss):0.0}/{part.Capacity}",x,row,loss>0?Red:Muted,.62f);row+=14;
        }
        int rx=x+leftWidth+20;
        Label("WOUNDS - wheel scrolls",rx,top,Cyan,.68f);row=top+26;
        foreach(var w in body.Wounds.Skip(rowOffset).Take(Math.Max(1,(panel.Bottom-row-42)/48)))
        {
            Label($"{w.Part}: {w.Kind} {w.Severity:0.0}",rx,row,Red,.67f);
            Label(w.Tended?$"Tended {w.TendQuality:P0} by {world.Find(w.DoctorId)?.Name??"former resident"}":"UNTENDED - needs a doctor",rx,row+17,w.Tended?Muted:Gold,.6f);
            if(w.Infected)Label($"Infection {w.Infection:0}% | Immunity {w.Immunity:0}%",rx,row+32,Red,.59f);row+=48;
        }
        if(body.Wounds.Count==0)Label("No active wounds.",rx,top+28,Muted,.7f);
        Label("Medicine policy and Doctor priority govern autonomous tending. Infections and scars persist.",x,panel.Bottom-52,Muted,.61f);
    }
    private void DrawPeoplePanel(Rectangle panel)
    {
        var entity=world.Find(selectedId);var person=world.Danger.People.GetValueOrDefault(selectedId);
        if(entity==null || person==null){Label("Select a resident.",panel.X+18,panel.Y+56,Muted);return;}
        int x=panel.X+18,y=panel.Y+52;
        var pawn=world.Colony.People[entity.Id];Label(entity.Name+$" | Mood {pawn.Mood}/100 | {pawn.Trait}",x,y,Cyan,.82f);
        Label($"Food {pawn.Food/10:0}% | Rest {pawn.Rest/10:0}% | Recreation {pawn.Recreation/10:0}% | Company {person.Company/10:0}% | Meaning {person.Meaning/10:0}%",x,y+30,Text,.65f);
        if(person.WithdrawalUntil>world.Tick)Label($"Withdrawn for {(person.WithdrawalUntil-world.Tick)/(double)World.TicksPerHour:0.0} more hours",x,y+54,Red,.69f);
        else Label($"Comfort {person.Comfort/10:0}% | Privacy {person.Privacy/10:0}% | Pain {world.Capacity(entity.Id).Pain:P0}",x,y+54,Muted,.63f);
        int mid=panel.X+panel.Width/2;
        Label("RELATIONSHIPS",x,y+90,Gold,.7f);int row=y+119;
        foreach(var r in person.Relationships.Values.OrderByDescending(r=>r.Opinion).Take(7))
        {Label((world.Find(r.OtherId)?.Name??"Departed")+$": {r.Bond} ({r.Opinion:+0;-0;0})",x,row,Text,.68f);row+=27;}
        Label("MEMORIES - wheel scrolls",mid,y+90,Gold,.7f);row=y+119;
        foreach(var m in person.Memories.AsEnumerable().Reverse().Skip(rowOffset).Take(Math.Max(1,(panel.Bottom-row-40)/44)))
        {
            double remaining=m.DurationDays-(world.Tick-m.Tick)/(double)World.TicksPerDay;
            Label(Fit(m.Text,panel.Width/2-35,.65f),mid,row,Text,.65f);
            Label($"Mood {m.Value:+0;-0;0}, fading over {remaining:0.0} days",mid,row+18,m.Value>=0?Cyan:Red,.6f);row+=44;
        }
    }
    private void DrawGearPanel(Rectangle panel)
    {
        var entity=world.Find(selectedId);var body=world.Body(selectedId);
        if(entity==null || !world.Colony.People.ContainsKey(entity.Id) || body==null){Label("Select a resident to equip.",panel.X+18,panel.Y+54,Muted);return;}
        int x=panel.X+18,y=panel.Y+52;
        Label(entity.Name+$" | {body.WeaponId} ({body.WeaponCondition}%) | Armor {(body.ArmorId==""?"none":body.ArmorId)} | Arrows {body.Ammo}/30",x,y,Cyan,.73f);
        Label("Pickup orders claim a real item and walk to it. Gear swaps return the previous item at its current condition.",x,y+29,Muted,.65f);
        y+=70;
        foreach(var item in world.Entities.Where(e=>definitions.Document.Weapons.Any(w=>w.Id==e.DefinitionId) || e.DefinitionId is "padded-armor" or "arrows").OrderBy(e=>e.DefinitionId).ThenBy(e=>e.Id).Skip(rowOffset).Take(Math.Max(1,(panel.Height-160)/32)))
        {
            var e=item;
            Label(e.Name+$" #{e.Id} x{e.Quantity} | {e.X},{e.Y} | {e.Health}%"+(e.Forbidden?" FORBIDDEN":""),x,y,Text,.66f);
            Button(new(panel.Right-240,y-3,207,27),e.DefinitionId=="arrows"?"Load ammunition":"Equip",()=>Execute(new(CommandKind.Equip,entity.Id,TargetId:e.Id)),hint:"A reachable allowed item is required.",scale:.68f);y+=32;
        }
    }
    private void DrawDangerPanel(Rectangle panel)
    {
        var danger=world.Danger;var raid=danger.Raid;int x=panel.X+18,y=panel.Y+52;
        Label("ASH-ROAD SCAVENGERS | "+raid.Phase.ToUpperInvariant(),x,y,raid.Phase=="Active"?Red:Cyan,.9f);
        string timing=raid.Phase=="Warning"?$"Arrival in {(raid.WarningUntil-world.Tick)/(double)World.TicksPerHour:0.0} hours | Entry {raid.SpawnCell%world.Width},{raid.SpawnCell/world.Width%world.Height}":raid.Phase=="Recovery"?$"Recovery: {(raid.RecoveryUntil-world.Tick)/(double)World.TicksPerDay:0.0} days remain":$"Next scheduled warning: day {danger.NextRaidAt/World.TicksPerDay+1}";
        Label(timing,x,y+33,Text,.7f);
        Label($"Completed raids {danger.RaidsCompleted} | Wounds {danger.WoundsInflicted} | Treatments {danger.Treatments} | Rescues {danger.Rescues}",x,y+58,Muted,.66f);
        Button(new(x,y+94,240,29),danger.RaidsEnabled?"Scheduled raids: ON":"Scheduled raids: OFF",()=>Execute(new(CommandKind.DangerSettings,Quantity:danger.RaidsEnabled?0:1,Text:"raids")),danger.RaidsEnabled,scale:.67f);
        Button(new(x+249,y+94,220,29),danger.AutoPause?"Danger pause: ON":"Danger pause: OFF",()=>Execute(new(CommandKind.DangerSettings,Quantity:danger.AutoPause?0:1,Text:"pause")),danger.AutoPause,scale:.67f);
        Button(new(x+478,y+94,250,29),"Start practice raid (1)",()=>Execute(new(CommandKind.TriggerRaid,Quantity:1)),hint:"Creates a real raid after a two-hour warning; obeys the recovery window.",scale:.67f);
        Button(new(x+737,y+94,180,29),"Defenders: Hold",()=>{foreach(long id in selection.ToArray())if(world.Colony.People.TryGetValue(id,out var p) && p.Drafted)Execute(new(CommandKind.CombatOrder,id,Text:"hold"));},scale:.65f);
        y+=146;
        foreach(var hostile in world.Entities.Where(e=>e.DefinitionId=="raider").Take(4))
        {
            var e=hostile;var b=world.Body(e.Id)!;
            Button(new(x,y,275,27),e.Name,()=>Focus(e.Id),hint:"Inspect this attacker.",scale:.65f);
            Label($"{(b.Dead?"Dead":b.Downed?"Downed":raid.Retreating?"Retreating":"Hostile")} | health {e.Health} | blood {b.Blood:0}%",x+291,y+4,Text,.64f);y+=33;
        }
        Paragraph("Draft defenders; right-click an attacker to attack, or terrain to move/retreat. Undraft after combat. Doctors use real medicine; medical beds improve care. Attackers seek supplies and retreat after losses. Scheduled raids respect recovery and untreated wounds.",new(x,panel.Bottom-108,panel.Width-45,69),Muted,.66f);
    }
}
