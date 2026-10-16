namespace Lastlight.Core;

public static class WorldGenerator
{
    public static World Create(Definitions definitions, int seed, int width = 128, int height = 128)
    {
        if (width is < 32 or > 256 || height is < 32 or > 256) throw new ArgumentOutOfRangeException(nameof(width));
        var random = new StableRandom(unchecked((ulong)(uint)seed) + 1);
        var world = new World
        {
            Width = width, Height = height, Seed = seed, ContentManifest = definitions.ManifestHash,
            JobRandomState = new StableRandom(unchecked((ulong)(uint)seed) + 101).State,
            CombatRandomState = new StableRandom(unchecked((ulong)(uint)seed) + 202).State,
            IncidentRandomState = new StableRandom(unchecked((ulong)(uint)seed) + 303).State
        };
        int cx = width / 2, cy = height / 2;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double hills = Math.Sin((x + seed % 83) * .085) + Math.Cos((y - seed % 47) * .11) + Math.Sin((x + y) * .046);
            double river = width * .25 + Math.Sin(y * .071 + seed % 19) * width * .05;
            var id = Math.Abs(x - river) < 2.3 ? "water" : hills > 1.58 ? "stone" : hills < -1.3 ? "sand" : "soil";
            if (Math.Abs(x - cx) < 16 && Math.Abs(y - cy) < 14) id = "soil";
            world.Cells.Add(new Cell { TerrainId = id, Variant = (byte)random.Next(8) });
        }
        // Crumbled observatory and campsite provide visual landmarks, not a construction system.
        for (int y = cy - 10; y <= cy - 3; y++)
        for (int x = cx + 5; x <= cx + 13; x++) world.CellAt(x, y).TerrainId = "floor";
        for (int x = cx + 5; x <= cx + 13; x++)
        {
            if (x != cx + 9) world.Apply(new(CommandKind.Spawn, DefinitionId: "wall", X: x, Y: cy - 10), definitions);
            if (x % 3 != 0) world.Apply(new(CommandKind.Spawn, DefinitionId: "wall", X: x, Y: cy - 3), definitions);
        }
        for (int y = cy - 9; y < cy - 3; y++)
        {
            world.Apply(new(CommandKind.Spawn, DefinitionId: "wall", X: cx + 5, Y: y), definitions);
            if (y % 3 != 0) world.Apply(new(CommandKind.Spawn, DefinitionId: "wall", X: cx + 13, Y: y), definitions);
        }
        string[] names = ["Ilyra", "Daren", "Osha", "Senn", "Vey"];
        for (int i = 0; i < names.Length; i++)
        {
            var result = world.Apply(new(CommandKind.Spawn, DefinitionId: "settler", X: cx - 4 + i * 2, Y: cy + i % 2), definitions);
            world.Find(result.EntityId)!.Name = names[i];
        }
        world.Apply(new(CommandKind.Spawn, DefinitionId: "wood", X: cx - 2, Y: cy + 4, Quantity: 40), definitions);
        world.Apply(new(CommandKind.Spawn, DefinitionId: "grain", X: cx, Y: cy + 4, Quantity: 30), definitions);
        world.Apply(new(CommandKind.Spawn, DefinitionId: "relic", X: cx + 9, Y: cy - 7), definitions);
        for (int i = 0; i < 12; i++)
        {
            int x = random.Next(width), y = random.Next(height);
            world.Apply(new(CommandKind.Spawn, DefinitionId: "grazer", X: x, Y: y), definitions);
        }
        world.GenerationRandomState = random.State;
        world.Events.Clear();
        world.Record("Five refugees reach the ruined observatory. Foundation world created.");
        world.Colony.Initialize(world, definitions);
        world.EnsureDanger(definitions);
        world.EnsureSlice(definitions);world.EnsurePhysical(definitions);world.EnsureRegion();
        world.Validate(definitions);
        return world;
    }
}
