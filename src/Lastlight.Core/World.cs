namespace Lastlight.Core;

// Stored RNG state is independent of System.Random and runtime implementation.
public struct StableRandom
{
    public ulong State { get; set; }
    public StableRandom(ulong seed) => State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    public uint Next()
    {
        ulong x = State;
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27; State = x;
        return (uint)((x * 2685821657736338717UL) >> 32);
    }
    public int Next(int bound) => bound > 0 ? (int)(Next() % (uint)bound) : throw new ArgumentOutOfRangeException(nameof(bound));
}
public sealed class Cell
{
    public string TerrainId { get; set; } = "soil";
    public byte Variant { get; set; }
}
public sealed class Entity
{
    public long Id { get; set; }
    public string DefinitionId { get; set; } = "";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Quantity { get; set; } = 1;
    public int Health { get; set; } = 100;
    public int CraftQuality {get;set;}=50;
    public long MakerId {get;set;}
    public int Rotation { get; set; }
    public string MaterialId { get; set; } = "";
    public bool Forbidden { get; set; }
}
public enum CommandKind
{
    Spawn, Remove, Teleport, PaintTerrain, SetPriority, SetSchedule, AssignPolicy, EditPolicy, ClonePolicy,
    Draft, Forbid, CreateBlueprint, CancelBlueprint, CreateZone, RemoveZone, EditZone, Designate,
    CancelDesignation, AcknowledgeLetter, SetWorkMode, Rename, SetAnimalPolicy, SetResearch, SetQuest, EditBill, MoveDrafted, CombatOrder, Equip, Rescue, MedicalBed, DangerSettings, TriggerRaid, SliceAction, PrepareFormula, CastFormula, TutorialAction, PhysicalAction, RegionAction, InstitutionAction, CampaignAction
}
public sealed record WorldCommand(CommandKind Kind, long EntityId = 0, string DefinitionId = "", int X = 0, int Y = 0, int Z = 0, int Quantity = 1,
    int Width = 1, int Height = 1, int Rotation = 0, string Text = "", long TargetId = 0);
public sealed record CommandResult(bool Accepted, string Message, long EntityId = 0);
public sealed record WorldEvent(long Tick, string Message);
public sealed partial class World
{
    public const int TickRate = 30;
    public const int TicksPerHour = 900;
    public const int TicksPerDay = TicksPerHour * 24;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Levels { get; set; } = 1;
    public int Seed { get; set; }
    public long Tick { get; set; }
    public long NextEntityId { get; set; } = 1;
    public ulong GenerationRandomState { get; set; }
    public ulong JobRandomState { get; set; }
    public ulong CombatRandomState { get; set; }
    public ulong IncidentRandomState { get; set; }
    public string ContentManifest { get; set; } = "";
    public List<Cell> Cells { get; set; } = [];
    public List<Entity> Entities { get; set; } = [];
    public List<WorldCommand> PendingCommands { get; set; } = [];
    public List<WorldEvent> Events { get; set; } = [];
    public ColonyState Colony { get; set; } = new();
    public bool Contains(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < Width && y < Height && z < Levels;
    public Cell CellAt(int x, int y, int z = 0) => Contains(x, y, z) ? Cells[(z * Height + y) * Width + x] : throw new ArgumentOutOfRangeException(nameof(x));
    public Entity? Find(long id) => Entities.Find(e => e.Id == id);
    public string DateText => $"DAY {Tick / TicksPerDay + 1}  {Tick % TicksPerDay / TicksPerHour:00}:{Tick % TicksPerHour * 60 / TicksPerHour:00}";
    public void Enqueue(WorldCommand command) => PendingCommands.Add(command);
    public List<CommandResult> FlushCommands(Definitions definitions)
    {
        var results = new List<CommandResult>();
        foreach (var command in PendingCommands) results.Add(Apply(command, definitions));
        PendingCommands.Clear();
        return results;
    }
    public void Step(Definitions definitions)
    {
        FlushCommands(definitions);
        Tick++;
        if (Tick % 30 == 0 && Profile != null) AdvanceProfiled(definitions);
        else if (Tick % 30 == 0) { AdvanceSolar(definitions); AdvanceInstitutions(definitions); AdvanceRegion(definitions); AdvancePhysical(definitions); AdvanceSlice(definitions); AdvancePeopleAndDanger(definitions); AdvanceColony(definitions); }
    }
    public CommandResult Apply(WorldCommand command, Definitions definitions)
    {
        CommandResult Reject(string reason) => new(false, reason);
        if (!Enum.IsDefined(command.Kind)) return Reject("Unknown command.");
        if (command.Kind >= CommandKind.SetPriority) return ApplyColonyCommand(command, definitions);
        if (command.Kind == CommandKind.Remove)
        {
            var target = Find(command.EntityId);
            if (target is null) return Reject("Entity no longer exists.");
            Interrupt(target.Id, definitions);
            foreach(var artifact in Region.Artifacts.Where(a=>a.LocalEntityId==target.Id)){artifact.Custody="Lost";artifact.LocalEntityId=0;artifact.PartyId=0;if(Region.Enabled)Chronicle("ArtifactLoss",artifact.Name+" was lost from local custody.",artifact.OwnerFactionId,artifact.SiteId,artifact.Id);}
            foreach(var site in Region.Sites.Where(s=>s.MarketId==target.Id))site.MarketId=0;
            RemoveCampaignEntity(target.Id); RemoveInstitutionEntity(target.Id); Entities.Remove(target); Autonomy.Bills.RemoveAll(b=>b.BenchId==target.Id); Colony.People.Remove(target.Id);foreach(var bill in Autonomy.Bills)if(bill.WorkerId==target.Id)bill.WorkerId=0; Colony.Animals.Remove(target.Id); RemoveDangerEntity(target.Id); RemoveSliceEntity(target.Id, definitions);Physical.Devices.Remove(target.Id);Physical.Animals.Remove(target.Id);Physical.Networks.RemoveAll(n=>n.Members.Contains(target.Id));
            foreach (var animal in Colony.Animals.Values) if (animal.HandlerId == target.Id) animal.HandlerId = 0;
            Record($"Removed {target.Name} #{target.Id}.");
            return new(true, "Entity removed.", target.Id);
        }
        if (!Contains(command.X, command.Y, command.Z)) return Reject("Target is outside the map.");
        if (command.Kind == CommandKind.PaintTerrain)
        {
            if (!definitions.Terrain.TryGetValue(command.DefinitionId, out var terrain)) return Reject("Unknown terrain definition.");
            if (!terrain.Walkable && Entities.Any(e => e.X == command.X && e.Y == command.Y && e.Z == command.Z))
                return Reject("Remove entities before painting impassable terrain.");
            CellAt(command.X, command.Y, command.Z).TerrainId = terrain.Id;
            Record($"Painted {terrain.Name} at {command.X},{command.Y},{command.Z}.");
            return new(true, "Terrain changed.");
        }
        if (!definitions.Terrain[CellAt(command.X, command.Y, command.Z).TerrainId].Walkable) return Reject("Target terrain is impassable.");
        if (command.Kind == CommandKind.Teleport)
        {
            var target = Find(command.EntityId);
            if (target is null) return Reject("Select an existing entity.");
            if (Occupied(command.X, command.Y, command.Z, target.Id, definitions)) return Reject("A building blocks the target.");
            if (definitions.Entities[target.DefinitionId].Kind == EntityKind.Building &&
                Entities.Any(e => e.Id != target.Id && e.X == command.X && e.Y == command.Y && e.Z == command.Z))
                return Reject("Building target is occupied.");
            target.X = command.X; target.Y = command.Y; target.Z = command.Z;
            Record($"Debug moved {target.Name} to {target.X},{target.Y},{target.Z}.");
            return new(true, "Debug teleport complete.", target.Id);
        }
        if (!definitions.Entities.TryGetValue(command.DefinitionId, out var def)) return Reject("Unknown entity definition.");
        if (command.Quantity < 1 || command.Quantity > def.MaxStack) return Reject($"Quantity must be 1–{def.MaxStack}.");
        if (Entities.Count >= 20000 || NextEntityId == long.MaxValue) return Reject("Foundation entity capacity reached.");
        if (Occupied(command.X, command.Y, command.Z, 0, definitions)) return Reject("A building occupies the target.");
        if (def.Kind == EntityKind.Building && Entities.Any(e => e.X == command.X && e.Y == command.Y && e.Z == command.Z))
            return Reject("Building target is occupied.");
        var entity = new Entity
        {
            Id = NextEntityId++, DefinitionId = def.Id, X = command.X, Y = command.Y, Z = command.Z,
            Quantity = command.Quantity, Name = def.Kind == EntityKind.Person ? $"Settler {NextEntityId - 1}" : def.Name
        };
        Entities.Add(entity); Record($"Created {entity.Name} #{entity.Id}.");
        if (Colony.Initialized) { Colony.EnsureEntities(this, definitions); EnsureDanger(definitions); EnsureSlice(definitions); EnsurePhysical(definitions); }
        return new(true, $"Created {entity.Name}.", entity.Id);
    }
    private bool Occupied(int x, int y, int z, long except, Definitions definitions) =>
        Entities.Any(e => e.Id != except && definitions.Entities[e.DefinitionId].Kind == EntityKind.Building && BuildingCells(e).Contains((x,y,z)));
    public void Record(string message)
    {
        Events.Add(new(Tick, message));
        if (Events.Count > 128) Events.RemoveRange(0, Events.Count - 128);
    }
    public void Validate(Definitions definitions)
    {
        if (Cells is null || Entities is null || PendingCommands is null || Events is null)
            throw new InvalidDataException("World collections cannot be null.");
        if (Width is < 8 or > 256 || Height is < 8 or > 256 || Levels is < 1 or > 8 || Cells.Count != Width * Height * Levels)
            throw new InvalidDataException("Invalid map dimensions or cell count.");
        if (Tick < 0 || Tick > long.MaxValue - 1000000 || NextEntityId < 1) throw new InvalidDataException("Invalid simulation clock or ID allocator.");
        if (ContentManifest != definitions.ManifestHash) throw new InvalidDataException("Content manifest mismatch. Restore the definitions used by this save.");
        if (GenerationRandomState == 0 || JobRandomState == 0 || CombatRandomState == 0 || IncidentRandomState == 0)
            throw new InvalidDataException("Invalid random stream state.");
        foreach (var cell in Cells)
            if (cell is null || !definitions.Terrain.ContainsKey(cell.TerrainId)) throw new InvalidDataException("Save references missing terrain.");
        if (Entities.Count > 20000 || PendingCommands.Count > 10000 || Events.Count > 128) throw new InvalidDataException("Save exceeds foundation limits.");
        var ids = new HashSet<long>();
        var buildings = new HashSet<(int, int, int)>();
        foreach (var e in Entities)
        {
            if (e is null || e.Id < 1 || e.Id >= NextEntityId || !ids.Add(e.Id) || !Contains(e.X, e.Y, e.Z) || e.Health is < 0 or > 100 || e.CraftQuality is <1 or >100 || e.MakerId<0 || e.MakerId>=NextEntityId || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 128)
                throw new InvalidDataException("Invalid or duplicate entity.");
            if (!definitions.Entities.TryGetValue(e.DefinitionId, out var def) || e.Quantity < 1 || e.Quantity > def.MaxStack)
                throw new InvalidDataException("Save references an invalid entity definition or stack.");
            if (!IsAway(e.Id) && !definitions.Terrain[CellAt(e.X, e.Y, e.Z).TerrainId].Walkable) throw new InvalidDataException($"Entity {e.Name} #{e.Id} is on impassable terrain at {e.X},{e.Y},{e.Z}.");
            if (def.Kind == EntityKind.Building && !buildings.Add((e.X, e.Y, e.Z))) throw new InvalidDataException("Overlapping buildings.");
        }
        foreach (var e in Entities)
            if (!IsAway(e.Id) && definitions.Entities[e.DefinitionId].Kind != EntityKind.Building && Entities.Any(b => b.DefinitionId is "wall" or "fence" && BuildingCells(b).Contains((e.X,e.Y,e.Z))))
                throw new InvalidDataException("Entity overlaps a blocking building.");
        foreach (var c in PendingCommands)
            if (c is null || !Enum.IsDefined(c.Kind)) throw new InvalidDataException("Invalid pending command.");
        foreach (var ev in Events)
            if (ev is null || ev.Tick < 0 || ev.Tick > Tick || ev.Message is null || ev.Message.Length > 512) throw new InvalidDataException("Invalid history event.");
        if (Colony is null) throw new InvalidDataException("Missing colony state.");
        Colony.Validate(this, definitions);
        ValidateAutonomy(definitions);
        ValidateDanger(definitions);
        ValidateSlice(definitions); ValidatePhysical(definitions);ValidateRegion(definitions); ValidateInstitutions(definitions); ValidateSolar(definitions);
    }
}
