using Lastlight.Core;
using System.Diagnostics;
using System.Text.Json;

public static class ReleaseAcceptance
{
    private static void Require(bool value,string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action,string message) { try { action(); } catch (InvalidDataException) { return; } throw new Exception(message); }
    public static void Run(Definitions d,string output,string assets,bool stress)
    {
        Directory.CreateDirectory(output);
        string samples=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assets)!,"../../../tests/fixtures"));
        if (!stress)
        {
            int migrated=0;
            foreach(string sample in Directory.GetFiles(samples,"*.lastlight").OrderBy(p=>p,StringComparer.Ordinal))
            {
                var old=SaveStore.Read(sample,d); string path=Path.Combine(output,"migration.lastlight");
                SaveStore.Write(path,old.World,old.View,d);var read=SaveStore.Read(path,d);
                Require(SaveStore.Fingerprint(old.World)==SaveStore.Fingerprint(read.World),"Migration roundtrip differs: "+sample);
                for(int n=0;n<90;n++){old.World.Step(d);read.World.Step(d);}
                Require(SaveStore.Fingerprint(old.World)==SaveStore.Fingerprint(read.World),"Migration continuation differs: "+sample);
                migrated++;
            }
            Console.WriteLine("PASS J03: "+migrated+" authentic prior checkpoints migrate, roundtrip and continue identically");
            string directory=Path.Combine(output,"mod-test");Directory.CreateDirectory(Path.Combine(directory,"Mods"));
            string basePath=Path.Combine(directory,"definitions.json");File.Copy(assets,basePath,true);
            void Order(params string[] packs)=>File.WriteAllText(Path.Combine(directory,"Mods","load-order.json"),JsonSerializer.Serialize(packs,JsonFormat.Options));
            var pack=new ContentPack {Id="release-example",Version="1",Content=new()};
            pack.Content.Recipes.Add(new("mod-ration","meal",1,new(){{"grain",2}}));
            File.WriteAllText(Path.Combine(directory,"Mods","example.json"),JsonSerializer.Serialize(pack,JsonFormat.Options));Order("example.json");
            var modded=Definitions.Load(basePath);Require(modded.Document.Recipes.Any(r=>r.Id=="mod-ration")&&modded.LoadedPacks.Single()=="release-example@1"&&modded.ManifestHash!=d.ManifestHash,"Pack did not load or hash");
            var world=WorldGenerator.Create(modded,74219,64,64);world.Tick=World.TicksPerHour*8;
            foreach(var pawn in world.Colony.People.Values){pawn.Priorities=Enumerable.Repeat(0,16).ToArray();pawn.Priorities[6]=1;pawn.Schedule=Enumerable.Repeat(ScheduleActivity.Work,24).ToArray();}
            var stove=world.Apply(new(CommandKind.Spawn,DefinitionId:"stove",X:30,Y:34),modded);Require(stove.Accepted,stove.Message);
            var bill=world.Apply(new(CommandKind.EditBill,stove.EntityId,"mod-ration",Text:"add"),modded);Require(bill.Accepted,bill.Message);
            for(int tick=0;tick<9000&&world.Autonomy.Bills.Single().Completed==0;tick++)world.Step(modded);
            Require(world.Autonomy.Bills.Single().Completed>0,"Mod recipe never produced real output");string save=Path.Combine(output,"modded.lastlight");SaveStore.Write(save,world,new(),modded);SaveStore.Read(save,modded);
            Reject(()=>SaveStore.Read(save,d),"Missing pack was silently accepted");
            Order("../example.json");Reject(()=>Definitions.Load(basePath),"Pack traversal accepted");
            Order("example.json","example.json");Reject(()=>Definitions.Load(basePath),"Duplicate load order accepted");
            Order("example.json");pack.Requires["missing"]="1";File.WriteAllText(Path.Combine(directory,"Mods","example.json"),JsonSerializer.Serialize(pack,JsonFormat.Options));Reject(()=>Definitions.Load(basePath),"Missing dependency accepted");
            pack.Requires.Clear();pack.Content.Recipes[0]=new("mod-ration","missing-output",1,new(){{"grain",2}});File.WriteAllText(Path.Combine(directory,"Mods","example.json"),JsonSerializer.Serialize(pack,JsonFormat.Options));Reject(()=>Definitions.Load(basePath),"Dangling recipe accepted");
            Console.WriteLine("PASS J01/J03: additive content load order, dependency diagnostics, references, manifest save compatibility and traversal rejection");
            // Solar scarcity must affect the actual reserve ledger, not just the explanation.
            var solar=SolarWorldGenerator.Create(d,74219);var faction=solar.Region.Factions.First();var town=solar.Region.Sites.First(s=>s.Kind=="Town"&&s.FactionId==faction.Id);solar.Region.Tiles[town.Y*12+town.X].Climate=20;
            solar.Solar.Scarcity=100;solar.Solar.Stage=4;solar.Solar.Mode="Custom";solar.Solar.NextBoundary=World.TicksPerDay*20;
            int grain=faction.Reserves["grain"];solar.Tick=World.TicksPerDay-1;solar.Step(d);
            Require(faction.Reserves["grain"]<grain+5-Math.Max(1,faction.Population/30),"Scarcity did not debit production");
            Console.WriteLine("PASS J02: solar scarcity applies to real faction food reserves");
            return;
        }
        foreach(string name in new[]{"autonomous-day-ten","solar-vessel-finale"})
        {
            var w=SaveStore.Read(Path.Combine(samples,name+".lastlight"),d).World;
            w.Profile=new();
            int Rations(World colony)=>colony.Entities.Where(e=>e.DefinitionId is "meal" or "grain").Sum(e=>e.Quantity)+colony.Autonomy.Jobs.Sum(j=>j.Cargo.GetValueOrDefault("meal")+j.Cargo.GetValueOrDefault("grain"));
            int initialRations=Rations(w);
            int initialMeals=w.Colony.People.Values.Sum(p=>p.MealsEaten),initialHarvest=w.Autonomy.Harvested;
            long initial=w.Tick;var watch=Stopwatch.StartNew();var rows=new List<object>();int priorMeals=initialMeals,priorHarvest=initialHarvest;
            // Every tick executes; there is no clock jump or annual replenishment.
            for(int year=1;year<=10;year++)
            {
                var lap=Stopwatch.StartNew();for(long tick=0;tick<World.TicksPerDay*30L;tick++)w.Step(d);lap.Stop();
                w.Validate(d);int meals=w.Colony.People.Values.Sum(p=>p.MealsEaten),harvest=w.Autonomy.Harvested;
                if(name=="autonomous-day-ten") { Require(Rations(w)==initialRations+6*(harvest-initialHarvest)-(meals-initialMeals),"Grain/meal conservation failed in year "+year);Require(w.Colony.People.Values.All(p=>p.Food>0),"Ten-year colony starved in year "+year);Require(meals>priorMeals&&harvest>priorHarvest,"Food/growing jobs stalled in year "+year); }
                if(name=="solar-vessel-finale")Require(w.Solar.Evacuees.All(id=>w.Body(id) is {Dead:false})&&w.Colony.People.Values.Sum(p=>p.MealsEaten)==initialMeals,"Evacuated residents consumed home supplies or died");
                string path=Path.Combine(output,name+"-year-"+year+".lastlight");SaveStore.Write(path,w,new(),d);var copy=SaveStore.Read(path,d).World;
                Require(SaveStore.Fingerprint(w)==SaveStore.Fingerprint(copy),"Annual save differs");
                for(int tick=0;tick<300;tick++){w.Step(d);copy.Step(d);}Require(SaveStore.Fingerprint(w)==SaveStore.Fingerprint(copy),"Annual continuation diverged");
                rows.Add(new {year,tick=w.Tick,seconds=lap.Elapsed.TotalSeconds,meals=meals-priorMeals,harvest=harvest-priorHarvest,pathFailures=w.Autonomy.PathFailures,entities=w.Entities.Count,claims=w.Autonomy.Claims.Count,jobs=w.Autonomy.Jobs.Count,managedBytes=GC.GetTotalMemory(false)});
                Console.WriteLine($"PASS J02/J05 {name} year {year}: {lap.Elapsed.TotalSeconds:0.00}s; meals +{meals-priorMeals}, harvest +{harvest-priorHarvest}, paths {w.Autonomy.PathFailures}, entities {w.Entities.Count}");priorMeals=meals;priorHarvest=harvest;
            }
            watch.Stop();Require(w.Tick-initial>=World.TicksPerDay*300L,"Less than ten simulated years");
            File.WriteAllText(Path.Combine(output,name+"-profile.json"),JsonSerializer.Serialize(new {name,seed=w.Seed,ticks=w.Tick-initial,seconds=watch.Elapsed.TotalSeconds,years=rows,systems=w.Profile.Systems},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
