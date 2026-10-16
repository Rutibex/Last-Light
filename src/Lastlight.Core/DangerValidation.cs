namespace Lastlight.Core;
public sealed partial class World
{
    public void ValidateDanger(Definitions d)
    {
        if(Danger==null || !Danger.Initialized || Danger.Bodies==null || Danger.People==null || Danger.Orders==null || Danger.MedicalBeds==null || Danger.History==null || Danger.Traces==null || Danger.Raid==null)throw new InvalidDataException("Missing people/danger state.");
        bool Finite(double value,double min,double max)=>double.IsFinite(value) && value>=min && value<=max;
        if(Danger.NextWoundId<1 || Danger.PauseSerial<0 || Danger.NextRaidAt<0 || Danger.History.Count>256 || Danger.Traces.Count>128 || Danger.RaidsCompleted<0 || Danger.Treatments<0 || Danger.Rescues<0 || Danger.Deaths<0)throw new InvalidDataException("Invalid danger counters or history limits.");
        var woundIds=new HashSet<long>();
        foreach(var pair in Danger.Bodies)
        {
            var b=pair.Value;var entity=Find(pair.Key);
            if(b==null || entity==null || b.EntityId!=pair.Key || d.Entities[entity.DefinitionId].Kind is not (EntityKind.Person or EntityKind.Creature) || b.AnatomyId!=(entity.DefinitionId=="grazer"?"grazer":"human") || b.Wounds==null || b.Scars==null || b.Wounds.Count>256 || !Finite(b.Blood,0,100) || b.WeaponCondition is <0 or >100 || b.ArmorCondition is <0 or >100 || b.Ammo is <0 or >30 || b.WeaponId!="unarmed" && !d.Document.Weapons.Any(w=>w.Id==b.WeaponId) || b.ArmorId is not ("" or "padded-armor") || !Finite(b.DiagnosticConfidence,0,1) || b.MagicBleedStopUntil<0 || b.RepairBoostUntil<0 || b.NextMoveAt<0 || b.NextAttackAt<0 || b.CarriedBy!=0 && (!Colony.People.ContainsKey(b.CarriedBy) || b.CarriedBy==b.EntityId) || b.Dead && entity.Health!=0)throw new InvalidDataException("Invalid body, equipment or carrier.");
            var parts=Anatomy.Parts(b.AnatomyId).Select(p=>p.Id).ToHashSet();
            if(b.Scars.Any(s=>!parts.Contains(s.Key) || !Finite(s.Value,0,1000)))throw new InvalidDataException("Invalid body scar.");
            foreach(var w in b.Wounds)
                if(w==null || w.Id<1 || w.Id>=Danger.NextWoundId || !woundIds.Add(w.Id) || !parts.Contains(w.Part) || w.Kind is not ("Cut" or "Bruise" or "Fracture" or "Burn" or "Poison") || !Finite(w.Severity,0,100) || !Finite(w.OriginalSeverity,.001,100) || w.Severity>w.OriginalSeverity || !Finite(w.TendQuality,0,1) || !Finite(w.Infection,0,100) || !Finite(w.Immunity,0,100) || w.LastTendedAt<0 || w.LastTendedAt>Tick || w.CreatedAt<0 || w.CreatedAt>Tick || w.SourceId<0 || w.SourceId>=NextEntityId || w.DoctorId<0 || w.DoctorId>=NextEntityId)throw new InvalidDataException("Invalid wound or treatment.");
        }
        if(Entities.Any(e=>d.Entities[e.DefinitionId].Kind is EntityKind.Person or EntityKind.Creature && !Danger.Bodies.ContainsKey(e.Id)))throw new InvalidDataException("Missing anatomy state.");
        foreach(var pair in Danger.People)
        {
            var p=pair.Value;
            if(p==null || p.EntityId!=pair.Key || !Colony.People.ContainsKey(pair.Key) || p.Memories==null || p.Relationships==null || p.Memories.Count>32 || !Finite(p.Comfort,0,1000) || !Finite(p.Privacy,0,1000) || !Finite(p.Company,0,1000) || !Finite(p.Meaning,0,1000) || p.StressTicks<0 || p.WithdrawalUntil<0 || p.LastSocialAt<0 || p.LastSocialAt>Tick)throw new InvalidDataException("Invalid personality state.");
            foreach(var m in p.Memories)if(m==null || m.Tick<0 || m.Tick>Tick || m.Value is <-100 or >100 || m.DurationDays is <1 or >30 || m.Cell<0 || m.Cell>=Cells.Count || m.Text==null || m.Text.Length>512 || m.Kind==null || m.OtherId<0 || m.OtherId>=NextEntityId)throw new InvalidDataException("Invalid memory.");
            foreach(var relation in p.Relationships)if(relation.Value==null || relation.Key==pair.Key || !Colony.People.ContainsKey(relation.Key) || relation.Value.OtherId!=relation.Key || relation.Value.Opinion is <-100 or >100 || relation.Value.Interactions<0 || relation.Value.LastInteractionAt<0 || relation.Value.LastInteractionAt>Tick)throw new InvalidDataException("Invalid relationship.");
        }
        if(Danger.People.Count!=Colony.People.Count)throw new InvalidDataException("Missing personality.");
        foreach(var pair in Danger.Orders)if(!Danger.Bodies.ContainsKey(pair.Key) || pair.Value==null || pair.Value.Kind is not ("Attack" or "Hold" or "Retreat") || pair.Value.Path==null || pair.Value.Path.Any(i=>i<0 || i>=Cells.Count) || pair.Value.TargetId<0 || pair.Value.TargetId>=NextEntityId || pair.Value.RetryAt<0)throw new InvalidDataException("Invalid combat order.");
        if(Danger.MedicalBeds.Any(id=>Find(id)?.DefinitionId!="bed"))throw new InvalidDataException("Missing medical bed.");
        var r=Danger.Raid;
        if(r.Phase is not ("Idle" or "Warning" or "Active" or "Recovery") || r.RaiderIds==null || r.Count is <0 or >6 || r.RaiderIds.Count>r.Count || r.RaiderIds.Distinct().Count()!=r.RaiderIds.Count || r.RaiderIds.Any(id=>id<1 || id>=NextEntityId) || r.SpawnCell is <-1 || r.SpawnCell>=Cells.Count || r.Phase is "Warning" or "Active" && r.SpawnCell<0 || r.WarningUntil<0 || r.StartedAt<0 || r.StartedAt>Tick || r.RecoveryUntil<0 || r.StolenFood<0 || r.Escaped<0)throw new InvalidDataException("Invalid raid state.");
        if(Danger.History.Any(e=>e==null || e.Tick<0 || e.Tick>Tick || e.Text==null || e.Text.Length>512) || Danger.Traces.Any(t=>t==null || t.Tick<0 || t.Tick>Tick || t.FromCell<0 || t.FromCell>=Cells.Count || t.ToCell<0 || t.ToCell>=Cells.Count))throw new InvalidDataException("Invalid danger history/trace.");
    }
}
