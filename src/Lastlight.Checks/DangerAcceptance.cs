using Lastlight.Core;
using System.Text.Json;
public static class DangerAcceptance
{
    public static void Run(Definitions d,string output,string assets)
    {
        void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        void Steps(World w,int n){for(int i=0;i<n;i++)w.Step(d);}
        World Camp()
        {
            string file=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assets))!,"../../../tests/fixtures/autonomous-colony.lastlight");
            var world=SaveStore.Read(file,d).World;world.SetupDangerSupplies(d);world.Danger.RaidsEnabled=false;return world;
        }
        void Save(string name,World w)=>SaveStore.Write(Path.Combine(output,name),w,new ViewState{CameraX=w.Width/2,CameraY=w.Height/2},d);
        void Equip(World w,long id,string definition)
        {
            var item=w.Entities.First(e=>e.DefinitionId==definition && !e.Forbidden && !w.Autonomy.Claims.ContainsKey("item:"+e.Id));
            var result=w.Apply(new(CommandKind.Equip,id,TargetId:item.Id),d);Require(result.Accepted,result.Message);
            for(int i=0;i<10000 && w.Autonomy.Jobs.Any(j=>j.PawnId==id && j.Kind=="Equip");i++)w.Step(d);
            Require(!w.Autonomy.Jobs.Any(j=>j.PawnId==id && j.Kind=="Equip"),"Equipment job stalled.");
        }
        var anatomy=Camp();long injured=anatomy.Colony.People.Keys.First();
        double originalMove=anatomy.Capacity(injured).Moving;
        anatomy.InflictWound(injured,"left-leg","Fracture",30,0,d);
        anatomy.InflictWound(injured,"right-arm","Cut",24,0,d);
        Require(anatomy.Capacity(injured).Moving<originalMove*.6 && anatomy.Capacity(injured).Manipulation<.7,"Injuries did not reduce capacities.");
        Require(anatomy.BleedingPerHour(injured)>0,"Cut did not cause bleeding.");
        Save("injury-capacity.lastlight",anatomy);Console.WriteLine("PASS D01: linked anatomy reduces movement/manipulation and wounds bleed");
        var medicine=Camp();var people=medicine.Colony.People.Keys.ToArray();long doctor=people[0],patient=people[1];
        foreach(long id in people)medicine.Colony.People[id].Priorities[2]=id==doctor?1:0;
        medicine.Colony.People[doctor].Skills[2]=15;
        int stock=medicine.Resources("medicine").Total;
        medicine.InflictWound(patient,"left-arm","Cut",18,0,d);
        for(int i=0;i<12000 && medicine.Danger.Treatments==0;i++)medicine.Step(d);
        Require(medicine.Danger.Treatments==1 && medicine.Body(patient)!.Wounds.All(w=>w.Tended),"Doctor did not tend wounds.");
        Require(medicine.Resources("medicine").Total==stock-1,"Tending did not consume exactly one medicine.");
        Require(medicine.Danger.People[patient].Memories.Any(m=>m.Kind=="Treated") && medicine.Danger.People[patient].Relationships[doctor].Opinion>=15,"Treatment memory or bond missing.");
        double bleeding=medicine.BleedingPerHour(patient);Require(bleeding<.05,"Tending did not arrest bleeding.");
        Steps(medicine,World.TicksPerDay*2);Require(medicine.Body(patient)!.Wounds.Count==0 && medicine.Find(patient)!.Health==100,"Recoverable injury did not heal.");
        Console.WriteLine("PASS D01-D03: autonomous tending consumes medicine, stops bleeding, creates memory/opinion and heals");
        var interrupted=Camp();people=interrupted.Colony.People.Keys.ToArray();doctor=people[0];patient=people[1];
        foreach(long id in people)interrupted.Colony.People[id].Priorities[2]=id==doctor?1:0;
        interrupted.InflictWound(patient,"left-arm","Cut",10,0,d);
        int allMedicine=interrupted.Resources("medicine").Total;
        for(int i=0;i<10000 && !interrupted.Autonomy.Jobs.Any(j=>j.PawnId==doctor && j.Cargo.ContainsKey("medicine"));i++)interrupted.Step(d);
        Require(interrupted.Autonomy.Jobs.Any(j=>j.PawnId==doctor && j.Cargo.ContainsKey("medicine")),"No medical material transport found.");
        interrupted.Apply(new(CommandKind.Draft,doctor,Quantity:1),d);
        Require(interrupted.Resources("medicine").Total==allMedicine && interrupted.Danger.Treatments==0,"Interrupted medical job lost medicine or fabricated treatment.");
        Console.WriteLine("PASS D01: interrupted medicine transport refunds supplies without treating");
        var infection=Camp();patient=infection.Colony.People.Keys.First();
        foreach(var p in infection.Colony.People.Values)p.Priorities[2]=0;
        infection.Apply(new(CommandKind.EditPolicy,DefinitionId:"medicine-default",Quantity:0,Text:"Care level"),d);
        infection.InflictWound(patient,"left-arm","Burn",6,0,d);Steps(infection,World.TicksPerDay*2);
        Require(infection.Danger.Treatments==0 && infection.Body(patient)!.Wounds.Any(w=>w.Infected && w.Infection>0),"Untended wound did not become infected under a no-care policy.");
        Save("infection-demo.lastlight",infection);Console.WriteLine("PASS D01: no-care policy prevents treatment; untreated burns become infected");
        var rescue=Camp();people=rescue.Colony.People.Keys.ToArray();doctor=people[0];patient=people[1];
        foreach(long id in people)rescue.Colony.People[id].Priorities[2]=0;
        rescue.Apply(new(CommandKind.Draft,doctor,Quantity:1),d);
        rescue.InflictWound(patient,"left-leg","Fracture",30,0,d);rescue.InflictWound(patient,"right-leg","Fracture",30,0,d);
        Require(rescue.Body(patient)!.Downed,"Patient did not become downed.");
        var bed=rescue.Entities.FirstOrDefault(e=>e.DefinitionId=="bed");
        if(bed==null){var b=rescue.Apply(new(CommandKind.Spawn,DefinitionId:"bed",X:rescue.Width/2-10,Y:rescue.Height/2+1),d);Require(b.Accepted,b.Message);bed=rescue.Find(b.EntityId)!;}
        rescue.Apply(new(CommandKind.MedicalBed,bed.Id,Quantity:1),d);
        var ordered=rescue.Apply(new(CommandKind.Rescue,doctor,TargetId:patient),d);Require(ordered.Accepted,ordered.Message);
        int moves=0;bool carried=false;
        for(int i=0;i<12000 && rescue.Danger.Rescues==0;i++)
        {
            rescue.Step(d);if(rescue.Body(patient)!.CarriedBy==doctor){carried=true;Require(rescue.Find(patient)!.X==rescue.Find(doctor)!.X && rescue.Find(patient)!.Y==rescue.Find(doctor)!.Y,"Carried patient did not follow rescuer.");moves++;}
        }
        Require(carried && moves>0 && rescue.Danger.Rescues==1 && rescue.Find(patient)!.X==bed.X && rescue.Find(patient)!.Y==bed.Y,"Rescue did not deliver to bed exactly once.");
        Require(rescue.Body(patient)!.CarriedBy==0 && rescue.Danger.People[patient].Relationships[doctor].Opinion>=20,"Rescue release or friendship failed.");
        Save("rescue-demo.lastlight",rescue);Console.WriteLine("PASS D01/D03/D04: physical rescue delivers a downed resident and creates friendship");
        var sight=Camp();people=sight.Colony.People.Keys.ToArray();long archer=people[0];var actor=sight.Find(archer)!;
        sight.Apply(new(CommandKind.Teleport,archer,X:20,Y:30),d);
        var enemy=sight.Apply(new(CommandKind.Spawn,DefinitionId:"raider",X:25,Y:30),d);Require(enemy.Accepted,enemy.Message);
        sight.Body(archer)!.WeaponId="bow";sight.Body(archer)!.Ammo=3;
        var wall=sight.Apply(new(CommandKind.Spawn,DefinitionId:"wall",X:22,Y:30),d);Require(wall.Accepted,wall.Message);
        Require(!sight.HasLineOfSight(actor,sight.Find(enemy.EntityId)!,d) && !sight.TryAttack(archer,enemy.EntityId,d) && sight.Body(archer)!.Ammo==3,"Shot crossed a wall or consumed ammo while blocked.");
        sight.Apply(new(CommandKind.Remove,wall.EntityId),d);Require(sight.TryAttack(archer,enemy.EntityId,d) && sight.Body(archer)!.Ammo==2,"Valid shot did not spend ammunition.");
        Console.WriteLine("PASS D04: range/LOS and physical ammunition gate attacks");
        var equipment=Camp();long fighter=equipment.Colony.People.Keys.First();int bows=equipment.Resources("bow").Total;
        Equip(equipment,fighter,"bow");Equip(equipment,fighter,"arrows");Equip(equipment,fighter,"padded-armor");
        Require(equipment.Body(fighter)!.WeaponId=="bow" && equipment.Body(fighter)!.Ammo==30 && equipment.Resources("bow").Total==bows-1,"Physical equipment pickup failed.");
        Equip(equipment,fighter,"spear");Require(equipment.Resources("bow").Total==bows && equipment.Body(fighter)!.WeaponId=="spear","Weapon swap lost gear.");
        Console.WriteLine("PASS D04: equipment/armor/ammunition pickups and swaps conserve stock");
        var fallen=equipment.Apply(new(CommandKind.Spawn,DefinitionId:"raider",X:equipment.Width/2+2,Y:equipment.Height/2+1),d);Require(fallen.Accepted,fallen.Message);
        equipment.Body(fallen.EntityId)!.WeaponId="bow";equipment.Body(fallen.EntityId)!.WeaponCondition=87;equipment.Body(fallen.EntityId)!.Ammo=6;
        equipment.InflictWound(fallen.EntityId,"torso","Cut",80,fighter,d);Require(equipment.Body(fallen.EntityId)!.Dead,"Fatal trauma did not cause death.");
        int loose=equipment.Resources("bow").Total;
        var salvage=equipment.Apply(new(CommandKind.Equip,fighter,Text:"loot",TargetId:fallen.EntityId),d);Require(salvage.Accepted,salvage.Message);
        for(int i=0;i<6000 && equipment.Autonomy.Jobs.Any(j=>j.Kind=="Loot");i++)equipment.Step(d);
        Require(equipment.Resources("bow").Total==loose+1 && equipment.Entities.Any(e=>e.DefinitionId=="bow" && e.Health==87),"Corpse salvage lost gear condition or duplicated stock.");
        Require(!equipment.Apply(new(CommandKind.Equip,fighter,Text:"loot",TargetId:fallen.EntityId),d).Accepted,"Empty corpse yielded repeated gear.");
        Console.WriteLine("PASS D01/D04: fatal trauma persists; physical corpse salvage preserves condition exactly once");
        var raid=Camp();people=raid.Colony.People.Keys.ToArray();fighter=people[0];doctor=people[4];
        Equip(raid,fighter,"spear");Equip(raid,fighter,"padded-armor");
        foreach(long id in people){raid.Apply(new(CommandKind.Draft,id,Quantity:1),d);raid.Colony.People[id].Priorities[2]=id==doctor?1:0;}
        int cx=raid.Width/2,cy=raid.Height/2;
        raid.Apply(new(CommandKind.Teleport,fighter,X:cx,Y:cy+1),d);
        for(int i=1;i<people.Length;i++)raid.Apply(new(CommandKind.Teleport,people[i],X:cx-9-i,Y:cy+1),d);
        var warning=raid.Apply(new(CommandKind.TriggerRaid,X:cx+3,Y:cy+1,Quantity:1,Text:"scenario"),d);Require(warning.Accepted,warning.Message);
        Save("raid-ready.lastlight",raid);
        long initialPause=raid.Danger.PauseSerial;
        bool combatSaved=false;
        for(int i=0;i<18000 && raid.Danger.Raid.Phase!="Recovery";i++)
        {raid.Step(d);if(!combatSaved && raid.Danger.Raid.Phase=="Active" && raid.Body(fighter)!.Wounds.Count>0){Save("combat-in-progress.lastlight",raid);combatSaved=true;}}
        Require(raid.Danger.Raid.Phase=="Recovery" && raid.Danger.PauseSerial>initialPause,"Raid did not arrive, resolve and enter recovery.");
        var wounded=people.Where(id=>raid.Body(id)!.Wounds.Count>0).ToArray();
        Require(wounded.Length>0 && wounded.All(id=>!raid.Body(id)!.Dead),"Raid failed to produce recoverable resident wounds.");
        Require(raid.Danger.History.Any(e=>e.Kind=="Injury" && raid.Danger.Raid.RaiderIds.Contains(e.ActorId) && people.Contains(e.OtherId)),"Wounds were not caused by the raid.");
        Save("raid-aftermath.lastlight",raid);
        Require(!raid.Apply(new(CommandKind.TriggerRaid,Quantity:1),d).Accepted,"Recovery window allowed another raid.");
        foreach(long id in people)if(raid.CanAct(id))raid.Apply(new(CommandKind.Draft,id,Quantity:0),d);
        var active=SaveStore.Read(Path.Combine(output,"raid-aftermath.lastlight"),d).World;
        // Resume both from the same battle checkpoint with matching undraft commands.
        foreach(long id in people)if(active.CanAct(id))active.Apply(new(CommandKind.Draft,id,Quantity:0),d);
        Steps(raid,3000);Steps(active,3000);
        Require(SaveStore.Fingerprint(raid)==SaveStore.Fingerprint(active),"Post-raid save/load continuation diverged.");
        Require(raid.Danger.Treatments>0,"Post-raid doctor did not treat injured residents.");
        Save("recovery-in-progress.lastlight",raid);
        Steps(raid,World.TicksPerDay*3);
        Require(wounded.All(id=>raid.Body(id)!.Wounds.Count==0 && raid.Find(id)!.Health==100),"Raid survivors did not recover.");
        Require(wounded.All(id=>raid.Danger.People[id].Memories.Any(m=>m.Kind is "Treated" or "SurvivedRaid")),"Survivors did not retain the raid/treatment memory.");
        Save("recovered-colony.lastlight",raid);Console.WriteLine($"PASS D05 EXIT GATE: raid causes {wounded.Length} wounded survivors, autonomous treatment, full recovery and lasting memories");
        var psychology=Camp();long distressed=psychology.Colony.People.Keys.First();
        foreach(var p in psychology.Colony.People.Values)p.Drafted=true;
        for(int i=0;i<8;i++)psychology.Remember(distressed,"Grief"+i,"A painful loss remains on my mind.",-12,0,10);
        Steps(psychology,World.TicksPerHour*8);
        Require(psychology.Danger.People[distressed].WithdrawalUntil>psychology.Tick && psychology.Colony.People[distressed].Mood<20,"Sustained distress did not cause withdrawal.");
        Console.WriteLine("PASS D02: memories affect mood and sustained distress causes a nonviolent withdrawal");
        var craft=Camp();int bx=craft.Width/2+8,by=craft.Height/2+2;
        var frame=craft.Apply(new(CommandKind.CreateBlueprint,DefinitionId:"workbench",X:bx,Y:by,Text:"timber"),d);Require(frame.Accepted,frame.Message);
        for(int i=0;i<World.TicksPerDay*2 && !craft.Entities.Any(e=>e.DefinitionId=="workbench");i++)craft.Step(d);
        var bench=craft.Entities.FirstOrDefault(e=>e.DefinitionId=="workbench");Require(bench!=null,"Crafting bench was not constructed.");
        var reagents=craft.Apply(new(CommandKind.Spawn,DefinitionId:"fungus",X:bx,Y:by+2,Quantity:4),d);Require(reagents.Accepted,reagents.Message);
        var recipe=craft.Apply(new(CommandKind.EditBill,bench!.Id,"d-medicine",Text:"add"),d);Require(recipe.Accepted,recipe.Message);
        craft.Apply(new(CommandKind.EditBill,recipe.EntityId,Quantity:1,Text:"mode"),d);craft.Apply(new(CommandKind.EditBill,recipe.EntityId,Quantity:2,Text:"remaining"),d);
        var order=craft.Autonomy.Bills.First(b=>b.Id==recipe.EntityId);int reserve=craft.Resources("medicine").Total;
        for(int i=0;i<World.TicksPerDay*2 && order.Remaining>0;i++)craft.Step(d);
        Require(order.Remaining==0 && craft.Resources("medicine").Total==reserve+2,"Production bills did not replenish medicine from physical ingredients.");
        Save("crafting-medicine.lastlight",craft);Console.WriteLine("PASS D01/C05: a constructed bench replenishes medicine from fungus through real bills");
        var corrupt=Camp();corrupt.Body(corrupt.Colony.People.Keys.First())!.Blood=double.NaN;bool rejected=false;
        try{corrupt.Validate(d);}catch(InvalidDataException){rejected=true;}Require(rejected,"Malformed health data was accepted.");
        Console.WriteLine("PASS D01-D05: malformed body state rejected");
        File.WriteAllText(Path.Combine(output,"stage-d-acceptance.txt"),$"Stage D exit gate passed. Seed 74219, five residents. Raid wounded {wounded.Length} resident(s); treatments {raid.Danger.Treatments}; raids completed {raid.Danger.RaidsCompleted}. Recoverable injuries, memories, physical rescue, capacity effects, ammo/equipment, LOS, recovery exclusion and deterministic continuation verified.\n");
    }
}
