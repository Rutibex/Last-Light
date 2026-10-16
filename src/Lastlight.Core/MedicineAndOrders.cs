namespace Lastlight.Core;
public sealed partial class World
{
    private PolicyPreset MedicalPolicy(long id)=>Colony.Policies.First(p=>p.Id==Colony.People[id].Policies["Medicine"]);
    private ColonyJob? PickMedicalJob(Entity pawn,PawnProfile p,Definitions d)
    {
        if(Capacity(pawn.Id).Manipulation<.2)return null;
        if(p.Priorities[2]>0 && !p.Drafted)
        {
            var patients=Colony.People.Keys.Where(id=>!IsAway(id)).Where(id=>Body(id) is {Dead:false} && MedicalPolicy(id).Values.GetValueOrDefault("Care level",2)>0 && (id!=pawn.Id || MedicalPolicy(id).Values.GetValueOrDefault("Self tend",1)>0)).OrderByDescending(id=>BleedingPerHour(id)).ThenBy(id=>id);
            foreach(long patientId in patients)
            {
                if(!NeedsTreatment(patientId) || Autonomy.Claims.ContainsKey("patient:"+patientId))continue;
                var patient=Find(patientId)!;int cell=Index(patient.X,patient.Y,patient.Z);
                if(FindPath(pawn,p,cell,pawn.Id!=patientId,d)==null)continue;
                var job=new ColonyJob{PawnId=pawn.Id,Kind="Tend",TargetId=patientId,Cell=cell,Claims=["patient:"+patientId]};
                int care=MedicalPolicy(patientId).Values.GetValueOrDefault("Care level",2);
                if(care>=2)ReserveIngredients(job,new(){{"medicine",1}},d);
                // A failed multi-stack search must not leave partial ingredient claims in a field-bandage job.
                if(job.Sources.Count==0){job.Amounts.Clear();job.Claims=["patient:"+patientId];}
                return job;
            }
            foreach(long patientId in patients.Where(id=>id!=pawn.Id && Body(id) is {Downed:true,CarriedBy:0}))
            {
                if(Autonomy.Claims.ContainsKey("patient:"+patientId))continue;
                var rescue=MakeRescue(pawn,patientId,d);if(rescue!=null)return rescue;
            }
        }
        if(p.Priorities[1]>0 && Body(pawn.Id) is {Dead:false} body && body.Wounds.Count>0 && !p.Drafted)
        {
            foreach(var bed in Entities.Where(e=>e.DefinitionId=="bed" && !e.Forbidden && !Autonomy.Claims.ContainsKey("bed:"+e.Id)).OrderByDescending(e=>Danger.MedicalBeds.Contains(e.Id)).ThenBy(e=>Distance(e,pawn)).ThenBy(e=>e.Id))
            {
                int cell=Index(bed.X,bed.Y,bed.Z);if(FindPath(pawn,p,cell,false,d)!=null)return new(){PawnId=pawn.Id,Kind="PatientRest",TargetId=bed.Id,Cell=cell,Claims=["bed:"+bed.Id]};
            }
        }
        return null;
    }
    private ColonyJob? MakeRescue(Entity actor,long patientId,Definitions d)
    {
        var patient=Find(patientId);var profile=Colony.People[actor.Id];
        if(patient==null || patient.Id==actor.Id || !Colony.People.ContainsKey(patientId) || Body(patientId) is not {Dead:false,Downed:true,CarriedBy:0})return null;
        int source=Index(patient.X,patient.Y,patient.Z);if(FindPath(actor,profile,source,true,d)==null)return null;
        foreach(var bed in Entities.Where(e=>e.DefinitionId=="bed" && !e.Forbidden && !Autonomy.Claims.ContainsKey("bed:"+e.Id)).OrderByDescending(e=>Danger.MedicalBeds.Contains(e.Id)).ThenBy(e=>Distance(patient,e)).ThenBy(e=>e.Id))
        {
            int destination=Index(bed.X,bed.Y,bed.Z);if(FindPath(actor,profile,destination,false,d)==null)continue;
            return new(){PawnId=actor.Id,Kind="Rescue",AuxiliaryId=bed.Id,TargetId=patientId,Cell=source,Destination=destination,Claims=["patient:"+patientId,"bed:"+bed.Id]};
        }
        return null;
    }
    private bool ValidMedicalJob(ColonyJob job,PawnProfile p)
    {
        if(job.Kind=="Tend")return Colony.People.ContainsKey(job.TargetId) && (MedicalPolicy(job.TargetId).Values.GetValueOrDefault("Care level",2)>=2 || !job.Cargo.ContainsKey("medicine") && job.Sources.All(id=>Find(id)?.DefinitionId!="medicine")) && !p.Drafted && p.Priorities[2]>0 && NeedsTreatment(job.TargetId) && MedicalPolicy(job.TargetId).Values.GetValueOrDefault("Care level",2)>0 && (job.TargetId!=job.PawnId || MedicalPolicy(job.TargetId).Values.GetValueOrDefault("Self tend",1)>0);
        if(job.Kind=="PatientRest")return !p.Drafted && Body(job.PawnId) is {Dead:false} b && b.Wounds.Count>0 && Find(job.TargetId) is {Forbidden:false};
        if(job.Kind=="Rescue")return Find(job.AuxiliaryId) is {DefinitionId:"bed",Forbidden:false} && Body(job.TargetId) is {Dead:false} body && (body.Downed && body.CarriedBy==0 || body.CarriedBy==job.PawnId);
        if(job.Kind=="Equip")return job.DefinitionId is "arrows" or "padded-armor" || Colony.Policies.First(q=>q.Id==p.Policies["Equipment"]).Values.GetValueOrDefault("Carry weapon",1)>0;
        if(job.Kind=="Loot")return Body(job.TargetId) is {Dead:true};
        return false;
    }
    private bool HandleMedicalJob(ColonyJob job,Entity pawn,Definitions d)
    {
        if(job.Kind=="Tend")
        {
            job.WorkCredit+=Capacity(pawn.Id).Manipulation;if(job.WorkCredit<1)return true;job.WorkCredit-=1;
            if(++job.Work<30)return true;
            Tend(pawn.Id,job.TargetId,job.Cargo.GetValueOrDefault("medicine")>0,d);job.Cargo.Clear();Interrupt(pawn.Id,d);return true;
        }
        if(job.Kind=="PatientRest")
        {
            var p=Colony.People[pawn.Id];p.Activity="Recovering in bed";p.Rest=Math.Min(1000,p.Rest+3);
            if(p.Food<250 || Body(pawn.Id)!.Wounds.Count==0)Interrupt(pawn.Id,d);return true;
        }
        if(job.Kind=="Rescue")
        {
            var body=Body(job.TargetId)!;
            if(job.Destination>=0)
            {
                body.CarriedBy=pawn.Id;MoveActor(pawn,Index(pawn.X,pawn.Y,pawn.Z));job.Cell=job.Destination;job.Destination=-1;job.Path.Clear();return true;
            }
            body.CarriedBy=0;Danger.Rescues++;Remember(job.TargetId,"Rescued",Find(pawn.Id)!.Name+" carried me to safety.",12,pawn.Id,6);ChangeOpinion(job.TargetId,pawn.Id,20);ChangeOpinion(pawn.Id,job.TargetId,8);
            LogDanger("Rescue",pawn.Id,job.TargetId,pawn.Name+" rescued "+Find(job.TargetId)!.Name+" to a bed.");Interrupt(pawn.Id,d);return true;
        }
        if(job.Kind=="Loot")
        {
            var corpse=Body(job.TargetId)!;
            if(corpse.WeaponId!="unarmed"){DropGear(corpse.WeaponId,corpse.WeaponCondition,pawn,d);corpse.WeaponId="unarmed";}
            if(corpse.ArmorId!=""){DropGear(corpse.ArmorId,corpse.ArmorCondition,pawn,d);corpse.ArmorId="";}
            if(corpse.Ammo>0){Drop("arrows",corpse.Ammo,Index(pawn.X,pawn.Y,pawn.Z),d);corpse.Ammo=0;}
            LogDanger("Loot",pawn.Id,job.TargetId,pawn.Name+" recovered the fallen combatant's gear.");Interrupt(pawn.Id,d);return true;
        }
        if(job.Kind=="Equip")
        {
            var body=Body(pawn.Id)!;
            if(job.DefinitionId=="arrows")body.Ammo+=job.Cargo.GetValueOrDefault("arrows");
            else if(job.DefinitionId=="padded-armor")
            {if(body.ArmorId!="")DropGear(body.ArmorId,body.ArmorCondition,pawn,d);body.ArmorId=job.DefinitionId;body.ArmorCondition=job.ItemCondition;}
            else
            {if(body.WeaponId!="unarmed")DropGear(body.WeaponId,body.WeaponCondition,pawn,d);body.WeaponId=job.DefinitionId;body.WeaponCondition=job.ItemCondition;}
            job.Cargo.Clear();Interrupt(pawn.Id,d);LogDanger("Equipment",pawn.Id,0,pawn.Name+" equipped "+job.DefinitionId+".");return true;
        }
        return false;
    }
    private void DropGear(string definition,int condition,Entity owner,Definitions d)
    {
        if(condition<=0)return;long id=NextEntityId++;Entities.Add(new(){Id=id,DefinitionId=definition,Quantity=1,Health=condition,Name=d.Entities[definition].Name,X=owner.X,Y=owner.Y,Z=owner.Z});
    }
    private CommandResult ApplyDangerCommand(WorldCommand c,Definitions d)
    {
        EnsureDanger(d);
        if(c.Kind==CommandKind.DangerSettings)
        {
            if(c.Text=="raids")Danger.RaidsEnabled=c.Quantity!=0;
            else if(c.Text=="pause")Danger.AutoPause=c.Quantity!=0;
            else return new(false,"Unknown danger setting.");
            return new(true,"Danger setting updated.");
        }
        if(c.Kind==CommandKind.TriggerRaid)return BeginRaid(c.Quantity,d,c.Text=="scenario" && Contains(c.X,c.Y,c.Z)?Index(c.X,c.Y,c.Z):-1);
        if(c.Kind==CommandKind.MedicalBed)
        {
            if(Find(c.EntityId)?.DefinitionId!="bed")return new(false,"Select a finished bed.");
            if(c.Quantity==0)Danger.MedicalBeds.Remove(c.EntityId);else Danger.MedicalBeds.Add(c.EntityId);
            return new(true,c.Quantity==0?"Bed returned to ordinary use.":"Bed reserved for medical care.");
        }
        var actor=Find(c.EntityId);
        if(actor==null || !Colony.People.TryGetValue(c.EntityId,out var p) || !CanAct(c.EntityId))return new(false,"Select a capable resident.");
        if(c.Kind==CommandKind.Rescue)
        {
            if(Autonomy.Claims.ContainsKey("patient:"+c.TargetId))return new(false,"Another worker has claimed the patient.");
            var job=MakeRescue(actor,c.TargetId,d);if(job==null)return new(false,"A downed resident and reachable unclaimed bed are required.");
            Interrupt(actor.Id,d);Danger.Orders.Remove(actor.Id);if(!Start(job))return new(false,"Rescue claims are unavailable.");return new(true,"Rescue ordered.");
        }
        if(c.Kind==CommandKind.Equip)
        {
            var item=Find(c.TargetId);
            if(c.Text=="loot")
            {
                var corpse=Body(c.TargetId);
                if(item==null || corpse is not {Dead:true} || corpse.WeaponId=="unarmed" && corpse.ArmorId=="" && corpse.Ammo==0)return new(false,"No recoverable gear on this corpse.");
                int at=Index(item.X,item.Y,item.Z);if(FindPath(actor,p,at,false,d)==null || Autonomy.Claims.ContainsKey("corpse:"+item.Id))return new(false,"Corpse is unreachable or already claimed.");
                Interrupt(actor.Id,d);Danger.Orders.Remove(actor.Id);Start(new(){PawnId=actor.Id,Kind="Loot",TargetId=item.Id,Cell=at,Claims=["corpse:"+item.Id]});return new(true,"Salvage ordered.");
            }
            if(item==null || item.Forbidden || item.Health<=0 || !(d.Document.Weapons.Any(w=>w.Id==item.DefinitionId) || item.DefinitionId is "padded-armor" or "arrows"))return new(false,"Select an allowed weapon, armor or ammunition stack.");
            if(d.Document.Weapons.Any(w=>w.Id==item.DefinitionId) && Colony.Policies.First(q=>q.Id==p.Policies["Equipment"]).Values.GetValueOrDefault("Carry weapon",1)==0)return new(false,"Equipment policy forbids carrying a weapon.");
            if(item.DefinitionId=="arrows" && Body(actor.Id)!.Ammo>=30)return new(false,"Ammunition pouch already holds 30 arrows.");
            int cell=Index(item.X,item.Y,item.Z);if(FindPath(actor,p,cell,false,d)==null)return new(false,"Equipment is unreachable.");
            if(Autonomy.Claims.ContainsKey("item:"+item.Id))return new(false,"Another worker claimed this item.");
            Interrupt(actor.Id,d);Danger.Orders.Remove(actor.Id);
            var job=new ColonyJob{PawnId=actor.Id,Kind="Equip",DefinitionId=item.DefinitionId,ItemCondition=Physical.Enabled?Math.Clamp((int)(item.Health*(.6+item.CraftQuality*.008)),1,100):item.Health,Cell=cell,Sources=[item.Id],Amounts=[item.DefinitionId=="arrows"?Math.Min(30-Body(actor.Id)!.Ammo,item.Quantity):1],Claims=["item:"+item.Id]};
            Start(job);return new(true,"Equipment pickup ordered.");
        }
        if(c.Kind==CommandKind.CombatOrder)
        {
            if(!p.Drafted)return new(false,"Draft the resident first.");
            if(c.Text=="hold"){Interrupt(actor.Id,d);Danger.Orders[actor.Id]=new(){Kind="Hold"};return new(true,"Hold position and defend.");}
            if(c.Text=="attack")
            {
                var target=Find(c.TargetId);if(target==null || target.Id==actor.Id || Colony.People.ContainsKey(target.Id) || Body(target.Id) is not {Dead:false,Downed:false})return new(false,"Select a living hostile or wild creature.");
                Interrupt(actor.Id,d);Danger.Orders[actor.Id]=new(){Kind="Attack",TargetId=target.Id};return new(true,"Attack ordered.");
            }
            if(c.Text=="retreat" && Contains(c.X,c.Y,c.Z))
            {
                var path=FindPath(actor,p,Index(c.X,c.Y,c.Z),false,d);if(path==null)return new(false,"Retreat destination is unreachable.");
                Interrupt(actor.Id,d);Danger.Orders[actor.Id]=new(){Kind="Retreat",Path=path};return new(true,"Retreat ordered.");
            }
            return new(false,"Unknown combat order.");
        }
        return new(false,"Command unavailable.");
    }
    public void SetupDangerSupplies(Definitions d)
    {
        int cx=Width/2,cy=Height/2;
        foreach(var item in new Dictionary<string,int>{{"medicine",20},{"arrows",60},{"fiber",30},{"brass",25},{"bow",3},{"sword",2},{"spear",3},{"padded-armor",3}})Drop(item.Key,item.Value,Index(cx+1,cy+3),d);
    }
}
