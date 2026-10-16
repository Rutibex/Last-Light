namespace Lastlight.Core;

public sealed record BodyPartDefinition(string Id,string Parent,int Capacity);
public static class Anatomy
{
    public static readonly BodyPartDefinition[] Human =
    [new("torso","",70),new("head","torso",35),new("brain","head",25),new("left-eye","head",12),new("right-eye","head",12),new("left-lung","torso",25),new("right-lung","torso",25),new("left-arm","torso",30),new("right-arm","torso",30),new("left-hand","left-arm",15),new("right-hand","right-arm",15),new("left-leg","torso",35),new("right-leg","torso",35)];
    public static readonly BodyPartDefinition[] Grazer =
    [new("torso","",90),new("head","torso",30),new("brain","head",20),new("left-eye","head",10),new("right-eye","head",10),new("left-lung","torso",30),new("right-lung","torso",30),new("left-leg","torso",35),new("right-leg","torso",35)];
    public static BodyPartDefinition[] Parts(string kind)=>kind=="grazer"?Grazer:Human;
}
public sealed class Wound
{
    public long Id { get; set; }
    public string Part { get; set; } = "torso";
    public string Kind { get; set; } = "Cut";
    public double Severity { get; set; }
    public double OriginalSeverity { get; set; }
    public long CreatedAt { get; set; }
    public long SourceId { get; set; }
    public long LastTendedAt { get; set; }
    public bool Tended { get; set; }
    public double TendQuality { get; set; }
    public long DoctorId { get; set; }
    public double Infection { get; set; }
    public double Immunity { get; set; }
    public bool Infected { get; set; }
}
public sealed class BodyState
{
    public long EntityId { get; set; }
    public string AnatomyId { get; set; } = "human";
    public List<Wound> Wounds { get; set; } = [];
    public Dictionary<string,double> Scars { get; set; } = new();
    public double Blood { get; set; } = 100;
    public bool Downed { get; set; }
    public bool Dead { get; set; }
    public bool Captured { get; set; }
    public bool DeathRecorded { get; set; }
    public string DeathCause { get; set; } = "";
    public string WeaponId { get; set; } = "unarmed";
    public int WeaponCondition { get; set; } = 100;
    public string ArmorId { get; set; } = "";
    public int ArmorCondition { get; set; } = 100;
    public int Ammo { get; set; }
    public long MagicBleedStopUntil { get; set; }
    public long RepairBoostUntil { get; set; }
    public long NextAttackAt { get; set; }
    public long NextMoveAt { get; set; }
    public long CarriedBy { get; set; }
    public double DiagnosticConfidence { get; set; }
}
public sealed class Memory
{
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public long OtherId { get; set; }
    public long Tick { get; set; }
    public int Value { get; set; }
    public int DurationDays { get; set; } = 3;
    public int Cell { get; set; }
}
public sealed class Relationship
{
    public long OtherId { get; set; }
    public int Opinion { get; set; }
    public int Interactions { get; set; }
    public long LastInteractionAt { get; set; }
    public string Bond => Opinion>=20?"Friend":Opinion<=-20?"Rival":"Acquaintance";
}
public sealed class PersonState
{
    public long EntityId { get; set; }
    public List<Memory> Memories { get; set; } = [];
    public Dictionary<long,Relationship> Relationships { get; set; } = new();
    public double Comfort { get; set; } = 600;
    public double Privacy { get; set; } = 600;
    public double Company { get; set; } = 700;
    public double Meaning { get; set; } = 600;
    public long StressTicks { get; set; }
    public long WithdrawalUntil { get; set; }
    public long LastSocialAt { get; set; }
}
public sealed class CombatOrder
{
    public string Kind { get; set; } = "Hold";
    public long TargetId { get; set; }
    public List<int> Path { get; set; } = [];
    public long RetryAt { get; set; }
}
public sealed class RaidState
{
    public string Phase { get; set; } = "Idle";
    public long WarningUntil { get; set; }
    public long StartedAt { get; set; }
    public long RecoveryUntil { get; set; }
    public int Count { get; set; }
    public int SpawnCell { get; set; } = -1;
    public List<long> RaiderIds { get; set; } = [];
    public bool Retreating { get; set; }
    public int Escaped { get; set; }
    public int StolenFood { get; set; }
}
public sealed record DangerEvent(long Tick,string Kind,long ActorId,long OtherId,string Text);
public sealed record AttackTrace(long Tick,long ActorId,long TargetId,int FromCell,int ToCell,bool Hit);
public sealed class DangerState
{
    public bool Initialized { get; set; }
    public Dictionary<long,BodyState> Bodies { get; set; } = new();
    public Dictionary<long,PersonState> People { get; set; } = new();
    public Dictionary<long,CombatOrder> Orders { get; set; } = new();
    public HashSet<long> MedicalBeds { get; set; } = [];
    public List<DangerEvent> History { get; set; } = [];
    public List<AttackTrace> Traces { get; set; } = [];
    public RaidState Raid { get; set; } = new();
    public bool RaidsEnabled { get; set; }
    public bool AutoPause { get; set; } = true;
    public long PauseSerial { get; set; }
    public long NextRaidAt { get; set; } = World.TicksPerDay*3+World.TicksPerHour*8;
    public long NextWoundId { get; set; } = 1;
    public int RaidsCompleted { get; set; }
    public int WoundsInflicted { get; set; }
    public int Treatments { get; set; }
    public int Rescues { get; set; }
    public int Deaths { get; set; }
}
public sealed record Capacities(double Moving,double Manipulation,double Sight,double Breathing,double Consciousness,double Speech,double Pain);
public sealed partial class World
{
    public DangerState Danger { get; set; } = new();
    public void EnsureDanger(Definitions d)
    {
        foreach(var e in Entities.Where(e=>d.Entities[e.DefinitionId].Kind is EntityKind.Person or EntityKind.Creature))
        {
            if(!Danger.Bodies.ContainsKey(e.Id))
            {
                var body=new BodyState{EntityId=e.Id,AnatomyId=e.DefinitionId=="grazer"?"grazer":"human",Dead=e.Health==0};
                if(e.Health is >0 and <100)body.Wounds.Add(new(){Id=Danger.NextWoundId++,Part="torso",Kind="Bruise",Severity=(100-e.Health)*.5,OriginalSeverity=(100-e.Health)*.5,CreatedAt=Tick});
                Danger.Bodies[e.Id]=body;
            }
            if(d.Entities[e.DefinitionId].Kind==EntityKind.Person && !Danger.People.ContainsKey(e.Id))Danger.People[e.Id]=new(){EntityId=e.Id};
        }
        foreach(var person in Danger.People.Values)foreach(long other in Colony.People.Keys.Order())
            if(other!=person.EntityId && !person.Relationships.ContainsKey(other))person.Relationships[other]=new(){OtherId=other};
        Danger.Initialized=true;
    }
    public void RemoveDangerEntity(long id)
    {
        Danger.Bodies.Remove(id);Danger.People.Remove(id);Danger.Orders.Remove(id);Danger.MedicalBeds.Remove(id);
        foreach(var p in Danger.People.Values)p.Relationships.Remove(id);
        foreach(var body in Danger.Bodies.Values)if(body.CarriedBy==id)body.CarriedBy=0;
    }
    public BodyState? Body(long id)=>Danger.Bodies.GetValueOrDefault(id);
    private double PartCondition(BodyState b,string part)
    {
        var definition=Anatomy.Parts(b.AnatomyId).FirstOrDefault(p=>p.Id==part);if(definition==null)return 1;
        double damage=b.Wounds.Where(w=>w.Part==part).Sum(w=>w.Severity)+b.Scars.GetValueOrDefault(part);
        double value=Math.Clamp(1-damage/definition.Capacity,0,1);
        return definition.Parent==""?value:Math.Min(value,PartCondition(b,definition.Parent));
    }
    public Capacities Capacity(long id)
    {
        var b=Body(id);if(b==null || b.Dead)return new(0,0,0,0,0,0,0);
        double pain=Math.Clamp(b.Wounds.Sum(w=>w.Severity*(w.Kind=="Fracture"?1.4:1))/150,0,.95);
        double breathing=(PartCondition(b,"left-lung")+PartCondition(b,"right-lung"))/2;
        double consciousness=Math.Clamp(Math.Min(PartCondition(b,"brain"),b.Blood/65)*breathing*(1-pain*.5),0,1);
        double moving=(PartCondition(b,"left-leg")+PartCondition(b,"right-leg"))/2*consciousness;
        double manipulation=(PartCondition(b,"left-hand")+PartCondition(b,"right-hand"))/2*consciousness;
        return new(moving,manipulation,(PartCondition(b,"left-eye")+PartCondition(b,"right-eye"))/2,breathing,consciousness,PartCondition(b,"head")*consciousness,pain);
    }
    public bool CanAct(long id)=>!IsContained(id) && !IsAway(id) && !Slice.Buried.Contains(id) && Body(id) is not {Dead:true} and not {Downed:true} && (Danger.People.GetValueOrDefault(id)?.WithdrawalUntil??0)<=Tick;
    public bool NeedsTreatment(long id)=>Body(id) is {Dead:false} b && b.Wounds.Any(w=>!w.Tended || w.Infected && Tick-w.LastTendedAt>=TicksPerHour*6);
    public double BleedingPerHour(long id)=>Body(id)?.MagicBleedStopUntil>Tick?0:Body(id)?.Wounds.Where(w=>w.Kind is "Cut" or "Burn").Sum(w=>w.Severity*.09*(w.Tended ? .02:1))??0;
    public double BleedingHoursRemaining(long id)
    {double rate=BleedingPerHour(id);return rate>.001?Math.Max(0,(Body(id)!.Blood-5)/rate):double.PositiveInfinity;}
    private void LogDanger(string kind,long actor,long other,string text)
    {
        Danger.History.Add(new(Tick,kind,actor,other,text));if(Danger.History.Count>256)Danger.History.RemoveAt(0);Record(text);
    }
    public void Remember(long id,string kind,string text,int value,long other=0,int duration=3)
    {
        if(!Danger.People.TryGetValue(id,out var p))return;
        if(p.Memories.Any(m=>m.Kind==kind && m.OtherId==other && Tick-m.Tick<TicksPerHour))return;
        if(kind is "Conversation" or "Argument" or "Treated" or "Helped" or "Combat" or "Recovered")
        {
            var existing=p.Memories.FirstOrDefault(m=>m.Kind==kind && m.OtherId==other);
            if(existing!=null){existing.Tick=Tick;existing.Value=value;existing.Text=text;existing.DurationDays=duration;return;}
        }
        var e=Find(id);p.Memories.Add(new(){Kind=kind,Text=text,Value=value,OtherId=other,Tick=Tick,DurationDays=duration,Cell=e==null?0:Index(e.X,e.Y,e.Z)});
        if(p.Memories.Count>32)p.Memories.RemoveAt(0);
    }
    private void ChangeOpinion(long actor,long other,int value)
    {
        if(!Danger.People.TryGetValue(actor,out var p) || actor==other || !Danger.People.ContainsKey(other))return;
        if(!p.Relationships.TryGetValue(other,out var r))p.Relationships[other]=r=new(){OtherId=other};
        r.Opinion=Math.Clamp(r.Opinion+value,-100,100);r.Interactions++;r.LastInteractionAt=Tick;
    }
    public void InflictWound(long id,string part,string kind,double severity,long source,Definitions d)
    {
        EnsureDanger(d);var b=Body(id);if(b==null || b.Dead)return;
        if(!Anatomy.Parts(b.AnatomyId).Any(p=>p.Id==part) || kind is not ("Cut" or "Bruise" or "Burn" or "Fracture" or "Poison") || !double.IsFinite(severity) || severity<=0 || severity>100)throw new ArgumentException("Invalid injury.");
        b.Wounds.Add(new(){Id=Danger.NextWoundId++,Part=part,Kind=kind,Severity=severity,OriginalSeverity=severity,SourceId=source,CreatedAt=Tick});Danger.WoundsInflicted++;
        Interrupt(id,d);
        Remember(id,"Wounded",$"I was wounded by {Find(source)?.Name??"a hazard"}.",-6,source);
        if(Danger.People.ContainsKey(id) && Danger.People.ContainsKey(source))ChangeOpinion(id,source,-15);
        RefreshBody(id,d);LogDanger("Injury",source,id,$"{Find(id)!.Name}: {kind.ToLowerInvariant()} to {part} ({severity:0.0}).");
    }
    private void RefreshBody(long id,Definitions d)
    {
        var e=Find(id);var b=Body(id);if(e==null || b==null)return;bool wasDown=b.Downed;
        if(b.Dead || b.Blood<=5 || PartCondition(b,"brain")==0 || PartCondition(b,"torso")==0 || b.Wounds.Any(w=>w.Infected && w.Infection>=100))
        {
            b.Dead=true;b.Downed=false;e.Health=0;
            if(!b.DeathRecorded)
            {
                if(b.CarriedBy!=0)Interrupt(b.CarriedBy,d);
                b.DeathRecorded=true;b.DeathCause=b.Blood<=5?"blood loss":b.Wounds.Any(w=>w.Infection>=100)?"infection":"fatal trauma";
                Interrupt(id,d);Danger.Orders.Remove(id);Danger.Deaths++;
                foreach(var survivor in Colony.People.Keys.Where(p=>p!=id && Colony.People.ContainsKey(id)))Remember(survivor,"Death",$"{e.Name} died ({b.DeathCause}).",Danger.People.GetValueOrDefault(survivor)?.Relationships.GetValueOrDefault(id)?.Opinion>=20?-18:-9,id,6);
                LogDanger("Death",id,0,$"{e.Name} died from {b.DeathCause}.");
            }
            return;
        }
        var capacity=Capacity(id);b.Downed=b.Blood<30 || capacity.Consciousness<.25 || capacity.Moving<.12 || capacity.Pain>.8;
        e.Health=Math.Clamp((int)Math.Round(100-b.Wounds.Sum(w=>w.Severity)*.6-(100-b.Blood)*.3),1,100);
        if(b.Downed)
        {
            Interrupt(id,d);Danger.Orders.Remove(id);if(Colony.People.TryGetValue(id,out var profile))profile.Drafted=false;
            if(!wasDown){Danger.PauseSerial++;LogDanger("Downed",id,0,e.Name+" is downed and needs help.");Remember(id,"Downed","I was helpless and injured.",-10);}
        }
    }
    private void AdvanceBodies(Definitions d)
    {
        foreach(var b in Danger.Bodies.Values.OrderBy(b=>b.EntityId).ToArray())
        {
            if(b.Dead || Solar.Evacuees.Contains(b.EntityId) || Find(b.EntityId)==null)continue;
            b.Blood=Math.Max(0,b.Blood-BleedingPerHour(b.EntityId)/30);if(BleedingPerHour(b.EntityId)<.05)b.Blood=Math.Min(100,b.Blood+.06);
            foreach(var wound in b.Wounds.ToArray())
            {
                if(!wound.Tended && Tick-wound.CreatedAt>TicksPerHour*6 && wound.Kind is "Cut" or "Burn")wound.Infection=Math.Min(100,wound.Infection+.018*CampaignDiseaseFactor());
                if(wound.Infection>=10)wound.Infected=true;
                if(wound.Infected)
                {
                    wound.Infection=Math.Min(100,wound.Infection+(wound.Tended ? .012:.045)*CampaignDiseaseFactor());wound.Immunity=Math.Min(100,wound.Immunity+(wound.Tended ? .04+wound.TendQuality*.05:.018));
                    if(wound.Immunity>=100){wound.Infected=false;wound.Infection=0;}
                }
                double heal=(wound.Tended ? .02+wound.TendQuality*.06:.004)*(wound.Kind=="Fracture" ? .4:1);
                if(b.RepairBoostUntil>Tick)heal+=.025;
                if(!wound.Infected)wound.Severity=Math.Max(0,wound.Severity-heal);
                if(wound.Severity<=.01)
                {
                    if(wound.OriginalSeverity>=25 && wound.Kind=="Fracture")b.Scars[wound.Part]=b.Scars.GetValueOrDefault(wound.Part)+2;
                    b.Wounds.Remove(wound);Remember(b.EntityId,"Recovered","I recovered from an injury.",6,wound.DoctorId);LogDanger("Recovered",b.EntityId,wound.DoctorId,Find(b.EntityId)!.Name+" recovered from a "+wound.Kind.ToLowerInvariant()+".");
                }
            }
            RefreshBody(b.EntityId,d);
        }
    }
    public void Tend(long doctorId,long patientId,bool medicine,Definitions d)
    {
        var body=Body(patientId);if(body==null || body.Dead)return;int skill=Colony.People.GetValueOrDefault(doctorId)?.Skills[2]??0;var patient=Find(patientId)!;
        bool bed=Entities.Any(e=>Danger.MedicalBeds.Contains(e.Id) && BuildingCells(e).Contains((patient.X,patient.Y,patient.Z)));
        double confidence=Math.Clamp(.45+skill*.035,.45,.98)*Capacity(doctorId).Sight;
        double quality=Math.Clamp(.15+skill*.035+(medicine ? .25:0)+(bed ? .1:0),.1,.95)*Capacity(doctorId).Manipulation*(.65+.35*confidence);
        foreach(var w in body.Wounds){w.Tended=true;w.LastTendedAt=Tick;w.TendQuality=Math.Max(w.TendQuality,quality);w.DoctorId=doctorId;}
        body.DiagnosticConfidence=confidence;Danger.Treatments++;
        Remember(patientId,"Treated",$"{Find(doctorId)!.Name} cared for my wounds.",9,doctorId,5);Remember(doctorId,"Helped","I helped an injured resident.",5,patientId);
        ChangeOpinion(patientId,doctorId,15);ChangeOpinion(doctorId,patientId,4);LogDanger("Treatment",doctorId,patientId,$"{Find(doctorId)!.Name} tended {patient.Name} ({quality:P0}, {(medicine?"herbal medicine":"field bandage")}).");
    }
    private void AdvancePsychology(Definitions d)
    {
        if(Tick%300!=0)return;
        foreach(var person in Danger.People.Values.OrderBy(p=>p.EntityId))
        {
            if(Solar.Evacuees.Contains(person.EntityId)||Body(person.EntityId) is not {Dead:false})continue;var p=Colony.People[person.EntityId];person.Company=Math.Max(0,person.Company-1.8);person.Meaning=Math.Max(0,person.Meaning-.4);
            var restJob=Autonomy.Jobs.Find(j=>j.PawnId==person.EntityId && j.Kind is "Sleep" or "PatientRest");
            if(restJob!=null)
            {person.Comfort=restJob.TargetId==0?300:900;var sleeper=Find(person.EntityId)!;int roommates=Colony.People.Keys.Count(id=>id!=person.EntityId && Colony.People[id].Activity is "Sleep" or "Recovering in bed" && Distance(sleeper,Find(id)!)<=4);person.Privacy=1000.0/(roommates+1);}
            person.Memories.RemoveAll(m=>Tick-m.Tick>=TicksPerDay*m.DurationDays);double memories=person.Memories.Sum(m=>m.Value*(1-(Tick-m.Tick)/(double)(TicksPerDay*m.DurationDays)));var cap=Capacity(person.EntityId);
            p.Mood=(int)Math.Clamp(62+(p.Food-500)/100+(p.Rest-500)/100+(p.Recreation-500)/120+(person.Company-500)/150+(person.Meaning-500)/150+(person.Comfort-500)/200+(person.Privacy-500)/200-cap.Pain*35+memories,0,100);
            if(p.Mood<20)person.StressTicks+=(long)(300*CampaignStressFactor());else person.StressTicks=Math.Max(0,person.StressTicks-600);
            int threshold=p.Trait=="Steadfast"?8:6;
            if(person.StressTicks>=TicksPerHour*threshold && person.WithdrawalUntil<=Tick)
            {
                person.WithdrawalUntil=Tick+4*TicksPerHour;person.StressTicks=0;Interrupt(person.EntityId,d);Danger.Orders.Remove(person.EntityId);p.Drafted=false;Danger.PauseSerial++;
                LogDanger("Stress",person.EntityId,0,Find(person.EntityId)!.Name+" withdraws from work after sustained distress.");Remember(person.EntityId,"Withdrawal","I needed time away from everyone.",-3);
            }
        }
        if(Tick%TicksPerHour!=0)return;
        var eligible=Colony.People.Keys.Where(id=>CanAct(id) && Capacity(id).Speech>.5 && Colony.People[id].Activity!="Sleep").Order().ToArray();var random=new StableRandom(JobRandomState);
        if(eligible.Length>=2)
        {
            long actor=eligible[random.Next(eligible.Length)];var first=Find(actor)!;var nearby=eligible.Where(id=>id!=actor && Distance(first,Find(id)!)<=5).ToArray();
            if(nearby.Length>0)
            {
                long other=nearby[random.Next(nearby.Length)];var second=Find(other)!;int opinion=Danger.People[actor].Relationships[other].Opinion;bool argument=opinion< -15 && random.Next(3)==0;
                int change=argument?-4:Colony.People[actor].Trait==Colony.People[other].Trait?3:2;ChangeOpinion(actor,other,change);ChangeOpinion(other,actor,change);
                foreach(long id in new[]{actor,other}){Danger.People[id].Company=Math.Min(1000,Danger.People[id].Company+90);Danger.People[id].LastSocialAt=Tick;Remember(id,argument?"Argument":"Conversation",argument?"We argued over the colony.":"A pleasant conversation made me feel less alone.",argument?-5:3,id==actor?other:actor);}
                LogDanger("Social",actor,other,first.Name+(argument?" argued with ":" chatted with ")+second.Name+".");
            }
        }
        JobRandomState=random.State;
    }
    private void AdvancePeopleAndDanger(Definitions d)
    {EnsureDanger(d);AdvanceBodies(d);AdvancePsychology(d);AdvanceRaid(d);AdvanceCombat(d);Danger.Traces.RemoveAll(t=>Tick-t.Tick>180);}
    public int Distance(Entity a,Entity b)=>a.Z==b.Z?Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y):int.MaxValue;
}
