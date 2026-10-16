using System.Globalization;
using System.Text.Json;
using Lastlight.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Lastlight.Desktop;

public sealed partial class LastlightGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly bool smoke, uiChecks, colonyUiChecks, dangerUiChecks, sliceUiChecks, physicalUiChecks, regionUiChecks, institutionUiChecks, solarUiChecks;
    private readonly string? screenshot, initialSave;
    private readonly string initialTab;
    private readonly float initialScale;
    private SpriteBatch batch = null!;
    private Texture2D pixel = null!, circle = null!, minimap = null!;
    private RenderTarget2D? smokeTarget;
    private BitmapFont font = null!;
    private Definitions definitions = null!;
    private World world = null!;
    private readonly Camera camera = new();
    private readonly SimulationClock clock = new();
    private readonly HashSet<long> selection = [];
    private long selectedId => selection.FirstOrDefault();
    private long blueprintId, zoneId, copyZoneId;
    private int level, frame, rotation, rowOffset, bindingPage;
    private bool grid, showZones = true, help, categorized = true;
    private string tab = "", inspectTab = "Bio", category = "Structure", tool = "", material = "basalt";
    private string status = "Refuge founded. Use Architect to plan your settlement; Space to run.";
    private ScheduleActivity scheduleBrush = ScheduleActivity.Work;
    private string craftingRecipe="timber-frame";
    private string policyKind = "Food", policyId = "food-default";
    private long letterId;
    private bool menuQuit;
    private KeyboardState oldKeyboard;
    private MouseState oldMouse;
    private Point dragStart, contextPoint;
    private bool dragging;
    private string dragTool = "";
    private double lastClickTime;
    private Point lastClickPoint;
    private readonly List<UiHit> hits = [];
    public bool ReleaseUiChecks {get;set;}
    private int keyboardFocus = -1;
    private string keyboardFocusText = "";
    private readonly List<(string Text, Action? Action, string Reason)> context = [];
    private string tooltip = "";
    private Rectangle popupBounds;
    private string textValue = "", textTitle = "";
    private Action<string>? textCommit;
    private string captureBinding = "";
    private readonly RasterizerState clipped = new() { ScissorTestEnable = true };
    private readonly string savePath = Path.Combine(SaveStore.DefaultDirectory, "foundation.lastlight");
    private readonly string settingsPath = Path.Combine(SaveStore.DefaultDirectory, "interface-settings.json");
    private InterfaceSettings settings = new();
    private long lastSavedTick = -1;
    private long lastDangerPause;
    private int UiWidth => (int)(Window.ClientBounds.Width / settings.Scale);
    private int UiHeight => (int)(Window.ClientBounds.Height / settings.Scale);
    private Rectangle MapArea => new(0, 62, UiWidth, Math.Max(64, UiHeight - 98));
    private Rectangle MinimapArea => new(UiWidth - 174, UiHeight - 296, 154, 154);
    private Rectangle PanelArea => new(18, Math.Max(94, UiHeight - Math.Min(440, UiHeight - 166) - 38), UiWidth - 36, Math.Min(440, UiHeight - 166));
    private static readonly Color Ink = new(15, 20, 25), Panel = new(24, 31, 37), Border = new(65, 78, 83), Gold = new(214, 170, 101), Text = new(220, 227, 223), Muted = new(146, 165, 165), Cyan = new(114, 205, 201), Red = new(226, 110, 98);
    private sealed record UiHit(Rectangle Bounds, Action? Action, string Hint, bool Paint, string Text = "");
    private static readonly string[] Tabs = ["Architect", "Work", "Schedule", "Assign", "Animals", "Wildlife", "Research", "Quests", "World", "History", "Factions", "Danger", "Levels", "Formulae", "Ruin", "Tutorial", "Engineering", "Menu"];
    private Point ToUi(Point p) => new((int)(p.X / settings.Scale), (int)(p.Y / settings.Scale));
    private Vector2 ToUiVector(Point p) => new(p.X / settings.Scale, p.Y / settings.Scale);

    public LastlightGame(bool smoke, string? screenshot, int width, string? initialSave = null, string initialTab = "", bool uiChecks = false, float initialScale = 0, bool colonyUiChecks = false, bool dangerUiChecks = false, bool sliceUiChecks = false, bool physicalUiChecks = false, bool regionUiChecks = false, bool institutionUiChecks = false, bool solarUiChecks = false)
    {
        this.solarUiChecks=solarUiChecks;this.institutionUiChecks=institutionUiChecks;this.regionUiChecks=regionUiChecks;this.physicalUiChecks=physicalUiChecks;this.sliceUiChecks=sliceUiChecks;this.dangerUiChecks = dangerUiChecks;this.colonyUiChecks = colonyUiChecks; this.smoke = smoke; this.screenshot = screenshot; this.initialSave = initialSave; this.initialTab = initialTab; this.uiChecks = uiChecks; this.initialScale = initialScale;
        graphics = new(this) { PreferredBackBufferWidth = width, PreferredBackBufferHeight = width == 1366 ? 768 : 900, SynchronizeWithVerticalRetrace = true };
        IsMouseVisible = true; IsFixedTimeStep = false;
        Window.Title = "LASTLIGHT | Stage I - Solar campaign"; Window.AllowUserResizing = true;
        Window.TextInput += (_, e) =>
        {
            if (textCommit != null && e.Character is >= ' ' and <= '~' && textValue.Length < 32) textValue += e.Character;
        };
        Window.ClientSizeChanged += (_, _) =>
        {
            if (Window.ClientBounds.Width < 1280 || Window.ClientBounds.Height < 720)
            {
                graphics.PreferredBackBufferWidth = Math.Max(1280, Window.ClientBounds.Width);
                graphics.PreferredBackBufferHeight = Math.Max(720, Window.ClientBounds.Height); graphics.ApplyChanges();
            }
        };
    }
    protected override void LoadContent()
    {
        definitions = Definitions.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "definitions.json"));
        world = uiChecks || colonyUiChecks || dangerUiChecks ? WorldGenerator.Create(definitions, 74219) : sliceUiChecks?VerticalWorldGenerator.Create(definitions,74219):physicalUiChecks?PhysicalWorldGenerator.Create(definitions,74219):regionUiChecks?LivingWorldGenerator.Create(definitions,74219):institutionUiChecks?InstitutionsWorldGenerator.Create(definitions,74219):SolarWorldGenerator.Create(definitions,74219);if(!world.Slice.Enabled){world.SetupDangerSupplies(definitions);world.Danger.RaidsEnabled=true;} camera.Center = new(world.Width / 2f, world.Height / 2f);
        if (!smoke && File.Exists(settingsPath))
        {
            try { settings = JsonSerializer.Deserialize<InterfaceSettings>(File.ReadAllText(settingsPath), JsonFormat.Options) ?? new(); settings.Validate(); }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { settings = new(); status = "Invalid preferences reset: " + ex.Message; }
        }
        if (initialScale != 0) { settings.Scale = initialScale; settings.Validate(); }
        if (initialSave != null) Restore(SaveStore.Read(initialSave, definitions));
        if(initialSave==null&&initialTab==""&&world.Solar.Enabled)tab="World";
        if (Tabs.Contains(initialTab)) tab = initialTab;
        if(initialTab is "Solar" or "Projects" or "Politics" or "Director"){tab="World";solarWorldPane=true;solarPage=initialTab=="Solar"?"Observations":initialTab;}
        if(initialTab=="Region"){tab="World";solarWorldPane=false;}
        if(initialTab is "Water" or "Climate" or "Networks" or "Industry" or "Husbandry"){tab="Engineering";engineeringPage=initialTab;}
        if(initialTab=="Bills" || colonyUiChecks) { SelectEntity(world.Entities.First(e=>e.DefinitionId=="stove").Id);tab="Bills"; }
        if(initialTab=="Craft") {SelectEntity(world.Entities.First(e=>e.DefinitionId=="workbench").Id);tab="Bills";craftingRecipe="d-medicine";}
        if(initialTab=="Combat") {SelectEntity(world.Colony.People.Keys.First());inspectTab="Health";}
        if(initialTab is "Medical" or "People" or "Gear") { SelectEntity(world.Colony.People.Keys.First());tab=initialTab; }
        if(dangerUiChecks){SelectEntity(world.Colony.People.Keys.First());tab="Danger";}
        lastDangerPause=world.Danger.PauseSerial;
        if(initialTab=="Needs") { SelectEntity(world.Colony.People.Keys.First());inspectTab="Needs"; }
        batch = new(GraphicsDevice); pixel = new(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        circle = new(GraphicsDevice, 32, 32); var data = new Color[1024];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) data[y * 32 + x] = Vector2.DistanceSquared(new(x, y), new(15.5f, 15.5f)) <= 225 ? Color.White : Color.Transparent;
        circle.SetData(data); font = new(GraphicsDevice, Path.Combine(AppContext.BaseDirectory, "Assets", "font.png"));
        if (smoke) smokeTarget = new RenderTarget2D(GraphicsDevice, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight);
        RebuildMinimap(); camera.Viewport = MapArea; LoadSliceAudio();
    }
    protected override void Update(GameTime gameTime)
    {
        camera.Viewport = MapArea; UpdateSliceAudio();
        var keyboard = Keyboard.GetState(); var mouse = Mouse.GetState(); Point point = ToUi(mouse.Position);
        bool Press(Keys k) => keyboard.IsKeyDown(k) && oldKeyboard.IsKeyUp(k);
        bool Bind(string action) => Press(settings.Keys[action]);
        bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (!IsActive && !smoke) clock.Paused = true;
        if (captureBinding != "")
        {
            var newly = keyboard.GetPressedKeys().Where(k => oldKeyboard.IsKeyUp(k)).ToArray();
            if (newly.Length > 0)
            {
                var key = newly[0];
                if (key is Keys.Enter or Keys.Back or Keys.Delete or Keys.LeftShift or Keys.RightShift or Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt or Keys.Up or Keys.Down or Keys.Left or Keys.Right) status = "This key is reserved for editing, selection or navigation.";
                else if (key != Keys.Escape)
                {
                    string? duplicate = settings.Keys.FirstOrDefault(p => p.Key != captureBinding && p.Value == key).Key;
                    if (duplicate != null) status = $"Key already assigned to {duplicate}. Choose another key.";
                    else { settings.Keys[captureBinding] = key; captureBinding = ""; SaveSettings(); }
                }
                else captureBinding = "";
            }
        }
        else if (textCommit != null)
        {
            if (Press(Keys.Back)) textValue = textValue.Length > 0 ? textValue[..^1] : textValue;
            if (Press(Keys.Enter)) { var commit = textCommit; textCommit = null; commit(textValue); }
            if (Press(Keys.Escape)) textCommit = null;
        }
        else
        {
            bool control = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            if (control && (Press(Keys.Up) || Press(Keys.Down)))
            {
                FocusControl(Press(Keys.Up) ? -1 : 1);
            }
            if (Press(Keys.Enter)) ActivateFocusedControl();
            if (Bind("Pause")) clock.Paused = !clock.Paused;
            if (Bind("Normal speed")) clock.SetSpeed(1); if (Bind("Fast speed")) clock.SetSpeed(3); if (Bind("Very fast speed")) clock.SetSpeed(6);
            if (Bind("Single tick") && clock.Paused) world.Step(definitions);
            foreach (var name in Tabs) if (settings.Keys.ContainsKey(name) && Bind(name)) ToggleTab(name);
            if (Bind("Save")) Save(); if (Bind("Load")) Load(shift); if (Bind("Home")) CenterCamp();
            if (Bind("Help")) help = !help;
            if (Bind("Grid")) grid = !grid;
            if (Press(Keys.Escape))
            {
                if (context.Count > 0) context.Clear(); else if (letterId != 0) letterId = 0; else if (tool != "") tool = "";
                else if (tab != "") tab = ""; else ToggleTab("Menu");
                dragging = false;
            }
            if (Bind("Rotate left") && tool.StartsWith("build:")) rotation = (rotation + 3) % 4;
            if (Bind("Rotate right") && tool.StartsWith("build:")) rotation = (rotation + 1) % 4;
            if (Bind("Draft") && selection.Count > 0) SetDrafted(!selection.All(id => world.Colony.People.TryGetValue(id, out var p) && p.Drafted));
            if (Bind("Level down")) {level = Math.Max(0, level - 1);RebuildMinimap();}
            if (Bind("Level up")) {level = Math.Min(world.Levels - 1, level + 1);RebuildMinimap();}
            if (Press(Keys.Delete)) CancelSelectedPlan();
            if (tab == "" || tab == "Architect")
            {
                float pan = 16 * (float)Math.Min(.05, gameTime.ElapsedGameTime.TotalSeconds) / camera.Zoom * (shift ? 2 : 1);
                Vector2 delta = Vector2.Zero;
                if (keyboard.IsKeyDown(settings.Keys["Pan up"]) || !control && keyboard.IsKeyDown(Keys.Up)) delta.Y--;
                if (keyboard.IsKeyDown(settings.Keys["Pan down"]) || !control && keyboard.IsKeyDown(Keys.Down)) delta.Y++;
                if (keyboard.IsKeyDown(settings.Keys["Pan left"]) || !control && keyboard.IsKeyDown(Keys.Left)) delta.X--;
                if (keyboard.IsKeyDown(settings.Keys["Pan right"]) || !control && keyboard.IsKeyDown(Keys.Right)) delta.X++;
                camera.Center += delta * pan;
                if (mouse.MiddleButton == ButtonState.Pressed && oldMouse.MiddleButton == ButtonState.Pressed)
                    camera.Center -= new Vector2(mouse.X - oldMouse.X, mouse.Y - oldMouse.Y) / (Camera.TileSize * camera.Zoom * settings.Scale);
                camera.Clamp(world);
            }
            if (mouse.ScrollWheelValue != oldMouse.ScrollWheelValue)
            {
                if (tab != "" && tab != "Architect" && PanelArea.Contains(point)) rowOffset = Math.Clamp(rowOffset + (oldMouse.ScrollWheelValue - mouse.ScrollWheelValue) / 120, 0, Math.Max(0, (tab == "Medical" ? world.Body(selectedId)?.Wounds.Count??0 : tab == "People" ? world.Danger.People.GetValueOrDefault(selectedId)?.Memories.Count??0 : tab == "Gear" ? world.Entities.Count : tab == "Bills" ? world.Autonomy.Bills.Count(b=>b.BenchId==selectedId) : tab == "History" ? world.Events.Count : tab is "Animals" or "Wildlife" ? world.Colony.Animals.Count : world.Colony.People.Count) - 1));
                else if (!InterfaceContains(point) && MapArea.Contains(point)) camera.ZoomAt(ToUiVector(mouse.Position), MathF.Pow(1.15f, (mouse.ScrollWheelValue - oldMouse.ScrollWheelValue) / 120f), world);
            }
            bool click = mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;
            bool release = mouse.LeftButton == ButtonState.Released && oldMouse.LeftButton == ButtonState.Pressed;
            if (click)
            {
                var hit = hits.LastOrDefault(h => h.Bounds.Contains(point));
                if (hit != null) { hit.Action?.Invoke(); if (hit.Action == null) status = hit.Hint; }
                else if (context.Count > 0) context.Clear();
                else if (MinimapArea.Contains(point)) { camera.Center = new((point.X - MinimapArea.X) * world.Width / (float)MinimapArea.Width, (point.Y - MinimapArea.Y) * world.Height / (float)MinimapArea.Height); camera.Clamp(world); }
                else if (!InterfaceContains(point) && MapArea.Contains(point)) { dragging = true; dragStart = point; dragTool = tool; }
            }
            else if (mouse.LeftButton == ButtonState.Pressed && !dragging)
            {
                var hit = hits.LastOrDefault(h => h.Paint && h.Bounds.Contains(point)); hit?.Action?.Invoke();
            }
            if (release && dragging)
            {
                dragging = false;
                if (!InterfaceContains(point) && MapArea.Contains(point)) CompleteMapDrag(point, shift, gameTime.TotalGameTime.TotalSeconds);
            }
            if (mouse.RightButton == ButtonState.Pressed && oldMouse.RightButton == ButtonState.Released && !InterfaceContains(point) && MapArea.Contains(point) && !MinimapArea.Contains(point))
                OpenContext(point, shift);
        }
        if(world.Danger.AutoPause && world.Danger.PauseSerial!=lastDangerPause)clock.Paused=true;
        clock.Advance(world, definitions, gameTime.ElapsedGameTime.TotalSeconds);
        if(world.Danger.PauseSerial!=lastDangerPause) {if(world.Danger.AutoPause){clock.Paused=true;status="Danger event — simulation paused. Check alerts and medical status.";}lastDangerPause=world.Danger.PauseSerial;}
        oldKeyboard = keyboard; oldMouse = mouse; base.Update(gameTime);
    }
    private void ToggleTab(string name) { tab = tab == name ? "" : name; rowOffset = 0; tool = ""; context.Clear(); dragging = false; menuQuit = false; if (tab == "Menu") clock.Paused = true; }
    private void SelectEntity(long id, bool add = false)
    {
        if (!add) selection.Clear();
        if (add && selection.Contains(id)) selection.Remove(id); else if (world.Find(id) != null) selection.Add(id);
        blueprintId = zoneId = 0; inspectTab = "Bio";
    }
    private void Focus(long id) { var e = world.Find(id); if (e == null) return;if(world.Solar.Evacuees.Contains(id)){SelectEntity(id);tab="People";status="This resident is aboard the evacuation vessel; their body, equipment and memories are preserved.";return;}if(world.IsAway(id)){tab="World";solarWorldPane=false;status="This resident is traveling with a regional expedition.";return;} SelectEntity(id); camera.Center = new(e.X + .5f, e.Y + .5f); level = e.Z; tab = ""; RebuildMinimap(); }
    private void CompleteMapDrag(Point end, bool shift, double time)
    {
        var startTile = camera.ScreenToTile(new(dragStart.X, dragStart.Y)); var endTile = camera.ScreenToTile(new(end.X, end.Y));
        int x = (int)MathF.Floor(endTile.X), y = (int)MathF.Floor(endTile.Y);
        if (!world.Contains(x, y, level)) return;
        bool box = Math.Abs(end.X - dragStart.X) + Math.Abs(end.Y - dragStart.Y) > 6;
        int left = Math.Max(0, (int)MathF.Floor(Math.Min(startTile.X, endTile.X))), top = Math.Max(0, (int)MathF.Floor(Math.Min(startTile.Y, endTile.Y)));
        int right = Math.Min(world.Width - 1, (int)MathF.Floor(Math.Max(startTile.X, endTile.X))), bottom = Math.Min(world.Height - 1, (int)MathF.Floor(Math.Max(startTile.Y, endTile.Y)));
        if(dragTool.StartsWith("cast:")){var target=world.Entities.FirstOrDefault(e=>e.X==x && e.Y==y && e.Z==level && (e.Id!=SliceActor || dragTool=="cast:flesh"));Execute(new(CommandKind.CastFormula,SliceActor,dragTool[5..],X:x,Y:y,Z:level,TargetId:target?.Id??0));tool="";return;}
        if (dragTool.StartsWith("zone:"))
        {
            var result = Execute(new(CommandKind.CreateZone, DefinitionId: dragTool[5..], X: left, Y: top, Z: level, Width: right - left + 1, Height: bottom - top + 1));
            if (result.Accepted) { zoneId = result.EntityId; blueprintId = 0; selection.Clear(); }
            return;
        }
        if (dragTool.StartsWith("build:"))
        {
            string id = dragTool[6..]; var def = ColonyCatalog.Find(id)!;
            int stepX = rotation % 2 == 0 ? def.Width : def.Height, stepY = rotation % 2 == 0 ? def.Height : def.Width;
            int placed = 0;
            for (int yy = top; yy <= bottom; yy += stepY) for (int xx = left; xx <= right; xx += stepX)
                if (Execute(new(CommandKind.CreateBlueprint, DefinitionId: id, X: xx, Y: yy, Z: level, Rotation: rotation, Text: material)).Accepted) placed++;
            if (placed > 0) status = $"Placed {placed} blueprint(s). Workers will collect materials and construct reachable plans.";
            return;
        }
        if (dragTool.StartsWith("order:"))
        {
            int count = 0;
            for (int yy = top; yy <= bottom; yy++) for (int xx = left; xx <= right; xx++)
            {
                var result = dragTool[6..] == "Cancel" ? CancelAt(xx, yy) : Execute(new(CommandKind.Designate, DefinitionId: dragTool[6..], X: xx, Y: yy, Z: level));
                if (result.Accepted) count++;
            }
            if (count > 0) status = $"Updated {count} designation(s)."; return;
        }
        if (box)
        {
            if (!shift) selection.Clear();
            foreach (var e in world.Entities.Where(e => e.Z == level && e.X >= left && e.X <= right && e.Y >= top && e.Y <= bottom && definitions.Entities[e.DefinitionId].Kind == EntityKind.Person)) selection.Add(e.Id);
            blueprintId = zoneId = 0; return;
        }
        var candidates = world.Entities.Where(e => e.X == x && e.Y == y && e.Z == level).OrderBy(e => e.Id).ToList();
        if (candidates.Count > 0)
        {
            if (time - lastClickTime < .32 && Vector2.Distance(new(end.X, end.Y), new(lastClickPoint.X, lastClickPoint.Y)) < 8)
            {
                if (!shift) selection.Clear(); var first = candidates[0];
                foreach (var e in world.Entities.Where(e => e.DefinitionId == first.DefinitionId && e.Z == level && MapArea.Contains(camera.TileToScreen(new(e.X + .5f, e.Y + .5f))))) selection.Add(e.Id);
            }
            else { int i = candidates.FindIndex(e => e.Id == selectedId); SelectEntity(candidates[(i + 1) % candidates.Count].Id, shift); }
            lastClickTime = time; lastClickPoint = end; return;
        }
        if (!shift) selection.Clear();
        blueprintId = world.Colony.Blueprints.FirstOrDefault(b => b.Z == level && World.BlueprintCells(b).Contains((x, y)))?.Id ?? 0;
        zoneId = blueprintId == 0 ? world.Colony.Zones.LastOrDefault(z => z.Cells.Contains((level * world.Height + y) * world.Width + x))?.Id ?? 0 : 0;
    }
    private CommandResult CancelAt(int x, int y)
    {
        var b = world.Colony.Blueprints.Find(b => b.Z == level && World.BlueprintCells(b).Contains((x, y)));
        if (b != null) return Execute(new(CommandKind.CancelBlueprint, b.Id));
        return Execute(new(CommandKind.CancelDesignation, X: x, Y: y, Z: level));
    }
    private void OpenContext(Point point, bool shift)
    {
        context.Clear(); contextPoint = new(Math.Min(point.X, UiWidth - 310), Math.Min(point.Y, UiHeight - 300));
        var t = camera.ScreenToTile(new(point.X, point.Y)); int x = (int)MathF.Floor(t.X), y = (int)MathF.Floor(t.Y);
        if (!world.Contains(x, y, level)) return;
        var e = world.Entities.FirstOrDefault(e => e.X == x && e.Y == y && e.Z == level);
        var plan = world.Colony.Blueprints.FirstOrDefault(b => b.Z == level && World.BlueprintCells(b).Contains((x, y)));
        if (e != null)
        {
            context.Add(("Inspect " + e.Name, () => SelectEntity(e.Id), ""));
            context.Add((e.Forbidden ? "Allow object" : "Forbid object", () => Execute(new(CommandKind.Forbid, e.Id, Quantity: e.Forbidden ? 0 : 1)), ""));
            if (definitions.Entities[e.DefinitionId].Kind == EntityKind.ItemStack) context.Add(("Designate hauling", () => Execute(new(CommandKind.Designate, DefinitionId: "Haul", X: x, Y: y, Z: level)), ""));
            if (definitions.Entities[e.DefinitionId].Kind == EntityKind.Creature)
            {
                context.Add(("Designate hunting", () => Execute(new(CommandKind.Designate, DefinitionId: "Hunt", X: x, Y: y, Z: level)), ""));
                context.Add(("Designate taming", () => Execute(new(CommandKind.Designate, DefinitionId: "Tame", X: x, Y: y, Z: level)), ""));
            }
        }
        if(e!=null && world.Body(e.Id) is {Dead:true})
            foreach(long actor in selection.Where(world.Colony.People.ContainsKey).Take(1).ToArray())context.Add(("Salvage with "+world.Find(actor)!.Name,()=>Execute(new(CommandKind.Equip,actor,Text:"loot",TargetId:e.Id)),""));
        if(e!=null && world.Colony.People.ContainsKey(e.Id) && world.Body(e.Id) is {Dead:false,Downed:true})
            foreach(long actor in selection.Where(id=>id!=e.Id && world.Colony.People.ContainsKey(id)).ToArray())context.Add(("Rescue with "+world.Find(actor)!.Name,()=>Execute(new(CommandKind.Rescue,actor,TargetId:e.Id)),""));
        if(e!=null && (definitions.Document.Weapons.Any(w=>w.Id==e.DefinitionId) || e.DefinitionId is "padded-armor" or "arrows"))
            foreach(long actor in selection.Where(world.Colony.People.ContainsKey).Take(1).ToArray())context.Add(("Equip with "+world.Find(actor)!.Name,()=>Execute(new(CommandKind.Equip,actor,TargetId:e.Id)),""));
        if (plan != null) context.Add(("Cancel blueprint", () => Execute(new(CommandKind.CancelBlueprint, plan.Id)), ""));
        if (selection.Any(id => world.Colony.People.ContainsKey(id)))
        {
            context.Add(("Draft / undraft selection", () => SetDrafted(!selection.All(id => world.Colony.People.TryGetValue(id, out var p) && p.Drafted)), ""));
            context.Add(("Move drafted selection here", () => { foreach(long id in selection.ToArray()) Execute(new(CommandKind.MoveDrafted,id,X:x,Y:y,Z:level)); }, ""));
            if(e!=null && !world.Colony.People.ContainsKey(e.Id) && world.Body(e.Id) is {Dead:false,Downed:false})
                context.Add(("Attack with drafted selection",()=>{foreach(long id in selection.ToArray())Execute(new(CommandKind.CombatOrder,id,Text:"attack",TargetId:e.Id));},""));
            context.Add(("Hold position",()=>{foreach(long id in selection.ToArray())Execute(new(CommandKind.CombatOrder,id,Text:"hold"));},""));
            context.Add(("Retreat here",()=>{foreach(long id in selection.ToArray())Execute(new(CommandKind.CombatOrder,id,X:x,Y:y,Z:level,Text:"retreat"));},""));
        }
        context.Add(("Plan this tile", () => Execute(new(CommandKind.Designate, DefinitionId: "Plan", X: x, Y: y, Z: level)), ""));
        context.Add(("Close", () => { }, ""));
        contextPoint=new(Math.Clamp(contextPoint.X,4,UiWidth-304),Math.Clamp(contextPoint.Y,4,Math.Max(4,UiHeight-context.Count*31-15)));
    }
    private void SetDrafted(bool drafted) { foreach (long id in selection.ToList()) if (world.Colony.People.ContainsKey(id)) Execute(new(CommandKind.Draft, id, Quantity: drafted ? 1 : 0)); }
    private void CancelSelectedPlan()
    {
        if (blueprintId != 0) { Execute(new(CommandKind.CancelBlueprint, blueprintId)); blueprintId = 0; }
        else if (zoneId != 0) { Execute(new(CommandKind.RemoveZone, zoneId)); zoneId = 0; }
        else status = "Select a blueprint or zone to delete. Entities are not deleted by normal controls.";
    }
    private CommandResult Execute(WorldCommand command)
    {
        world.Enqueue(command); var result = world.FlushCommands(definitions).Last(); status = result.Message;
        selection.RemoveWhere(id => world.Find(id) == null); return result;
    }
    private void CenterCamp() => camera.Center = new(world.Width / 2f, world.Height / 2f);
    private ViewState CurrentView() => new() { SolarWorldPane=solarWorldPane,SolarPage=solarPage,SolarProject=solarProject,SolarFaction=solarFaction, RegionalSiteId=regionalSite,RegionalPartyId=regionalParty,RegionalFactionId=regionalFaction,RegionalQuestId=regionalQuest,CameraX = camera.Center.X, CameraY = camera.Center.Y, Zoom = camera.Zoom, Level = level, SelectedEntityId = selectedId, SelectedEntityIds = selection.ToList(), OpenTab = tab, ShowGrid = grid, ShowZones = showZones };
    private void Restore(SaveSnapshot snapshot)
    {
        solarWorldPane=snapshot.View.SolarWorldPane;solarPage=snapshot.View.SolarPage;solarProject=snapshot.View.SolarProject;solarFaction=snapshot.View.SolarFaction;
        world = snapshot.World;solarModeChoice=Array.IndexOf(World.SolarModes,world.Solar.Mode);solarSeason=world.Solar.SeasonDays;solarHost=world.Solar.Projects.FirstOrDefault(p=>p.Id==solarProject)?.HostId??0; camera.Center = new(snapshot.View.CameraX, snapshot.View.CameraY); camera.SetZoom(snapshot.View.Zoom);
        regionalSite=snapshot.View.RegionalSiteId;regionalParty=snapshot.View.RegionalPartyId;regionalFaction=snapshot.View.RegionalFactionId;regionalQuest=snapshot.View.RegionalQuestId;
        level = snapshot.View.Level; selection.Clear(); foreach (long id in snapshot.View.SelectedEntityIds) selection.Add(id);
        if (selection.Count == 0 && snapshot.View.SelectedEntityId != 0) selection.Add(snapshot.View.SelectedEntityId);
        tab = (Tabs.Contains(snapshot.View.OpenTab) || snapshot.View.OpenTab is "Bills" or "Medical" or "People" or "Gear") ? snapshot.View.OpenTab : ""; grid = snapshot.View.ShowGrid; showZones = snapshot.View.ShowZones;
        blueprintId = zoneId = 0; context.Clear(); dragging = false; clock.Reset(); lastSavedTick = world.Tick;lastDangerPause=world.Danger.PauseSerial;
    }
    private void Save() => TrySave();
    private bool TrySave()
    {
        try { SaveStore.Write(savePath, world, CurrentView(), definitions); lastSavedTick = world.Tick; status = "Saved settlement, plans, work, schedules, policies and interface."; return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { status = "Save failed: " + ex.Message; clock.Paused = true; return false; }
    }
    private void Load(bool backup)
    {
        try { Restore(SaveStore.Read(backup ? savePath + ".bak" : savePath, definitions)); tool = ""; RebuildMinimap(); status = "Settlement restored; simulation paused."; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { status = "Load failed; current world retained: " + ex.Message; clock.Paused = true; }
    }
    private void SaveSettings()
    {
        try { if (smoke) return; Directory.CreateDirectory(SaveStore.DefaultDirectory); File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonFormat.Options)); status = "Interface preferences saved."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status = "Preferences could not be saved: " + ex.Message; }
    }
    private void BeginRename(string title, string value, Action<string> commit) { textTitle = title; textValue = value; textCommit = commit; clock.Paused = true; }
    private static Color ColorFrom(string hex) => new(byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber), byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber), byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber));
    private void RebuildMinimap()
    {
        minimap?.Dispose(); minimap = new(GraphicsDevice, world.Width, world.Height); var colors = new Color[world.Width * world.Height];
        for (int y = 0; y < world.Height; y++) for (int x = 0; x < world.Width; x++) colors[y * world.Width + x] = ColorFrom(definitions.Terrain[world.CellAt(x, y, level).TerrainId].Color);
        minimap.SetData(colors);
    }
    protected override void Draw(GameTime gameTime)
    {
        camera.Viewport = MapArea;
        if (smokeTarget != null) GraphicsDevice.SetRenderTarget(smokeTarget);
        GraphicsDevice.Viewport = new Viewport(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        GraphicsDevice.Clear(Ink);
        GraphicsDevice.ScissorRectangle = new(0, (int)(MapArea.Y * settings.Scale), Window.ClientBounds.Width, (int)(MapArea.Height * settings.Scale));
        batch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: clipped, transformMatrix: camera.Transform * Matrix.CreateScale(settings.Scale));
        DrawWorld(); DrawPlans(); batch.End();
        batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: Matrix.CreateScale(settings.Scale)); hits.Clear(); tooltip = "";
        DrawInterface(); batch.End();
        if (smokeTarget != null) GraphicsDevice.SetRenderTarget(null);
        frame++;
        if (ReleaseUiChecks) RunReleaseInterfaceChecks();
        if (uiChecks) RunInterfaceChecks();
        if (colonyUiChecks) RunColonyInterfaceChecks();
        if(dangerUiChecks)RunDangerInterfaceChecks();
        if(sliceUiChecks)RunSliceInterfaceChecks();
        if(physicalUiChecks)RunPhysicalInterfaceChecks();
        if(solarUiChecks)RunSolarInterfaceChecks();
        if(institutionUiChecks)RunInstitutionInterfaceChecks();
        if(regionUiChecks)RunRegionInterfaceChecks();
        if (smoke && frame == 8 && !ReleaseUiChecks && !uiChecks && !colonyUiChecks && !dangerUiChecks && !sliceUiChecks && !physicalUiChecks && !regionUiChecks && !institutionUiChecks && !solarUiChecks)
        {
            string smokePath = Path.Combine(SaveStore.DefaultDirectory, "stage-b-smoke.lastlight");
            SaveStore.Write(smokePath, world, CurrentView(), definitions);
            if (SaveStore.Fingerprint(world) != SaveStore.Fingerprint(SaveStore.Read(smokePath, definitions).World)) throw new InvalidDataException("Graphics smoke save differs.");
            if (screenshot != null) CaptureImage(screenshot);
            Console.WriteLine($"STAGE J GRAPHICS PASS: {UiWidth}x{UiHeight}, tab '{tab}', {hits.Count} controls; persistence verified."); Exit();
        }
        base.Draw(gameTime);
    }
    private void CaptureImage(string path)
    {
        using var buffer = new MemoryStream();
        smokeTarget!.SaveAsPng(buffer, smokeTarget.Width, smokeTarget.Height);
        byte[] bytes = buffer.ToArray();
        if (bytes.Length < 100 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) throw new InvalidDataException("PNG capture failed.");
        File.WriteAllBytes(path, bytes);
        if (new FileInfo(path).Length != bytes.Length) throw new IOException("PNG capture was not fully written.");
    }
    private void Fill(Rectangle rect, Color color) => batch.Draw(pixel, rect, color);
    private void Outline(Rectangle rect, Color color, int size = 1)
    {
        Fill(new(rect.X, rect.Y, rect.Width, size), color); Fill(new(rect.X, rect.Bottom - size, rect.Width, size), color);
        Fill(new(rect.X, rect.Y, size, rect.Height), color); Fill(new(rect.Right - size, rect.Y, size, rect.Height), color);
    }
    private void Label(string value, int x, int y, Color color, float scale = .78f) => font.Draw(batch, value, new(x, y), settings.HighContrast && color == Muted ? Text : color, scale);
    private string Fit(string value, int width, float scale = .78f)
    {
        if (BitmapFont.Width(value, scale) <= width) return value;
        while (value.Length > 0 && BitmapFont.Width(value + "...", scale) > width) value = value[..^1]; return value + "...";
    }
    private void Button(Rectangle rect, string text, Action? action, bool active = false, string hint = "", bool paint = false, float scale = .75f)
    {
        bool hover = rect.Contains(ToUi(Mouse.GetState().Position));
        Fill(rect, active ? new Color(65, 69, 50) : hover ? new Color(47, 60, 66) : new Color(31, 41, 46)); Outline(rect, active ? Gold : Border);
        Label(Fit(text, rect.Width - 12, scale), rect.X + 6, rect.Y + (rect.Height - (int)(24 * scale)) / 2 - 2, action == null ? Muted * .65f : active ? Gold : Text, scale);
        int focusIndex = hits.Count(h => h.Action != null && !h.Paint);
        if (action != null && !paint && keyboardFocus == focusIndex && keyboardFocusText == text) { Outline(rect, Color.White, 3); if (hint != "") tooltip = hint; }
        if (settings.HighContrast) Outline(rect, action == null ? Border : Color.White);
        hits.Add(new(rect, action, hint, paint, text)); if (hover && hint != "") tooltip = hint;
    }
    private void Paragraph(string text, Rectangle area, Color color, float scale = .78f)
    {
        string line = ""; int y = area.Y;
        foreach (string word in text.Split(' '))
        {
            if (BitmapFont.Width(line + word, scale) > area.Width && line != "") { Label(line, area.X, y, color, scale); y += 24; line = ""; if (y > area.Bottom - 20) return; }
            line += word + " ";
        }
        if (line != "") Label(line, area.X, y, color, scale);
    }
    protected override void UnloadContent()
    {
        batch.Dispose(); pixel.Dispose(); circle.Dispose(); minimap.Dispose(); font.Dispose(); clipped.Dispose(); DisposeSliceAudio();smokeTarget?.Dispose(); base.UnloadContent();
    }
}

public sealed class InterfaceSettings
{
    public float Scale { get; set; } = 1;
    public bool HighContrast { get; set; }
    public Dictionary<string, Keys> Keys { get; set; } = Defaults();
    public static Dictionary<string, Keys> Defaults() => new()
    {
        ["Pause"] = Microsoft.Xna.Framework.Input.Keys.Space, ["Architect"] = Microsoft.Xna.Framework.Input.Keys.Tab,
        ["Work"] = Microsoft.Xna.Framework.Input.Keys.F1, ["Schedule"] = Microsoft.Xna.Framework.Input.Keys.F2,
        ["Assign"] = Microsoft.Xna.Framework.Input.Keys.F3, ["Animals"] = Microsoft.Xna.Framework.Input.Keys.F4,
        ["Wildlife"] = Microsoft.Xna.Framework.Input.Keys.F5, ["Research"] = Microsoft.Xna.Framework.Input.Keys.F6,
        ["Quests"] = Microsoft.Xna.Framework.Input.Keys.F7, ["World"] = Microsoft.Xna.Framework.Input.Keys.F8,
        ["History"] = Microsoft.Xna.Framework.Input.Keys.F9, ["Save"] = Microsoft.Xna.Framework.Input.Keys.F10,
        ["Load"] = Microsoft.Xna.Framework.Input.Keys.F11, ["Home"] = Microsoft.Xna.Framework.Input.Keys.Home,
        ["Pan up"] = Microsoft.Xna.Framework.Input.Keys.W, ["Pan down"] = Microsoft.Xna.Framework.Input.Keys.S,
        ["Pan left"] = Microsoft.Xna.Framework.Input.Keys.A, ["Pan right"] = Microsoft.Xna.Framework.Input.Keys.D,
        ["Menu"] = Microsoft.Xna.Framework.Input.Keys.M, ["Factions"] = Microsoft.Xna.Framework.Input.Keys.L,
        ["Draft"] = Microsoft.Xna.Framework.Input.Keys.R, ["Grid"] = Microsoft.Xna.Framework.Input.Keys.G,
        ["Rotate left"] = Microsoft.Xna.Framework.Input.Keys.Q, ["Rotate right"] = Microsoft.Xna.Framework.Input.Keys.E,
        ["Normal speed"] = Microsoft.Xna.Framework.Input.Keys.D1, ["Fast speed"] = Microsoft.Xna.Framework.Input.Keys.D2,
        ["Very fast speed"] = Microsoft.Xna.Framework.Input.Keys.D3, ["Single tick"] = Microsoft.Xna.Framework.Input.Keys.OemPeriod,
        ["Level down"] = Microsoft.Xna.Framework.Input.Keys.OemOpenBrackets, ["Level up"] = Microsoft.Xna.Framework.Input.Keys.OemCloseBrackets,
        ["Help"] = Microsoft.Xna.Framework.Input.Keys.F12
    };
    public void Validate()
    {
        if (!float.IsFinite(Scale) || Scale is < 1 or > 1.25f || Keys == null || Defaults().Keys.Any(k => !Keys.ContainsKey(k)) || Keys.Values.Any(k => !Enum.IsDefined(k) || k is Microsoft.Xna.Framework.Input.Keys.Escape or Microsoft.Xna.Framework.Input.Keys.Enter or Microsoft.Xna.Framework.Input.Keys.Back or Microsoft.Xna.Framework.Input.Keys.Delete or Microsoft.Xna.Framework.Input.Keys.LeftShift or Microsoft.Xna.Framework.Input.Keys.RightShift or Microsoft.Xna.Framework.Input.Keys.LeftControl or Microsoft.Xna.Framework.Input.Keys.RightControl or Microsoft.Xna.Framework.Input.Keys.LeftAlt or Microsoft.Xna.Framework.Input.Keys.RightAlt or Microsoft.Xna.Framework.Input.Keys.Up or Microsoft.Xna.Framework.Input.Keys.Down or Microsoft.Xna.Framework.Input.Keys.Left or Microsoft.Xna.Framework.Input.Keys.Right) || Keys.Values.Distinct().Count() != Keys.Count) throw new InvalidDataException("Invalid interface preferences.");
    }
}
