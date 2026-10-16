namespace Lastlight.Core;

public enum ScheduleActivity { Anything, Work, Sleep, Recreation, Study }
public enum ZoneKind { Stockpile, Growing, Home, Allowed }
public sealed record Buildable(string Id, string Name, string Category, int Width, int Height, int Cost, string Material);
public static class ColonyCatalog
{
    public static readonly string[] Work = ["Firefight", "Patient", "Doctor", "Basic", "Warden", "Handle", "Cook", "Hunt", "Construct", "Grow", "Mine", "Craft", "Art", "Haul", "Clean", "Research"];
    public static readonly string[] PolicyKinds = ["Food", "Clothing", "Medicine", "Substances", "Equipment", "Formulae"];
    public static readonly List<Buildable> Buildings =
    [
        new("wall", "Wall", "Structure", 1, 1, 5, "basalt"),
        new("door", "Door", "Structure", 1, 1, 12, "timber"),
        new("fence", "Fence", "Structure", 1, 1, 3, "timber"),
        new("bed", "Bed", "Furniture", 1, 2, 35, "timber"),
        new("table", "Table", "Furniture", 2, 2, 40, "timber"),
        new("stool", "Stool", "Furniture", 1, 1, 10, "timber"),
        new("shelf", "Shelf", "Furniture", 1, 2, 25, "timber"),
        new("stove", "Fueled stove", "Production", 2, 1, 50, "basalt"),
        new("workbench", "Crafting bench", "Production", 2, 1, 45, "timber"),
        new("study", "Study desk", "Production", 2, 1, 30, "timber"),
        new("floor", "Flagstone floor", "Floors", 1, 1, 2, "basalt"),
        new("woodfloor", "Wood floor", "Floors", 1, 1, 2, "timber"),
        new("barricade", "Barricade", "Security", 1, 1, 12, "timber"),
        new("trap", "Spike trap", "Security", 1, 1, 25, "timber"),
        new("torch", "Torch", "Temperature", 1, 1, 8, "timber"),
        new("hearth", "Hearth", "Temperature", 2, 1, 25, "basalt"),
        new("generator", "Hand generator", "Power", 2, 2, 60, "sun-brass"),
        new("conduit", "Conduit", "Power", 1, 1, 2, "sun-brass"),
        new("circle", "Preparation circle", "Ritual", 3, 3, 40, "basalt"),
        new("hitch", "Caravan hitch", "Transport", 1, 1, 10, "timber"),
        new("stairs", "Stairs", "Structure", 1, 1, 40, "timber"),
        new("column", "Support column", "Structure", 1, 1, 10, "basalt"),
        new("roof", "Constructed roof", "Structure", 1, 1, 5, "timber"),
        new("bridge", "Bridge floor", "Floors", 1, 1, 5, "timber"),
        new("sunlamp", "Dawn reflector", "Furniture", 1, 1, 20, "sun-brass"),
        new("f-floodgate", "Floodgate", "Temperature", 1, 1, 20, "sun-brass"),
        new("f-pump", "Electric water pump", "Temperature", 1, 1, 20, "sun-brass"),
        new("f-battery", "Battery", "Power", 1, 1, 20, "sun-brass"),
        new("f-heater", "Electric room heater", "Temperature", 1, 1, 20, "sun-brass"),
        new("f-vent", "Heat vent", "Temperature", 1, 1, 8, "sun-brass"),
        new("f-solar", "Solar generator", "Power", 1, 1, 20, "sun-brass"),
        new("f-shaft", "Mechanical shaft", "Floors", 1, 1, 8, "sun-brass"),
        new("f-wheel", "Waterwheel", "Temperature", 1, 1, 20, "sun-brass"),
        new("f-mill", "Mechanical mill", "Production", 1, 1, 20, "sun-brass"),
        new("f-resonator", "Resonance source", "Power", 1, 1, 20, "sun-brass"),
        new("f-channel", "Thaumic channel", "Floors", 1, 1, 8, "sun-brass"),
        new("f-ward", "Containment ward", "Temperature", 1, 1, 20, "sun-brass"),
        new("f-smelter", "Electric smelter", "Production", 1, 1, 20, "sun-brass"),
        new("f-kiln", "Glass kiln", "Production", 1, 1, 20, "sun-brass"),
        new("f-loom", "Loom", "Production", 1, 1, 20, "sun-brass"),
        new("f-fermenter", "Fermentation vat", "Production", 1, 1, 20, "sun-brass"),
        new("f-filter", "Water filter", "Temperature", 1, 1, 20, "sun-brass")
    ];
    public static Buildable? Find(string id) => Buildings.Find(b => b.Id == id);
}
public sealed class PawnProfile
{
    public long EntityId { get; set; }
    public double Food { get; set; } = 1000;
    public double Rest { get; set; } = 1000;
    public double Recreation { get; set; } = 1000;
    public string Activity { get; set; } = "Idle";
    public long RetryAt { get; set; }
    public int MealsEaten { get; set; }
    public int Age { get; set; }
    public string Background { get; set; } = "Refugee";
    public string Trait { get; set; } = "Practical";
    public int Mood { get; set; } = 75;
    public bool Drafted { get; set; }
    public int[] Priorities { get; set; } = Enumerable.Repeat(3, ColonyCatalog.Work.Length).ToArray();
    public int[] Skills { get; set; } = new int[ColonyCatalog.Work.Length];
    public ScheduleActivity[] Schedule { get; set; } = Enumerable.Repeat(ScheduleActivity.Anything, 24).ToArray();
    public Dictionary<string, string> Policies { get; set; } = new();
    public long AllowedZoneId { get; set; }
}
public sealed class PolicyPreset
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, int> Values { get; set; } = new();
}
public sealed class Blueprint
{
    public Dictionary<string,int> Delivered { get; set; } = new();
    public int WorkProgress { get; set; }
    public long Id { get; set; }
    public string BuildableId { get; set; } = "";
    public string MaterialId { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Rotation { get; set; }
}
public sealed class Zone
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public ZoneKind Kind { get; set; }
    public HashSet<int> Cells { get; set; } = [];
    public int Priority { get; set; } = 2;
    public bool AllowWood { get; set; } = true;
    public bool AllowGrain { get; set; } = true;
    public bool AllowForbidden { get; set; }
    public bool AllowDamaged { get; set; } = true;
    public string Crop { get; set; } = "Duskgrain";
}
public sealed record Designation(long Id, string Kind, int X, int Y, int Z);
public sealed class ColonyLetter
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Read { get; set; }
    public bool Archived { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
public sealed class AnimalProfile
{
    public long EntityId { get; set; }
    public long HandlerId { get; set; }
    public bool Tame { get; set; }
    public bool Train { get; set; }
    public bool Breed { get; set; } = true;
    public bool Slaughter { get; set; }
    public int Medical { get; set; } = 1;
}
public sealed class ColonyState
{
    public bool Initialized { get; set; }
    public bool ManualPriorities { get; set; } = true;
    public long NextObjectId { get; set; } = 1;
    public Dictionary<long, PawnProfile> People { get; set; } = new();
    public Dictionary<long, AnimalProfile> Animals { get; set; } = new();
    public List<PolicyPreset> Policies { get; set; } = [];
    public List<Blueprint> Blueprints { get; set; } = [];
    public List<Zone> Zones { get; set; } = [];
    public List<Designation> Designations { get; set; } = [];
    public List<ColonyLetter> Letters { get; set; } = [];
    public string ResearchProject { get; set; } = "";
    public string QuestState { get; set; } = "Offered";
    public bool TrackQuest { get; set; }

    public void Initialize(World world, Definitions definitions)
    {
        if (Initialized) return;
        foreach (var kind in ColonyCatalog.PolicyKinds)
        {
            var defaults = kind switch
            {
                "Food" => new Dictionary<string, int> { ["Duskgrain"] = 1, ["Raw food"] = 1 },
                "Clothing" => new() { ["Min condition"] = 50, ["Max quality"] = 100 },
                "Medicine" => new() { ["Care level"] = 2, ["Self tend"] = 1 },
                "Substances" => new() { ["Recreation use"] = 0, ["Minimum age"] = 18 },
                "Equipment" => new() { ["Carry weapon"] = 1, ["Avoid damaged"] = 1 },
                _ => new() { ["Auto preparation"] = 0, ["Strain limit"] = 50 }
            };
            Policies.Add(new() { Id = kind.ToLowerInvariant() + "-default", Kind = kind, Name = "Default", Values = defaults });
            Policies.Add(new() { Id = kind.ToLowerInvariant() + "-restricted", Kind = kind, Name = "Restricted", Values = defaults.ToDictionary(p => p.Key, p => p.Value) });
            var second = Policies[^1];
            if (kind == "Food") second.Values["Raw food"] = 0;
            else if (kind == "Medicine") second.Values["Care level"] = 1;
            else if (kind == "Clothing") second.Values["Min condition"] = 80;
            else if (kind == "Equipment") second.Values["Carry weapon"] = 0;
            else if (kind == "Formulae") second.Values["Strain limit"] = 25;
            else second.Values["Minimum age"] = 21;
        }
        Initialized = true;
        EnsureEntities(world, definitions);
        Letters.Add(new()
        {
            Id = NextObjectId++, Title = "The Observatory Refuge",
            Body = "Five refugees have reached an ancient observatory. Plan rooms, mark supplies, and organize their work. The daylight engine is dormant. Assign work, establish a growing zone, build a stove and add a meal bill to secure food.",
            X = world.Width / 2, Y = world.Height / 2
        });
    }
    public void EnsureEntities(World world, Definitions definitions)
    {
        foreach (var e in world.Entities)
        {
            var kind = definitions.Entities[e.DefinitionId].Kind;
            if (kind == EntityKind.Person && !People.ContainsKey(e.Id))
            {
                var random = new StableRandom((ulong)e.Id * 7937 + (uint)world.Seed + 17);
                var p = new PawnProfile { EntityId = e.Id, Age = 21 + random.Next(35), Trait = new[] { "Practical", "Curious", "Patient", "Independent", "Steadfast" }[(int)(e.Id % 5)] };
                p.Skills = Enumerable.Range(0, ColonyCatalog.Work.Length).Select(_ => 2 + random.Next(12)).ToArray();
                for (int hour = 0; hour < 24; hour++)
                    p.Schedule[hour] = hour < 6 || hour >= 22 ? ScheduleActivity.Sleep : hour is 6 or 7 or 20 or 21 ? ScheduleActivity.Recreation : ScheduleActivity.Anything;
                p.Priorities[0] = 1; p.Priorities[1] = 1; p.Priorities[2] = 2;
                p.Policies = ColonyCatalog.PolicyKinds.ToDictionary(k => k, k => k.ToLowerInvariant() + "-default");
                People.Add(e.Id, p);
            }
            if (kind == EntityKind.Creature && e.DefinitionId != "raider" && !Animals.ContainsKey(e.Id)) Animals.Add(e.Id, new() { EntityId = e.Id });
        }
    }
    public void Validate(World world, Definitions definitions)
    {
        if (!Initialized) throw new InvalidDataException("Colony interface state is not initialized.");
        if (People is null || Animals is null || Policies is null || Blueprints is null || Zones is null || Designations is null || Letters is null)
            throw new InvalidDataException("Null colony collection.");
        if (NextObjectId < 1 || NextObjectId >= int.MaxValue || Blueprints.Count > 10000 || Zones.Count > 256 || Designations.Count > 10000 || Policies.Count > 128 || Letters.Count > 128)
            throw new InvalidDataException("Colony state exceeds limits.");
        var policyIds = new HashSet<string>();
        foreach (var p in Policies)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Id) || !policyIds.Add(p.Id) || !ColonyCatalog.PolicyKinds.Contains(p.Kind) ||
                string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 32 || p.Values == null || p.Values.Count > 16 || p.Values.Values.Any(v => v is < 0 or > 100))
                throw new InvalidDataException("Invalid policy preset.");
        }
        foreach (var pair in People)
        {
            var p = pair.Value;
            if (p == null || world.Find(pair.Key) is not { } e || definitions.Entities[e.DefinitionId].Kind != EntityKind.Person || p.EntityId != pair.Key ||
                p.Priorities == null || p.Priorities.Length != ColonyCatalog.Work.Length || p.Priorities.Any(v => v is < 0 or > 4) ||
                p.Skills == null || p.Skills.Length != ColonyCatalog.Work.Length || p.Skills.Any(v => v is < 0 or > 20) ||
                p.Schedule == null || p.Schedule.Length != 24 || p.Schedule.Any(v => !Enum.IsDefined(v)) || p.Mood is < 0 or > 100 ||
                p.Policies == null || p.Policies.Count != ColonyCatalog.PolicyKinds.Length || p.Age is < 0 or > 200 ||
                string.IsNullOrWhiteSpace(p.Background) || string.IsNullOrWhiteSpace(p.Trait))
                throw new InvalidDataException("Invalid pawn profile.");
            foreach (var kind in ColonyCatalog.PolicyKinds)
                if (!p.Policies.TryGetValue(kind, out var id) || !Policies.Any(q => q.Id == id && q.Kind == kind)) throw new InvalidDataException("Missing assigned policy.");
            if (p.AllowedZoneId != 0 && !Zones.Any(z => z.Id == p.AllowedZoneId && z.Kind == ZoneKind.Allowed)) throw new InvalidDataException("Missing allowed area.");
        }
        if (world.Entities.Count(e => definitions.Entities[e.DefinitionId].Kind == EntityKind.Person) != People.Count)
            throw new InvalidDataException("Missing settler profiles.");
        foreach (var pair in Animals)
            if (pair.Value == null || world.Find(pair.Key) is not { } e || definitions.Entities[e.DefinitionId].Kind != EntityKind.Creature || pair.Value.EntityId != pair.Key ||
                pair.Value.Medical is < 0 or > 3 || pair.Value.HandlerId != 0 && !People.ContainsKey(pair.Value.HandlerId)) throw new InvalidDataException("Invalid animal policy.");
        var objectIds = new HashSet<long>();
        void Id(long id) { if (id < 1 || id >= NextObjectId || !objectIds.Add(id)) throw new InvalidDataException("Invalid colony object ID."); }
        var occupied = new HashSet<(int, int, int)>();
        foreach (var b in Blueprints)
        {
            if (b == null) throw new InvalidDataException("Null blueprint.");
            Id(b.Id);
            var d = ColonyCatalog.Find(b.BuildableId);
            if (d == null || !definitions.Document.Materials.Any(m => m.Id == b.MaterialId) || b.Rotation is < 0 or > 3) throw new InvalidDataException("Invalid blueprint definition.");
            foreach (var cell in World.BlueprintCells(b))
                if (!world.Contains(cell.X, cell.Y, b.Z) || (!definitions.Terrain[world.CellAt(cell.X, cell.Y, b.Z).TerrainId].Walkable && !(b.BuildableId=="bridge" && world.CellAt(cell.X,cell.Y,b.Z).TerrainId=="void")) ||
                    !occupied.Add((cell.X, cell.Y, b.Z))) throw new InvalidDataException("Invalid blueprint footprint.");
        }
        foreach (var z in Zones)
        {
            if (z == null) throw new InvalidDataException("Null zone.");
            Id(z.Id);
            if (!Enum.IsDefined(z.Kind) || string.IsNullOrWhiteSpace(z.Name) || z.Name.Length > 32 || z.Cells == null || z.Cells.Count == 0 ||
                z.Cells.Any(i => i < 0 || i >= world.Cells.Count) || z.Priority is < 0 or > 4 || z.Crop is not ("Duskgrain" or "Fungus" or "Herbs"))
                throw new InvalidDataException("Invalid zone.");
        }
        foreach (var d in Designations)
        {
            if (d == null) throw new InvalidDataException("Null designation.");
            Id(d.Id); if (!world.Contains(d.X, d.Y, d.Z) || !World.DesignationKinds.Contains(d.Kind)) throw new InvalidDataException("Invalid designation.");
        }
        foreach (var l in Letters)
        {
            if (l == null) throw new InvalidDataException("Null letter.");
            Id(l.Id); if (l.Title == null || l.Title.Length > 80 || l.Body == null || l.Body.Length > 2048 || !world.Contains(l.X, l.Y, 0)) throw new InvalidDataException("Invalid letter.");
        }
        if (ResearchProject is not ("" or "restoration" or "agriculture" or "formulae") || QuestState is not ("Offered" or "Accepted" or "Declined"))
            throw new InvalidDataException("Invalid research or quest selection.");
    }
}

public sealed record ColonyAlert(string Id, string Label, string Details, string Tab = "", long EntityId = 0);
public sealed record ResourceTotals(string DefinitionId, int Stored, int Available, int Forbidden, int Total);
public sealed partial class World
{
    public static readonly string[] DesignationKinds = ["Mine", "Haul", "Hunt", "Tame", "Deconstruct", "Plan", "Clear"];
    public static IEnumerable<(int X, int Y)> BlueprintCells(Blueprint blueprint)
    {
        var d = ColonyCatalog.Find(blueprint.BuildableId)!;
        int width = blueprint.Rotation % 2 == 0 ? d.Width : d.Height;
        int height = blueprint.Rotation % 2 == 0 ? d.Height : d.Width;
        for (int y = blueprint.Y; y < blueprint.Y + height; y++)
        for (int x = blueprint.X; x < blueprint.X + width; x++) yield return (x, y);
    }
    public string? BlueprintError(string id, string material, int x, int y, int z, int rotation, Definitions definitions)
    {
        var d = ColonyCatalog.Find(id);
        if (d == null || !definitions.Document.Materials.Any(m => m.Id == material) || rotation is < 0 or > 3) return "Unknown building, material or rotation.";
        var candidate = new Blueprint { BuildableId = id, X = x, Y = y, Z = z, Rotation = rotation };
        foreach (var cell in BlueprintCells(candidate))
        {
            if (!Contains(cell.X, cell.Y, z)) return "Footprint is outside the map.";
            if (!definitions.Terrain[CellAt(cell.X, cell.Y, z).TerrainId].Walkable && !(id=="bridge" && CellAt(cell.X,cell.Y,z).TerrainId=="void")) return "Cannot build on impassable terrain.";
            if (id!="roof" && Entities.Any(e => definitions.Entities[e.DefinitionId].Kind == EntityKind.Building && BuildingCells(e).Contains((cell.X,cell.Y,z)))) return "An existing building blocks the footprint.";
            if (Colony.Blueprints.Any(b => b.Z == z && BlueprintCells(b).Contains(cell))) return "Another blueprint occupies this tile.";
        }
        if(id=="stairs" && (z+1>=Levels || !definitions.Terrain[CellAt(x,y,z+1).TerrainId].Walkable || Occupied(x,y,z+1,0,definitions)))return "Excavate a clear landing on the floor below first.";
        return null;
    }
    public IEnumerable<ColonyAlert> Alerts()
    {
        foreach(var alert in CampaignAlerts())yield return alert;
        foreach(var alert in PhysicalAlerts())yield return alert;
        foreach(var alert in SliceAlerts())yield return alert;
        foreach(var alert in DangerAlerts()) yield return alert;
        if (Colony.People.Values.Any(p => p.Food < 250)) yield return new("hungry", "Hungry residents", "Check reachable allowed food, food policies and cooking bills.", "Work");
        if (Colony.People.Values.Any(p => p.Activity == "No reachable work")) yield return new("stranded", "No reachable work", "Check walls, allowed areas, materials and priorities.", "Work");
        if (!Colony.People.Any(p => p.Value.Priorities[2] > 0)) yield return new("doctor", "No doctor assigned", "Enable Doctor work for at least one settler.", "Work");
        if (!Colony.People.Any(p => p.Value.Priorities[6] > 0)) yield return new("cook", "No cook assigned", "Enable Cook work for at least one settler.", "Work");
        var hurt = Entities.FirstOrDefault(e => Colony.People.ContainsKey(e.Id) && e.Health < 50);
        if (hurt != null) yield return new("injury", "Settler needs treatment", "Select the injured settler to inspect health.", EntityId: hurt.Id);
        if (!Colony.Zones.Any(z => z.Kind == ZoneKind.Stockpile)) yield return new("storage", "No stockpile zone", "Draw a stockpile in Architect > Zones.", "Architect");
        if (!Colony.Zones.Any(z => z.Kind == ZoneKind.Growing)) yield return new("growing", "No growing zone", "Draw a growing zone on fertile soil.", "Architect");
        if (Colony.Blueprints.Count > 0) yield return new("construction", $"{Colony.Blueprints.Count} pending blueprints", "Workers reserve materials and build reachable plans. Inspect needs and priorities if work stalls.", "Architect");
    }
    public ResourceTotals Resources(string id)
    {
        int stored = 0, available = 0, forbidden = 0, total = 0;
        foreach (var e in Entities.Where(e => e.DefinitionId == id))
        {
            total += e.Quantity;
            if (e.Forbidden) forbidden += e.Quantity; else available += e.Quantity;
            int index = (e.Z * Height + e.Y) * Width + e.X;
            if (Colony.Zones.Any(z => z.Kind == ZoneKind.Stockpile && z.Cells.Contains(index) && ZoneAccepts(z, e))) stored += e.Quantity;
        }
        return new(id, stored, available, forbidden, total);
    }
    public static bool ZoneAccepts(Zone zone, Entity e) => (e.DefinitionId == "wood" ? zone.AllowWood : e.DefinitionId is "grain" or "meal" or "fungus" ? zone.AllowGrain : true) &&
        (!e.Forbidden || zone.AllowForbidden) && (zone.AllowDamaged || e.Health == 100);
    private CommandResult ApplyColonyCommand(WorldCommand c, Definitions definitions)
    {
        CommandResult No(string reason) => new(false, reason);
        CommandResult Yes(string message, long id = 0) { Record(message); return new(true, message, id); }
        if (!Colony.Initialized) return No("Colony state is unavailable.");
        if(c.Kind==CommandKind.CampaignAction)return ApplyCampaignCommand(c,definitions);
        if(c.Kind==CommandKind.InstitutionAction)return ApplyInstitutionCommand(c,definitions);
        if(c.Kind==CommandKind.RegionAction)return ApplyRegionCommand(c,definitions);
        if(c.Kind==CommandKind.PhysicalAction)return ApplyPhysicalCommand(c,definitions);
        if(c.Kind>=CommandKind.SliceAction)return ApplySliceCommand(c,definitions);
        if (c.Kind >= CommandKind.CombatOrder) return ApplyDangerCommand(c, definitions);
        if (c.Kind == CommandKind.MoveDrafted) return ApplyMoveCommand(c, definitions);
        if (c.Kind == CommandKind.EditBill) return ApplyBillCommand(c, definitions);
        if (c.Kind == CommandKind.SetWorkMode) { Colony.ManualPriorities = c.Quantity != 0; return Yes("Work priority mode changed."); }
        if (c.Kind is CommandKind.SetPriority or CommandKind.SetSchedule or CommandKind.AssignPolicy or CommandKind.Draft)
        {
            if (!Colony.People.TryGetValue(c.EntityId, out var pawn)) return No("Select a settler.");
            if (c.Kind == CommandKind.SetPriority)
            {
                if (c.X < 0 || c.X >= ColonyCatalog.Work.Length || c.Quantity is < 0 or > 4) return No("Invalid work priority.");
                pawn.Priorities[c.X] = c.Quantity; return Yes($"Work: {Find(c.EntityId)!.Name} / {ColonyCatalog.Work[c.X]} = {c.Quantity}.");
            }
            if (c.Kind == CommandKind.SetSchedule)
            {
                if (c.X < 0 || c.Width < 1 || c.X + c.Width > 24 || !Enum.IsDefined((ScheduleActivity)c.Quantity)) return No("Invalid schedule cells.");
                for (int h = c.X; h < c.X + c.Width; h++) pawn.Schedule[h] = (ScheduleActivity)c.Quantity;
                return Yes($"Schedule updated for {Find(c.EntityId)!.Name}.");
            }
            if (c.Kind == CommandKind.Draft) { if (!CanAct(c.EntityId)) return No("This resident is dead, downed or overwhelmed."); pawn.Drafted = c.Quantity != 0; Danger.Orders.Remove(c.EntityId); Interrupt(c.EntityId, definitions); return Yes(pawn.Drafted ? "Settler drafted." : "Settler undrafted."); }
            if (c.Text == "Area")
            {
                if (c.Quantity != 0 && !Colony.Zones.Any(z => z.Id == c.Quantity && z.Kind == ZoneKind.Allowed)) return No("Unknown allowed area.");
                pawn.AllowedZoneId = c.Quantity; return Yes("Allowed area assigned.");
            }
            var policy = Colony.Policies.Find(p => p.Id == c.DefinitionId && p.Kind == c.Text);
            if (policy == null) return No("Policy does not match this assignment.");
            pawn.Policies[c.Text] = policy.Id; return Yes($"{c.Text} policy assigned to {Find(c.EntityId)!.Name}.");
        }
        if (c.Kind == CommandKind.EditPolicy)
        {
            var policy = Colony.Policies.Find(p => p.Id == c.DefinitionId);
            if (policy == null || !policy.Values.ContainsKey(c.Text) || c.Quantity is < 0 or > 100) return No("Invalid policy value.");
            int max = c.Text == "Care level" ? 3 : c.Text is "Min condition" or "Max quality" or "Minimum age" or "Strain limit" ? 100 : 1;
            if (c.Quantity > max) return No("Policy value exceeds its valid range.");
            policy.Values[c.Text] = c.Quantity; return Yes($"Updated {policy.Kind} / {policy.Name}.");
        }
        if (c.Kind == CommandKind.ClonePolicy)
        {
            var p = Colony.Policies.Find(p => p.Id == c.DefinitionId);
            if (p == null || Colony.Policies.Count >= 128) return No("Policy cannot be copied.");
            string id = p.Kind.ToLowerInvariant() + "-custom-" + Colony.NextObjectId++;
            string name = p.Name.Length > 27 ? p.Name[..27] + " copy" : p.Name + " copy";
            Colony.Policies.Add(new() { Id = id, Kind = p.Kind, Name = name, Values = p.Values.ToDictionary(k => k.Key, k => k.Value) });
            return Yes("Independent policy copy created.");
        }
        if (c.Kind == CommandKind.Rename)
        {
            if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 32 || !c.Text.All(ch => ch is >= ' ' and <= '~')) return No("Use 1-32 printable characters.");
            if (c.DefinitionId == "zone")
            {
                var zone = Colony.Zones.Find(z => z.Id == c.EntityId); if (zone == null) return No("Zone no longer exists."); zone.Name = c.Text;
            }
            else if (c.DefinitionId.StartsWith("policy:", StringComparison.Ordinal))
            {
                var policy = Colony.Policies.Find(p => p.Id == c.DefinitionId[7..]); if (policy == null) return No("Policy no longer exists."); policy.Name = c.Text;
            }
            else { var e = Find(c.EntityId); if (e == null) return No("Entity no longer exists."); e.Name = c.Text; }
            return Yes("Name updated.");
        }
        if (c.Kind == CommandKind.Forbid)
        {
            var e = Find(c.EntityId); if (e == null) return No("Entity no longer exists.");
            e.Forbidden = c.Quantity != 0; return Yes(e.Forbidden ? "Object forbidden." : "Object allowed.");
        }
        if (c.Kind == CommandKind.CreateBlueprint)
        {
            if (Colony.Blueprints.Count >= 10000) return No("Blueprint capacity reached.");
            string? reason = BlueprintError(c.DefinitionId, c.Text, c.X, c.Y, c.Z, c.Rotation, definitions);
            if (reason != null) return No(reason);
            var b = new Blueprint { Id = Colony.NextObjectId++, BuildableId = c.DefinitionId, MaterialId = c.Text, X = c.X, Y = c.Y, Z = c.Z, Rotation = c.Rotation };
            Colony.Blueprints.Add(b); return Yes($"{ColonyCatalog.Find(b.BuildableId)!.Name} blueprint placed.", b.Id);
        }
        if (c.Kind == CommandKind.CancelBlueprint)
        {
            var b = Colony.Blueprints.Find(b => b.Id == c.EntityId); if (b == null) return No("Blueprint no longer exists.");
            foreach(var supply in b.Delivered) Drop(supply.Key,supply.Value,Index(b.X,b.Y,b.Z),definitions);
            Colony.Blueprints.Remove(b); return Yes("Blueprint cancelled; delivered materials returned.");
        }
        if (c.Kind == CommandKind.CreateZone)
        {
            if (!Enum.TryParse<ZoneKind>(c.DefinitionId, out var kind) || !Enum.IsDefined(kind) || c.Width < 1 || c.Height < 1 ||
                c.Width > Width || c.Height > Height || !Contains(c.X, c.Y, c.Z) || !Contains(c.X + c.Width - 1, c.Y + c.Height - 1, c.Z)) return No("Invalid zone rectangle.");
            if (Colony.Zones.Count >= 256) return No("Zone capacity reached.");
            var zone = new Zone { Id = Colony.NextObjectId, Name = $"{kind} {Colony.NextObjectId}", Kind = kind };
            for (int y = c.Y; y < c.Y + c.Height; y++) for (int x = c.X; x < c.X + c.Width; x++)
            {
                var cell = CellAt(x, y, c.Z);
                if (kind == ZoneKind.Growing ? cell.TerrainId == "soil" : kind is ZoneKind.Home or ZoneKind.Allowed || definitions.Terrain[cell.TerrainId].Walkable)
                    zone.Cells.Add((c.Z * Height + y) * Width + x);
            }
            if (zone.Cells.Count == 0) return No("No suitable tiles in this area.");
            Colony.NextObjectId++; Colony.Zones.Add(zone); return Yes($"{kind} zone created.", zone.Id);
        }
        if (c.Kind == CommandKind.RemoveZone)
        {
            var z = Colony.Zones.Find(z => z.Id == c.EntityId); if (z == null) return No("Zone no longer exists.");
            Colony.Zones.Remove(z); foreach (var p in Colony.People.Values) if (p.AllowedZoneId == z.Id) p.AllowedZoneId = 0;
            return Yes("Zone removed; affected settlers are unrestricted.");
        }
        if (c.Kind == CommandKind.EditZone)
        {
            var z = Colony.Zones.Find(z => z.Id == c.EntityId); if (z == null) return No("Zone no longer exists.");
            switch (c.Text)
            {
                case "priority" when c.Quantity is >= 0 and <= 4: z.Priority = c.Quantity; break;
                case "wood": z.AllowWood = c.Quantity != 0; break;
                case "grain": z.AllowGrain = c.Quantity != 0; break;
                case "forbidden": z.AllowForbidden = c.Quantity != 0; break;
                case "damaged": z.AllowDamaged = c.Quantity != 0; break;
                case "crop" when c.DefinitionId is "Duskgrain" or "Fungus" or "Herbs": z.Crop = c.DefinitionId; break;
                case "copy":
                    var other = Colony.Zones.Find(q => q.Id == c.Quantity && q.Kind == z.Kind);
                    if (other == null) return No("Source zone settings do not match.");
                    z.Priority = other.Priority; z.AllowWood = other.AllowWood; z.AllowGrain = other.AllowGrain; z.AllowForbidden = other.AllowForbidden; z.AllowDamaged = other.AllowDamaged; z.Crop = other.Crop; break;
                default: return No("Invalid zone setting.");
            }
            return Yes("Zone settings updated.");
        }
        if (c.Kind == CommandKind.Designate)
        {
            if (!Contains(c.X, c.Y, c.Z) || !DesignationKinds.Contains(c.DefinitionId)) return No("Invalid designation.");
            if (Colony.Designations.Count >= 10000) return No("Designation capacity reached.");
            if (Colony.Designations.Any(d => d.X == c.X && d.Y == c.Y && d.Z == c.Z && d.Kind == c.DefinitionId)) return No("Designation already exists.");
            if (c.DefinitionId == "Mine" && CellAt(c.X, c.Y, c.Z).TerrainId != "stone") return No("There is no rock to mine.");
            if (c.DefinitionId == "Deconstruct" && !Entities.Any(e => e.X == c.X && e.Y == c.Y && e.Z == c.Z && definitions.Entities[e.DefinitionId].Kind == EntityKind.Building)) return No("No building to deconstruct.");
            if (c.DefinitionId is "Hunt" or "Tame" && !Entities.Any(e => e.X == c.X && e.Y == c.Y && e.Z == c.Z && definitions.Entities[e.DefinitionId].Kind == EntityKind.Creature)) return No("No creature at this tile.");
            if (c.DefinitionId == "Haul" && !Entities.Any(e => e.X == c.X && e.Y == c.Y && e.Z == c.Z && definitions.Entities[e.DefinitionId].Kind == EntityKind.ItemStack && !e.Forbidden)) return No("No allowed item to haul.");
            Colony.Designations.Add(new(Colony.NextObjectId++, c.DefinitionId, c.X, c.Y, c.Z)); return Yes(c.DefinitionId + " designation added.");
        }
        if (c.Kind == CommandKind.CancelDesignation)
        {
            int count = Colony.Designations.RemoveAll(d => d.X == c.X && d.Y == c.Y && d.Z == c.Z); return count == 0 ? No("No designations at this tile.") : Yes("Designations cancelled.");
        }
        if (c.Kind == CommandKind.AcknowledgeLetter)
        {
            var l = Colony.Letters.Find(l => l.Id == c.EntityId); if (l == null) return No("Letter no longer exists.");
            l.Read = true; l.Archived = c.Quantity != 0; return Yes(l.Archived ? "Letter archived." : "Letter read.");
        }
        if (c.Kind == CommandKind.SetAnimalPolicy)
        {
            if (!Colony.Animals.TryGetValue(c.EntityId, out var a)) return No("Select an animal.");
            switch (c.Text)
            {
                case "handler" when c.Quantity == 0 || Colony.People.ContainsKey(c.Quantity): a.HandlerId = c.Quantity; break;
                case "train": a.Train = c.Quantity != 0; break;
                case "breed": a.Breed = c.Quantity != 0; break;
                case "slaughter": a.Slaughter = c.Quantity != 0; break;
                case "medical" when c.Quantity is >= 0 and <= 3: a.Medical = c.Quantity; break;
                default: return No("Invalid animal policy.");
            }
            return Yes("Animal policy updated.");
        }
        if (c.Kind == CommandKind.SetResearch)
        {
            if (c.DefinitionId is not ("" or "restoration" or "agriculture" or "formulae")) return No("Unknown research project.");
            Colony.ResearchProject = c.DefinitionId; return Yes("Research project selected; scholarly work is now assigned automatically.");
        }
        if (c.Kind == CommandKind.SetQuest)
        {
            if (c.Text == "track") Colony.TrackQuest = c.Quantity != 0;
            else if (c.DefinitionId is "Offered" or "Accepted" or "Declined") Colony.QuestState = c.DefinitionId;
            else return No("Invalid quest choice.");
            return Yes("Journal entry updated.");
        }
        return No("Command is unavailable.");
    }
}
