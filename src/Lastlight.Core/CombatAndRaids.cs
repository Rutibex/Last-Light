namespace Lastlight.Core;
public sealed partial class World
{
    public WeaponDefinition Weapon(long id,Definitions d)
    {
        var body=Body(id);var weapon=d.Document.Weapons.Find(w=>w.Id==body?.WeaponId);
        return weapon!=null && body!.WeaponCondition>0 && (weapon.AmmoId=="" || body.Ammo>0)?weapon:new("unarmed",4,1,90,.72,"Bruise");
    }
    public bool HasLineOfSight(Entity a,Entity b,Definitions d)
    {
        if(a.Z!=b.Z)return false;
        int x=a.X,y=a.Y,dx=Math.Abs(b.X-x),dy=-Math.Abs(b.Y-y),sx=x<b.X?1:-1,sy=y<b.Y?1:-1,error=dx+dy;
        while(x!=b.X || y!=b.Y)
        {
            int twice=error*2;if(twice>=dy){error+=dy;x+=sx;}if(twice<=dx){error+=dx;y+=sy;}
            if(x==b.X && y==b.Y)break;
            if(!Contains(x,y,a.Z) || Sealed(Index(x,y,a.Z)) || CellAt(x,y,a.Z).TerrainId=="stone" || Entities.Any(e=>e.DefinitionId is "wall" or "door" or "column" or "sealed-door" && BuildingCells(e).Contains((x,y,a.Z))))return false;
        }
        return true;
    }
    public double Cover(Entity target)=>Entities.Any(e=>e.DefinitionId is "wall" or "fence" or "barricade" && Distance(target,e)<=1) ? .22:0;
    private bool Hostile(long id)=>(Find(id)?.DefinitionId=="raider" || Slice.Hostiles.Contains(id)) && Body(id) is {Dead:false,Downed:false,Captured:false};
    public bool TryAttack(long attackerId,long targetId,Definitions d)
    {
        var actor=Find(attackerId);var target=Find(targetId);var body=Body(attackerId);
        if(actor==null || target==null || IsAway(targetId) || body==null || !CanAct(attackerId) || Body(targetId) is not {Dead:false} || body.NextAttackAt>Tick)return false;
        var weapon=Weapon(attackerId,d);
        if(Distance(actor,target)>weapon.Range || !HasLineOfSight(actor,target,d))return false;
        var capacities=Capacity(attackerId);if(capacities.Manipulation<.2 || capacities.Sight<.1)return false;
        body.NextAttackAt=Tick+weapon.Cooldown;
        if(weapon.AmmoId!="")body.Ammo--;
        var random=new StableRandom(CombatRandomState);
        int skill=Colony.People.GetValueOrDefault(attackerId)?.Skills[weapon.Range>1?7:4]??6;
        double chance=Math.Clamp(weapon.Accuracy*(.65+skill*.025)*capacities.Manipulation*capacities.Sight-Cover(target)*(weapon.Range>1?1:.25),.08,.96);
        bool hit=random.Next(10000)<chance*10000;
        Danger.Traces.Add(new(Tick,attackerId,targetId,Index(actor.X,actor.Y,actor.Z),Index(target.X,target.Y,target.Z),hit));
        if(Danger.Traces.Count>128)Danger.Traces.RemoveAt(0);
        if(hit)
        {
            string[] parts=["torso","torso","torso","head","left-arm","right-arm","left-leg","right-leg","left-hand","right-hand"];
            string part=parts[random.Next(parts.Length)];if(!Anatomy.Parts(Body(targetId)!.AnatomyId).Any(p=>p.Id==part))part="torso";
            double damage=weapon.Damage*(.85+random.Next(31)/100.0);
            var protection=Body(targetId)!;
            if(protection.ArmorId!="" && protection.ArmorCondition>0){damage*=.6;protection.ArmorCondition=Math.Max(0,protection.ArmorCondition-2);}
            CombatRandomState=random.State;InflictWound(targetId,part,weapon.InjuryKind,damage,attackerId,d);
            Remember(attackerId,"Combat","I fought to protect the refuge.",3,targetId);
        }
        else CombatRandomState=random.State;
        if(body.WeaponId!="unarmed")body.WeaponCondition=Math.Max(0,body.WeaponCondition-1);
        return true;
    }
    private void MoveActor(Entity actor,int cell)
    {
        var pos=Position(cell);actor.X=pos.X;actor.Y=pos.Y;actor.Z=pos.Z;
        foreach(var b in Danger.Bodies.Values.Where(b=>b.CarriedBy==actor.Id)){var carried=Find(b.EntityId);if(carried!=null){carried.X=actor.X;carried.Y=actor.Y;carried.Z=actor.Z;}}
    }
    private void ScheduleNextMovement(long id,Definitions d)
    {
        var body=Body(id)!;double carrying=Danger.Bodies.Values.Any(b=>b.CarriedBy==id)?.6:1;
        double mass=Physical.Enabled?(Autonomy.Jobs.FirstOrDefault(j=>j.PawnId==id)?.Cargo.Sum(k=>k.Value*(d.Document.Materials.FirstOrDefault(m=>m.Id==MaterialProperties(new Entity{DefinitionId=k.Key},d).Id)?.Mass??1))??0):0;
        double ward=Physical.Enabled && (Slice.Hostiles.Contains(id)||Find(id)?.DefinitionId=="raider") && Find(id) is {} contained && Entities.Any(e=>e.DefinitionId=="f-ward"&&e.Z==contained.Z&&Distance(e,contained)<=4&&DevicePowered(e.Id))?.4:1;
        double rate=Math.Max(.1,Capacity(id).Moving*carrying*ward/(1+mass/100));
        body.NextMoveAt=Math.Max(body.NextMoveAt,Tick-30)+(long)Math.Ceiling(30/rate);
    }
    private void Pursue(Entity actor,int destination,bool adjacent,CombatOrder order,Definitions d)
    {
        var body=Body(actor.Id)!;if(Tick<body.NextMoveAt || Tick<order.RetryAt)return;
        var profile=Colony.People.GetValueOrDefault(actor.Id)??new PawnProfile();
        int here=Index(actor.X,actor.Y,actor.Z);
        if(order.Path.Count==0 || !Neighbors(here).Contains(order.Path[0]) || !Passable(order.Path[0],profile,d))
        {
            var path=FindPath(actor,profile,destination,adjacent,d);
            if(path==null){order.RetryAt=Tick+300;return;}order.Path=path;
        }
        if(order.Path.Count>0){MoveActor(actor,order.Path[0]);order.Path.RemoveAt(0);}
        ScheduleNextMovement(actor.Id,d);
    }
    private void AdvanceCombat(Definitions d)
    {
        foreach(long id in Colony.People.Keys.Order().ToArray())
        {
            var actor=Find(id)!;var profile=Colony.People[id];if(!CanAct(id) || Slice.Casts.ContainsKey(id))continue;
            if(!profile.Drafted)
            {
                // Civilians interrupt chores and flee nearby raiders; orders remain a drafted action.
                var enemy=Entities.Where(e=>Hostile(e.Id) && Distance(actor,e)<=7 && HasLineOfSight(actor,e,d)).OrderBy(e=>Distance(actor,e)).ThenBy(e=>e.Id).FirstOrDefault();
                if(enemy!=null)
                {
                    Interrupt(id,d);profile.Activity="Fleeing";
                    var choices=Neighbors(Index(actor.X,actor.Y,actor.Z)).Where(c=>Passable(c,profile,d)).OrderByDescending(c=>Math.Abs(Position(c).X-enemy.X)+Math.Abs(Position(c).Y-enemy.Y)).ThenBy(c=>c).ToArray();
                    if(choices.Length>0 && Tick>=Body(id)!.NextMoveAt){MoveActor(actor,choices[0]);Body(id)!.NextMoveAt=Tick+60;}
                }
                continue;
            }
            if(Autonomy.Jobs.Any(j=>j.PawnId==id && j.Kind is "Move" or "Equip" or "Rescue"))continue;
            var order=Danger.Orders.GetValueOrDefault(id)??new CombatOrder();
            if(order.Kind=="Retreat")
            {
                if(order.Path.Count>0)Pursue(actor,order.Path[^1],false,order,d);profile.Activity="Retreating";continue;
            }
            var target=order.Kind=="Attack"?Find(order.TargetId):Entities.Where(e=>Hostile(e.Id) && Distance(actor,e)<=Weapon(id,d).Range).OrderBy(e=>Distance(actor,e)).ThenBy(e=>e.Id).FirstOrDefault();
            if(target==null || Body(target.Id) is {Dead:true} or {Downed:true}){if(order.Kind=="Attack")Danger.Orders.Remove(id);continue;}
            profile.Activity="Fighting";
            if(!TryAttack(id,target.Id,d) && order.Kind=="Attack" && (Distance(actor,target)>Weapon(id,d).Range || !HasLineOfSight(actor,target,d)))
            {order.Path.Clear();Pursue(actor,Index(target.X,target.Y,target.Z),true,order,d);}
        }
        AdvanceRuinGuardians(d);
        if(Danger.Raid.Phase!="Active")return;
        foreach(long id in Danger.Raid.RaiderIds.Order().ToArray())
        {
            if(!Hostile(id))continue;var actor=Find(id)!;
            if(!Danger.Orders.TryGetValue(id,out var order))Danger.Orders[id]=order=new();
            if(Danger.Raid.Retreating)
            {
                int destination=Danger.Raid.SpawnCell;
                if(Index(actor.X,actor.Y,actor.Z)==destination){LeaveRaider(id,true);continue;}
                order.Kind="Retreat";Pursue(actor,destination,false,order,d);continue;
            }
            var target=Entities.Where(e=>Colony.People.ContainsKey(e.Id) && !IsAway(e.Id) && Body(e.Id) is {Dead:false,Downed:false}).OrderBy(e=>Distance(actor,e)).ThenBy(e=>e.Id).FirstOrDefault();
            if(target!=null)
            {
                order.Kind="Attack";order.TargetId=target.Id;
                if(!TryAttack(id,target.Id,d) && (Distance(actor,target)>Weapon(id,d).Range || !HasLineOfSight(actor,target,d)))
                {order.Path.Clear();Pursue(actor,Index(target.X,target.Y,target.Z),true,order,d);}
            }
            // Their objective is supplies, not extermination. Reaching unattended food triggers departure.
            var food=Entities.Where(e=>e.DefinitionId is "grain" or "meal" && !e.Forbidden && Distance(actor,e)<=1 && !Autonomy.Claims.ContainsKey("item:"+e.Id)).OrderBy(e=>e.Id).FirstOrDefault();
            if(food!=null && !Entities.Any(e=>Colony.People.ContainsKey(e.Id) && !IsAway(e.Id) && Colony.People[e.Id].Drafted && Distance(actor,e)<=3))
            {int stolen=Math.Min(5,food.Quantity);food.Quantity-=stolen;if(food.Quantity==0)Entities.Remove(food);Danger.Raid.StolenFood+=stolen;Danger.Raid.Retreating=true;LogDanger("Theft",id,0,"The scavengers stole "+stolen+" food and are withdrawing.");}
        }
    }
    private void LeaveRaider(long id,bool escaped)
    {
        var actor=Find(id);if(actor!=null)Entities.Remove(actor);Colony.Animals.Remove(id);RemoveDangerEntity(id);if(escaped)Danger.Raid.Escaped++;
    }
    public CommandResult BeginRaid(int count,Definitions d,int scenarioCell=-1)
    {
        EnsureDanger(d);
        if(Danger.Raid.Phase is "Warning" or "Active" || Tick<Danger.Raid.RecoveryUntil)return new(false,"A raid or recovery window is already in progress.");
        if(count is <1 or >6)return new(false,"Raid size must be 1–6.");
        if(!Colony.People.Keys.Any(CanAct))return new(false,"No capable residents remain.");
        int spawn=scenarioCell;
        var anchor=Entities.First(e=>Colony.People.ContainsKey(e.Id) && !IsAway(e.Id) && CanAct(e.Id));
        var random=new StableRandom(IncidentRandomState);
        if(spawn<0)
        {
            var candidates=Enumerable.Range(0,Cells.Count).Where(i=>Position(i).Z==0 && (Position(i).X==Width-2 || Position(i).Y==1 || Position(i).Y==Height-2) && Passable(i,new PawnProfile(),d) && !Occupied(Position(i).X,Position(i).Y,Position(i).Z,0,d)).ToArray();
            int offset=random.Next(Math.Max(1,candidates.Length));
            spawn=-1;for(int i=0;i<candidates.Length;i++){int cell=candidates[(offset+i)%candidates.Length];if(FindPath(anchor,new PawnProfile(),cell,false,d)!=null){spawn=cell;break;}}
        }
        if(spawn<0 || spawn>=Cells.Count || !Passable(spawn,new PawnProfile(),d) || Occupied(Position(spawn).X,Position(spawn).Y,Position(spawn).Z,0,d) || FindPath(anchor,new PawnProfile(),spawn,false,d)==null)return new(false,"No reachable raider entry point.");
        IncidentRandomState=random.State;
        Danger.Raid=new(){Phase="Warning",Count=count,SpawnCell=spawn,WarningUntil=Tick+2*TicksPerHour};Danger.PauseSerial++;
        var pos=Position(spawn);
        AddDangerLetter(new(){Id=Colony.NextObjectId++,Title="Raid warning: Ash-road scavengers",Body=$"{count} scavengers are approaching from {pos.X},{pos.Y}. They seek food and salvage. Draft defenders, equip weapons and protect the injured. They arrive in two game hours.",X=pos.X,Y=pos.Y});
        LogDanger("RaidWarning",0,0,$"A scout reports {count} approaching scavengers.");return new(true,"Raid warning issued. Two game hours to prepare.");
    }
    private void AddDangerLetter(ColonyLetter letter)
    {
        if(Colony.Letters.Count>=128){int at=Colony.Letters.FindIndex(l=>l.Archived);Colony.Letters.RemoveAt(at<0?0:at);}Colony.Letters.Add(letter);
    }
    private void AdvanceRaid(Definitions d)
    {
        var raid=Danger.Raid;
        if(raid.Phase=="Warning" && Tick>=raid.WarningUntil)
        {
            var pos=Position(raid.SpawnCell);
            for(int i=0;i<raid.Count;i++)
            {
                var result=Apply(new(CommandKind.Spawn,DefinitionId:"raider",X:pos.X,Y:pos.Y,Z:pos.Z),d);if(!result.Accepted)continue;
                var actor=Find(result.EntityId)!;actor.Name="Ash-road scavenger "+(i+1);EnsureDanger(d);Body(actor.Id)!.WeaponId="spear";raid.RaiderIds.Add(actor.Id);
            }
            raid.Phase="Active";raid.StartedAt=Tick;Danger.PauseSerial++;
            foreach(long resident in Colony.People.Keys)Remember(resident,"Raid","Scavengers attacked our refuge.",-7,0,4);
            LogDanger("RaidArrival",0,0,"The Ash-road scavengers have arrived.");
        }
        if(raid.Phase=="Active")
        {
            int capable=raid.RaiderIds.Count(Hostile);
            if(capable<raid.Count || Tick-raid.StartedAt>TicksPerHour*6)raid.Retreating=true;
            if(capable==0)
            {
                foreach(long defeated in raid.RaiderIds)if(Body(defeated) is {Dead:false,Downed:true} captive)captive.Captured=true;
                raid.Phase="Recovery";raid.RecoveryUntil=Tick+TicksPerDay*3;Danger.NextRaidAt=raid.RecoveryUntil+TicksPerDay*2;Danger.RaidsCompleted++;Danger.PauseSerial++;
                foreach(long resident in Colony.People.Keys)if(Body(resident) is {Dead:false})Remember(resident,"SurvivedRaid","We survived the scavenger raid.",8,0,5);
                AddDangerLetter(new(){Id=Colony.NextObjectId++,Title="Raid ended: recovery",Body="The attackers are no longer fighting. Undraft residents, assign Doctor work, allow medicine and provide a medical bed. Three quiet days are reserved for recovery.",X=Width/2,Y=Height/2});
                LogDanger("RaidEnded",0,0,"Raid ended; recovery window begins.");
            }
            return;
        }
        if(raid.Phase=="Recovery" && Tick>=raid.RecoveryUntil)raid.Phase="Idle";
        if(!Solar.Enabled && Danger.RaidsEnabled && raid.Phase=="Idle" && Tick>=Danger.NextRaidAt)
        {
            if(Colony.People.Keys.Any(id=>NeedsTreatment(id) || Body(id) is {Downed:true})){Danger.NextRaidAt=Tick+TicksPerDay;return;}
            int equipped=Colony.People.Keys.Count(id=>Body(id)!.WeaponId!="unarmed" && CanAct(id));int wealth=Resources("wood").Total+Resources("blocks").Total+Resources("brass").Total;
            int count=Math.Clamp(1+equipped/3+wealth/500,1,4);var started=BeginRaid(count,d);if(!started.Accepted)Danger.NextRaidAt=Tick+TicksPerDay;
        }
    }
    public IEnumerable<ColonyAlert> DangerAlerts()
    {
        if(Danger.Raid.Phase is "Warning" or "Active")yield return new("raid",Danger.Raid.Phase=="Warning"?"Raid approaching":"Raid in progress","Open Danger for warning time, attackers and recovery settings.","Danger");
        var emergency=Colony.People.Keys.FirstOrDefault(id=>Body(id) is {Dead:false,Downed:true});
        if(emergency!=0)yield return new("downed","Resident downed","Rescue to a bed or send an undrafted doctor to treat at the wound site.",EntityId:emergency);
        var patient=Colony.People.Keys.FirstOrDefault(NeedsTreatment);
        if(patient!=0)yield return new("medical","Untended wounds","Check Doctor priority, medicine policy, supplies and a reachable patient.",EntityId:patient);
        var stressed=Colony.People.Keys.FirstOrDefault(id=>Body(id) is {Dead:false} && Colony.People[id].Mood<25);
        if(stressed!=0)yield return new("stress","Resident distressed","Inspect needs and memories; pain, grief and poor schedules lower mood.",EntityId:stressed);
        if(Danger.Raid.Phase=="Recovery")yield return new("recovery","Post-raid recovery","Automatic raids are suppressed during the recovery window.","Danger");
    }
}
