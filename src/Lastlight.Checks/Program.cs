using Lastlight.Core;
using System.Text.Json;

string assets = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "Assets", "definitions.json");
var definitions = Definitions.Load(assets);
int passed = 0;
void Check(string name, Action action)
{
    action(); passed++; Console.WriteLine("PASS " + name);
}
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Reject(Action action)
{
    bool rejected = false;
    try { action(); } catch (InvalidDataException) { rejected = true; }
    Require(rejected, "Invalid data was accepted.");
}
string temporary = Path.Combine(Path.GetTempPath(), "LastlightChecks-" + Guid.NewGuid());
Directory.CreateDirectory(temporary);
try
{
    if(args.Contains("--release-only") || args.Contains("--release-stress-only")){ReleaseAcceptance.Run(definitions,args.Length>1?args[1]:temporary,Path.GetFullPath(assets),args.Contains("--release-stress-only"));return 0;}
    if(args.Contains("--solar-finales-only")){SolarAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets,true);return 0;}
    if(args.Contains("--solar-only")){SolarAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    if(args.Contains("--solar-edges-only")){SolarEdges.Run(definitions,args.Length>1?args[1]:temporary);return 0;}
    if(args.Contains("--institution-edges-only")){InstitutionEdges.Run(definitions,args.Length>1?args[1]:temporary);return 0;}
    if(args.Contains("--institutions-only")){InstitutionAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    if(args.Contains("--region-edges-only")){RegionEdges.Run(definitions,args.Length>1?args[1]:temporary);return 0;}
    if(args.Contains("--region-only")){RegionAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    if(args.Contains("--physical-edges-only")){PhysicalEdges.Run(definitions,args.Length>1?args[1]:temporary);return 0;}
    if(args.Contains("--physical-only")){PhysicalAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    if(args.Contains("--slice-edges-only")){SliceEdges.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    if(args.Contains("--slice-only")){SliceAcceptance.Run(definitions,args.Length>1?args[1]:temporary);return 0;}
    if(args.Contains("--danger-only")){DangerAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);return 0;}
    Check("A01: content validation rejects duplicate IDs and missing recipe references", () =>
    {
        var copy = JsonSerializer.Deserialize<ContentDocument>(JsonSerializer.Serialize(definitions.Document, JsonFormat.Options), JsonFormat.Options)!;
        copy.Entities.Add(copy.Entities[0]); Reject(() => new Definitions(copy));
        copy.Entities.RemoveAt(copy.Entities.Count - 1);
        copy.Recipes.Add(new("broken", "missing", 1, new() { ["wood"] = 1 })); Reject(() => new Definitions(copy));
    });
    Check("A01: schema versions and invalid material values fail with diagnostics", () =>
    {
        var copy = JsonSerializer.Deserialize<ContentDocument>(JsonSerializer.Serialize(definitions.Document, JsonFormat.Options), JsonFormat.Options)!;
        copy.SchemaVersion = 99; Reject(() => new Definitions(copy));
        copy.SchemaVersion = 1; copy.Materials.Add(new("bad", "Bad", float.NaN, 1, 1)); Reject(() => new Definitions(copy));
    });
    Check("A02/A04: seed reproducibility, different seeds, five named settlers", () =>
    {
        var a = WorldGenerator.Create(definitions, 74219); var b = WorldGenerator.Create(definitions, 74219);
        Require(SaveStore.Fingerprint(a) == SaveStore.Fingerprint(b), "Same seed generated different state.");
        Require(SaveStore.Fingerprint(a) != SaveStore.Fingerprint(WorldGenerator.Create(definitions, 74220)), "Different seeds match.");
        Require(a.Entities.Count(e => e.DefinitionId == "settler") == 5, "Initial settlers missing.");
        a.Validate(definitions);
    });
    Check("A03: pause, speed tiers, single step and bounded backlog", () =>
    {
        var w = WorldGenerator.Create(definitions, 1); var clock = new SimulationClock();
        clock.Advance(w, definitions, 1); Require(w.Tick == 0, "Paused clock advanced.");
        clock.SetSpeed(1); for (int i = 0; i < 30; i++) clock.Advance(w, definitions, 1.0 / 30);
        Require(w.Tick == 30, "Normal speed tick count.");
        clock.SetSpeed(3); for (int i = 0; i < 30; i++) clock.Advance(w, definitions, 1.0 / 30);
        Require(w.Tick == 120, "3x speed tick count.");
        clock.SetSpeed(6); clock.Advance(w, definitions, 10);
        Require(clock.LastFrameTicks <= 45, "Frame backlog exceeded bound.");
        clock.Paused = true; long before = w.Tick; w.Step(definitions); Require(w.Tick == before + 1, "Single step failed.");
    });
    Check("A03/A04: ordered commands, invalid terrain, stacks, stable nonreused IDs", () =>
    {
        var w = WorldGenerator.Create(definitions, 2);
        long next = w.NextEntityId;
        w.Enqueue(new(CommandKind.Spawn, DefinitionId: "wood", X: 50, Y: 64, Quantity: 75));
        w.Enqueue(new(CommandKind.Spawn, DefinitionId: "wood", X: 51, Y: 64, Quantity: 76));
        var results = w.FlushCommands(definitions);
        Require(results[0].Accepted && !results[1].Accepted, "Ordered results wrong.");
        Require(results[0].EntityId == next, "Allocator mismatch.");
        Require(!w.Apply(new(CommandKind.Spawn, DefinitionId: "settler", X: -1), definitions).Accepted, "Out-of-bounds accepted.");
        Require(!w.Apply(new(CommandKind.Spawn, DefinitionId: "missing", X: 64, Y: 64), definitions).Accepted, "Unknown definition accepted.");
        w.Apply(new(CommandKind.Remove, next), definitions);
        var recreated = w.Apply(new(CommandKind.Spawn, DefinitionId: "wood", X: 50, Y: 64), definitions);
        Require(recreated.EntityId > next, "Deleted ID reused.");
        w.Validate(definitions);
    });
    Check("A04: occupancy and teleport restrictions", () =>
    {
        var w = WorldGenerator.Create(definitions, 3);
        var wall = w.Apply(new(CommandKind.Spawn, DefinitionId: "wall", X: 50, Y: 64), definitions);
        Require(wall.Accepted, "Wall setup failed.");
        var person = w.Entities.First(e => e.DefinitionId == "settler");
        Require(!w.Apply(new(CommandKind.Teleport, person.Id, X: 50, Y: 64), definitions).Accepted, "Person entered wall.");
        Require(!w.Apply(new(CommandKind.PaintTerrain, DefinitionId: "water", X: person.X, Y: person.Y), definitions).Accepted, "Terrain stranded person.");
        Require(w.Apply(new(CommandKind.Teleport, person.Id, X: 51, Y: 64), definitions).Accepted, "Valid teleport rejected.");
    });
    Check("A05: map, people, clock, pending commands, RNG and view round-trip", () =>
    {
        var w = WorldGenerator.Create(definitions, 4);
        for (int i = 0; i < 1234; i++) w.Step(definitions);
        w.Enqueue(new(CommandKind.Spawn, DefinitionId: "grain", X: 51, Y: 64, Quantity: 11));
        var view = new ViewState { CameraX = 50, CameraY = 60, Zoom = .8f, SelectedEntityId = w.Entities.First().Id };
        string path = Path.Combine(temporary, "world.lastlight");
        SaveStore.Write(path, w, view, definitions);
        var loaded = SaveStore.Read(path, definitions);
        Require(SaveStore.Fingerprint(w) == SaveStore.Fingerprint(loaded.World), "Authoritative state differs.");
        Require(loaded.View.Zoom == view.Zoom && loaded.View.SelectedEntityId == view.SelectedEntityId, "View differs.");
        w.Step(definitions); loaded.World.Step(definitions);
        Require(SaveStore.Fingerprint(w) == SaveStore.Fingerprint(loaded.World), "Post-load command execution differs.");
    });
    Check("A05: previous backup, corruption rejection, safe failed writes", () =>
    {
        var w = WorldGenerator.Create(definitions, 5);
        var view = new ViewState { CameraX = 64, CameraY = 64 };
        string path = Path.Combine(temporary, "backup.lastlight");
        SaveStore.Write(path, w, view, definitions); string first = SaveStore.Fingerprint(w);
        w.Step(definitions); SaveStore.Write(path, w, view, definitions);
        Require(SaveStore.Fingerprint(SaveStore.Read(path + ".bak", definitions).World) == first, "Backup lost previous state.");
        string valid = File.ReadAllText(path);
        w.NextEntityId = 0; Reject(() => SaveStore.Write(path, w, view, definitions));
        Require(File.ReadAllText(path) == valid, "Failed save damaged valid slot.");
        File.WriteAllText(path, valid.Replace("Sha256", "Corrupt").Replace("sha256", "corrupt"));
        Reject(() => SaveStore.Read(path, definitions));
    });
    Check("A05: future save schema and missing definitions rejected", () =>
    {
        var w = WorldGenerator.Create(definitions, 6); string path = Path.Combine(temporary, "version.lastlight");
        SaveStore.Write(path, w, new ViewState { CameraX = 64, CameraY = 64 }, definitions);
        string text = File.ReadAllText(path); File.WriteAllText(path, text.Replace($"\"formatVersion\":{SaveStore.FormatVersion}", "\"formatVersion\":99"));
        Reject(() => SaveStore.Read(path, definitions));
        w.Cells[0].TerrainId = "unknown"; Reject(() => w.Validate(definitions));
    });
    Check("A05: deterministic stream continuation", () =>
    {
        var w = WorldGenerator.Create(definitions, 7); string path = Path.Combine(temporary, "rng.lastlight");
        var random = new StableRandom(w.JobRandomState); for (int i = 0; i < 10; i++) random.Next(); w.JobRandomState = random.State;
        SaveStore.Write(path, w, new ViewState { CameraX = 64, CameraY = 64 }, definitions);
        var saved = SaveStore.Read(path, definitions); var restored = new StableRandom(saved.World.JobRandomState);
        for (int i = 0; i < 100; i++) Require(random.Next() == restored.Next(), "RNG stream continuation differs.");
    });
    Check("A01/A05: malformed null collections rejected with readable errors", () =>
    {
        var copy = JsonSerializer.Deserialize<ContentDocument>(JsonSerializer.Serialize(definitions.Document, JsonFormat.Options), JsonFormat.Options)!;
        copy.Terrain = null!; Reject(() => new Definitions(copy));
        var w = WorldGenerator.Create(definitions, 8); w.Cells = null!; Reject(() => w.Validate(definitions));
    });
    Check("B03: rotated footprints, overlapping plans and cancellation preserve resources", () =>
    {
        var w = WorldGenerator.Create(definitions, 9);
        int supply = w.Entities.Where(e => e.DefinitionId == "wood").Sum(e => e.Quantity);
        var placed = w.Apply(new(CommandKind.CreateBlueprint, DefinitionId: "bed", X: 50, Y: 64, Rotation: 1, Text: "timber"), definitions);
        Require(placed.Accepted, "Blueprint rejected.");
        var cells = World.BlueprintCells(w.Colony.Blueprints.Single()).ToList();
        Require(cells.Contains((51,64)) && !cells.Contains((50,65)), "Rotation did not change footprint.");
        Require(!w.Apply(new(CommandKind.CreateBlueprint, DefinitionId: "wall", X: 51, Y: 64, Text: "basalt"), definitions).Accepted, "Overlapping blueprint allowed.");
        Require(w.Entities.Where(e => e.DefinitionId == "wood").Sum(e => e.Quantity) == supply, "Planning consumed materials.");
        Require(w.Apply(new(CommandKind.CancelBlueprint, placed.EntityId), definitions).Accepted && w.Colony.Blueprints.Count == 0, "Cancel failed.");
        w.Validate(definitions);
    });
    Check("B03: stockpile filters, forbidden breakdown and independent zone copy", () =>
    {
        var w = WorldGenerator.Create(definitions, 10);
        var wood = w.Entities.Single(e => e.DefinitionId == "wood");
        var z1 = w.Apply(new(CommandKind.CreateZone, DefinitionId: "Stockpile", X: wood.X, Y: wood.Y), definitions);
        Require(z1.Accepted && w.Resources("wood").Stored == 40, "Stockpile count is wrong.");
        w.Apply(new(CommandKind.EditZone, z1.EntityId, Quantity: 0, Text: "wood"), definitions);
        Require(w.Resources("wood").Stored == 0, "Exclusion filter ignored.");
        var z2 = w.Apply(new(CommandKind.CreateZone, DefinitionId: "Stockpile", X: wood.X+3, Y: wood.Y), definitions);
        w.Apply(new(CommandKind.EditZone, z2.EntityId, Quantity: (int)z1.EntityId, Text: "copy"), definitions);
        w.Apply(new(CommandKind.EditZone, z1.EntityId, Quantity: 1, Text: "wood"), definitions);
        Require(!w.Colony.Zones.Single(z => z.Id == z2.EntityId).AllowWood, "Zone copy shares mutable state.");
        w.Apply(new(CommandKind.Forbid, wood.Id, Quantity: 1), definitions);
        Require(w.Resources("wood") is { Available: 0, Forbidden: 40, Stored: 0 }, "Forbidden resources counted as usable.");
        w.Validate(definitions);
    });
    Check("B04: priorities, drag schedule ranges, independent presets and area assignment", () =>
    {
        var w = WorldGenerator.Create(definitions, 11); var e = w.Entities.First(e => e.DefinitionId == "settler"); var p = w.Colony.People[e.Id];
        w.Apply(new(CommandKind.SetPriority, e.Id, X: 6, Quantity: 1), definitions);
        w.Apply(new(CommandKind.SetSchedule, e.Id, X: 8, Width: 4, Quantity: (int)ScheduleActivity.Study), definitions);
        Require(p.Priorities[6] == 1 && p.Schedule.Skip(8).Take(4).All(s => s == ScheduleActivity.Study), "Matrix updates failed.");
        Require(!w.Apply(new(CommandKind.SetSchedule, e.Id, X: 23, Width: 4, Quantity: 99), definitions).Accepted, "Invalid schedule accepted.");
        w.Apply(new(CommandKind.ClonePolicy, DefinitionId: "food-default"), definitions); var copy = w.Colony.Policies[^1];
        w.Apply(new(CommandKind.EditPolicy, DefinitionId: copy.Id, Quantity: 0, Text: "Duskgrain"), definitions);
        w.Apply(new(CommandKind.AssignPolicy, e.Id, DefinitionId: copy.Id, Text: "Food"), definitions);
        Require(w.Colony.Policies.First(q => q.Id == "food-default").Values["Duskgrain"] == 1 && p.Policies["Food"] == copy.Id, "Policy copy or assignment failed.");
        var zone = w.Apply(new(CommandKind.CreateZone, DefinitionId: "Allowed", X: 50, Y: 60, Width: 5, Height: 5), definitions);
        w.Apply(new(CommandKind.AssignPolicy, e.Id, Quantity: (int)zone.EntityId, Text: "Area"), definitions);
        w.Apply(new(CommandKind.RemoveZone, zone.EntityId), definitions); Require(p.AllowedZoneId == 0, "Removed area left dangling reference.");
        w.Validate(definitions);
    });
    Check("B02/B05: drafting, letters, alerts and invalid context actions", () =>
    {
        var w = WorldGenerator.Create(definitions, 12); var e = w.Entities.First(e => e.DefinitionId == "settler");
        w.Apply(new(CommandKind.Draft, e.Id, Quantity: 1), definitions); Require(w.Colony.People[e.Id].Drafted, "Draft command failed.");
        foreach (long id in w.Colony.People.Keys) w.Apply(new(CommandKind.SetPriority, id, X: 2, Quantity: 0), definitions);
        Require(w.Alerts().Any(a => a.Id == "doctor" && a.Tab == "Work"), "Doctor alert missing navigation.");
        w.Apply(new(CommandKind.AcknowledgeLetter, w.Colony.Letters[0].Id, Quantity: 1), definitions);
        Require(w.Colony.Letters[0].Read && w.Colony.Letters[0].Archived, "Letter state failed.");
        Require(!w.Apply(new(CommandKind.Designate, DefinitionId: "Mine", X: e.X, Y: e.Y), definitions).Accepted, "Invalid mining allowed.");
    });
    Check("B05: all interface-facing state survives version 2 save/load", () =>
    {
        var w = WorldGenerator.Create(definitions, 13); var people = w.Entities.Where(e => e.DefinitionId == "settler").Take(2).Select(e => e.Id).ToList();
        w.Apply(new(CommandKind.CreateBlueprint, DefinitionId: "wall", X: 50, Y: 64, Text: "basalt"), definitions);
        w.Apply(new(CommandKind.CreateZone, DefinitionId: "Home", X: 52, Y: 64, Width: 2, Height: 2), definitions);
        w.Apply(new(CommandKind.SetResearch, DefinitionId: "restoration"), definitions);
        w.Apply(new(CommandKind.SetQuest, DefinitionId: "Accepted"), definitions);
        string path = Path.Combine(temporary, "interface.lastlight");
        SaveStore.Write(path, w, new ViewState { CameraX=64, CameraY=64, SelectedEntityId=people[0], SelectedEntityIds=people, OpenTab="Work", ShowGrid=true, ShowZones=false }, definitions);
        var loaded = SaveStore.Read(path, definitions);
        Require(SaveStore.Fingerprint(w) == SaveStore.Fingerprint(loaded.World) && loaded.View.SelectedEntityIds.SequenceEqual(people) && loaded.View.OpenTab == "Work" && !loaded.View.ShowZones, "Interface snapshot differs.");
    });
    Check("B05: authentic Stage A sample migrates without altering world facts", () =>
    {
        string sample = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assets))!, "../../../tests/fixtures/foundation.lastlight");
        if (!File.Exists(sample)) throw new Exception("Stage A sample fixture missing: " + sample);
        var raw = JsonSerializer.Deserialize<SaveEnvelope>(File.ReadAllText(sample), JsonFormat.Options)!;
        Require(raw.FormatVersion == 1, "Migration fixture is not Stage A.");
        using var document = JsonDocument.Parse(raw.Payload); var old = document.RootElement.GetProperty("world");
        var loaded = SaveStore.Read(sample, definitions).World;
        Require(loaded.Tick == old.GetProperty("tick").GetInt64() && loaded.Seed == old.GetProperty("seed").GetInt32() &&
            loaded.Entities.Count == old.GetProperty("entities").GetArrayLength() && loaded.Cells.Count == old.GetProperty("cells").GetArrayLength() &&
            loaded.NextEntityId == old.GetProperty("nextEntityId").GetInt64() && loaded.GenerationRandomState == old.GetProperty("generationRandomState").GetUInt64(), "Migration changed foundation facts.");
        Require(loaded.Colony.Initialized && loaded.Colony.People.Count == 5, "Migration did not initialize interface state.");
    });
    AutonomyAcceptance.Run(definitions, args.Length > 1 ? args[1] : temporary);
    DangerAcceptance.Run(definitions,args.Length>1?args[1]:temporary,assets);
    Console.WriteLine($"ALL {passed} FOUNDATION/INTERFACE AND STAGES C/D CHECKS PASSED");
    return 0;
}
catch (Exception exception) { Console.Error.WriteLine("FAIL " + exception); return 1; }
finally { Directory.Delete(temporary, recursive: true); }
