using Lastlight.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Lastlight.Desktop;

public sealed partial class LastlightGame
{
    private bool InterfaceContains(Point point)
    {
        if (context.Count > 0 && new Rectangle(contextPoint.X, contextPoint.Y, 300, context.Count * 31 + 10).Contains(point)) return true;
        if (textCommit != null || letterId != 0 || captureBinding != "") return true;
        if (point.Y < 62 || point.Y >= UiHeight - 36) return true;
        if (tab != "" && PanelArea.Contains(point)) return true;
        if (new Rectangle(12, 84, 208, 245).Contains(point) || new Rectangle(UiWidth - 300, 82, 286, 210).Contains(point)) return true;
        if (tab == "" && new Rectangle(12, UiHeight - 268, 350, 225).Contains(point)) return true;
        if (MinimapArea.Contains(point) || new Rectangle(UiWidth - 278, UiHeight - 118, 264, 80).Contains(point)) return true;
        if (help && new Rectangle(235, 83, 515, 237).Contains(point)) return true;
        if (tool.StartsWith("build:") && new Rectangle(235, 90, 515, 108).Contains(point)) return true;
        return false;
    }
    private void DrawPlans()
    {
        if (showZones || zoneId != 0)
        {
            foreach (var zone in world.Colony.Zones)
            {
                if (!showZones && zone.Id != zoneId) continue;
                Color color = zone.Kind switch { ZoneKind.Stockpile => new(86, 147, 203), ZoneKind.Growing => new(123, 187, 84), ZoneKind.Home => new(193, 155, 101), _ => new(173, 122, 204) };
                foreach (int index in zone.Cells)
                {
                    int z = index / (world.Width * world.Height); if (z != level) continue;
                    int x = index % world.Width, y = index / world.Width % world.Height;
                    Fill(new(x * 32, y * 32, 32, 32), color * (zone.Id == zoneId ? .4f : .2f));
                    Outline(new(x * 32, y * 32, 32, 32), color * .6f, 1);
                }
            }
        }
        foreach (var b in world.Colony.Blueprints.Where(b => b.Z == level))
            foreach (var cell in World.BlueprintCells(b))
            {
                Fill(new(cell.X * 32 + 2, cell.Y * 32 + 2, 28, 28), Cyan * .25f);
                Outline(new(cell.X * 32 + 1, cell.Y * 32 + 1, 30, 30), b.Id == blueprintId ? Gold : Cyan, 2);
                if (b.BuildableId == "bed") Fill(new(cell.X * 32 + 8, cell.Y * 32 + 8, 16, 16), Cyan * .4f);
            }
        foreach (var d in world.Colony.Designations.Where(d => d.Z == level))
        {
            var color = d.Kind == "Mine" ? Gold : d.Kind is "Hunt" or "Deconstruct" ? Red : Cyan;
            Outline(new(d.X * 32 + 4, d.Y * 32 + 4, 24, 24), color, 2);
            Fill(new(d.X * 32 + 14, d.Y * 32 + 8, 3, 16), color);
        }
        var point = ToUi(Mouse.GetState().Position);
        if (tool.StartsWith("build:") && !InterfaceContains(point) && MapArea.Contains(point))
        {
            var t = camera.ScreenToTile(new(point.X, point.Y)); int x = (int)MathF.Floor(t.X), y = (int)MathF.Floor(t.Y);
            var preview = new Blueprint { BuildableId = tool[6..], X = x, Y = y, Z = level, Rotation = rotation };
            var color = world.BlueprintError(preview.BuildableId, material, x, y, level, rotation, definitions) == null ? Cyan : Red;
            foreach (var cell in World.BlueprintCells(preview)) { Fill(new(cell.X * 32, cell.Y * 32, 32, 32), color * .3f); Outline(new(cell.X * 32, cell.Y * 32, 32, 32), color); }
        }
        if (dragging)
        {
            var a = camera.ScreenToTile(new(dragStart.X, dragStart.Y)); var b = camera.ScreenToTile(new(point.X, point.Y));
            int x = (int)MathF.Floor(Math.Min(a.X, b.X)), y = (int)MathF.Floor(Math.Min(a.Y, b.Y));
            int width = (int)MathF.Floor(Math.Max(a.X, b.X)) - x + 1, height = (int)MathF.Floor(Math.Max(a.Y, b.Y)) - y + 1;
            Fill(new(x * 32, y * 32, width * 32, height * 32), Cyan * .12f); Outline(new(x * 32, y * 32, width * 32, height * 32), Cyan, 2);
        }
    }
    private void DrawInterface()
    {
        Fill(new(0, 0, UiWidth, 62), Ink); Fill(new(0, 60, UiWidth, 2), Border);
        Label("LASTLIGHT", 18, 7, Gold, 1.1f); Label("OBSERVATORY REFUGE", 19, 35, Muted, .65f);
        int max = Math.Max(1, (UiWidth - 455) / 77), px = 280;
        foreach (var pair in world.Colony.People.Take(max))
        {
            var e = world.Find(pair.Key)!; var p = pair.Value; long id = e.Id;
            Button(new(px, 5, 71, 48), e.Name, () => Focus(id), selection.Contains(id), $"Age {p.Age}; health {e.Health}; mood {p.Mood}" + (p.Drafted ? "; DRAFTED" : "; undrafted"), scale: .67f);
            Fill(new(px + 3, 49, (int)(65 * p.Mood / 100f), 3), p.Drafted ? Cyan : new Color(134, 164, 99));
            if (p.Drafted) Label("D", px + 54, 6, Cyan, .65f); px += 77;
        }
        Label("STAGE J", UiWidth - 140, 8, Gold, .85f); Label("RELEASE RC", UiWidth - 140, 32, Muted, .65f);
        DrawResources(); DrawAlerts(); DrawMinimap(); DrawTime();
        if (tool.StartsWith("build:")) DrawBuildControls();
        if (tab == "") DrawInspector(); else DrawPanel();
        if (help) DrawHelp();
        Fill(new(0, UiHeight - 36, UiWidth, 36), Ink); Fill(new(0, UiHeight - 37, UiWidth, 1), Border);
        int x = 9, available = UiWidth - 18;
        float tabScale=UiWidth<1400?.52f:.62f;
        int total = Tabs.Sum(t => (int)BitmapFont.Width(t, tabScale) + 12);
        foreach (string name in Tabs)
        {
            string n = name; int width = (int)BitmapFont.Width(name, tabScale) + 12 + Math.Max(0, (available - total) / Tabs.Length);
            Button(new(x, UiHeight - 32, width, 28), n, () => ToggleTab(n), tab == n, settings.Keys.TryGetValue(n, out var key) ? $"Open {n}: {key}" : $"Open {n}", scale: tabScale); x += width;
        }
        if (tool != "") Label(Fit($"TOOL: {tool.Replace("build:", "").Replace("zone:", "").Replace("order:", "")}  |  drag to paint; Esc cancels", UiWidth - 500, .65f), 235, 66, Cyan, .65f);
        DrawContext();
        if (letterId != 0) DrawLetter();
        if (textCommit != null) DrawRename();
        if (captureBinding != "") DrawCapture();
        if (tool.StartsWith("build:") && !InterfaceContains(ToUi(Mouse.GetState().Position)))
        {
            var pointer = camera.ScreenToTile(ToUiVector(Mouse.GetState().Position));
            string? reason = world.BlueprintError(tool[6..], material, (int)MathF.Floor(pointer.X), (int)MathF.Floor(pointer.Y), level, rotation, definitions);
            if (reason != null) tooltip = reason;
            else if(world.Slice.Enabled && tool is "build:roof" or "build:bridge" && world.Contains((int)pointer.X,(int)pointer.Y,level)){int cell=world.Index((int)pointer.X,(int)pointer.Y,level);tooltip=world.Supported(cell,tool=="build:bridge")?"Supported span.":"UNSUPPORTED: add a column or an anchored floor before the two-hour collapse deadline.";}
        }
        if(tooltip=="" && world.Physical.Enabled && tool=="" && !InterfaceContains(ToUi(Mouse.GetState().Position))){var point=camera.ScreenToTile(ToUiVector(Mouse.GetState().Position));int tx=(int)point.X,ty=(int)point.Y;if(world.Contains(tx,ty,level)){int cell=world.Index(tx,ty,level);tooltip=$"Water {world.WaterDepth(cell)}/7 | contamination {world.WaterConcentration(cell):P0} | {world.TemperatureAt(cell):0.0} C | light {world.LightAt(cell):0} | moisture {world.MoistureAt(cell):0}";if(world.Autonomy.Crops.TryGetValue(cell,out var crop))tooltip+=" | crop: "+(world.CropFailure(crop)==""?"growing":world.CropFailure(crop));}}
        if (tooltip != "") DrawTooltip(tooltip);
    }
    private void DrawResources()
    {
        var area = new Rectangle(12, 84, 208, 245); Fill(area, Panel * .95f); Outline(area, Border);
        Button(new(20, 92, 192, 25), categorized ? "Resources - grouped" : "Resources - flat", () => categorized = !categorized, hint: "Counts show accepted goods in stockpile zones. Hover rows for total and available.");
        int y = 126; string previousGroup = "";
        foreach (string id in new[] { "wood", "blocks", "brass", "grain", "meal" })
        {
            var r = world.Resources(id); string name = definitions.Entities[id].Name;
            string group=id is "grain" or "meal"?"FOOD":"MATERIALS";
            if(categorized && group!=previousGroup) {Label(group,23,y,Muted,.63f);y+=19;previousGroup=group;}
            Button(new(22, y, 188, 25), name + $" : {r.Stored}", () =>
            {
                var entity = world.Entities.FirstOrDefault(e => e.DefinitionId == id); if (entity != null) Focus(entity.Id);
            }, hint: $"Total {r.Total} | Available {r.Available} | Stored {r.Stored} | Forbidden {r.Forbidden} | Claims {world.Autonomy.Claims.Count}.");
            y += 31;
        }
    }
    private void DrawAlerts()
    {
        int x = UiWidth - 300, y = 82;
        foreach (var alert in world.Alerts().Take(5))
        {
            var a = alert;
            Button(new(x, y, 285, 28), "! " + a.Label, () =>
            {
                if (a.EntityId != 0) Focus(a.EntityId); else { tab = a.Tab; rowOffset = 0; if (a.Id is "storage" or "growing") category = "Zones"; }
                status = a.Details;
            }, hint: a.Details, scale: .7f); y += 31;
        }
        foreach (var letter in world.Colony.Letters.Where(l => !l.Archived).Take(2))
        {
            long id = letter.Id;
            Button(new(x, y + 6, 285, 29), (letter.Read ? "Letter: " : "NEW: ") + letter.Title, () => { letterId = id; Execute(new(CommandKind.AcknowledgeLetter, id, Quantity: 0)); }, !letter.Read, "Read and archive this settlement letter.", scale: .68f);
            y += 35;
        }
    }
    private void DrawBuildControls()
    {
        var rect = new Rectangle(235, 90, 515, 108); Fill(rect, Panel); Outline(rect, Cyan);
        var d = ColonyCatalog.Find(tool[6..])!;
        Label(d.Name + " / Material / Q-E rotation", rect.X + 10, rect.Y + 5, Cyan, .72f);
        int x = rect.X + 10, materialIndex=0;
        foreach (var m in definitions.Document.Materials)
        {
            string id = m.Id;
            Button(new(rect.X+10+materialIndex%4*120, rect.Y+32+materialIndex/4*32, 115, 28), m.Name, () => material = id, material == id, "Select blueprint material.", scale: .59f); materialIndex++;
        }
        Button(new(rect.Right-91, rect.Y+3, 81, 25), rotation * 90 + " deg", () => rotation = (rotation + 1) % 4, hint: "Rotate placement footprint.", scale: .62f);
    }
    private void DrawMinimap()
    {
        Fill(new(MinimapArea.X - 5, MinimapArea.Y - 5, 164, 180), Panel); Outline(new(MinimapArea.X - 5, MinimapArea.Y - 5, 164, 180), Border);
        batch.Draw(minimap, MinimapArea, Color.White);
        foreach (var e in world.Entities.Where(e => e.Z == level && definitions.Entities[e.DefinitionId].Kind is EntityKind.Person or EntityKind.Artifact))
            Fill(new(MinimapArea.X + e.X * 154 / world.Width, MinimapArea.Y + e.Y * 154 / world.Height, 3, 3), selection.Contains(e.Id) ? Gold : Color.White);
        foreach (var b in world.Colony.Blueprints) Fill(new(MinimapArea.X + b.X * 154 / world.Width, MinimapArea.Y + b.Y * 154 / world.Height, 2, 2), Cyan);
        var a = camera.ScreenToTile(new(MapArea.Left, MapArea.Top)); var b2 = camera.ScreenToTile(new(MapArea.Right, MapArea.Bottom));
        int left = Math.Clamp(MinimapArea.X + (int)(a.X * 154 / world.Width), MinimapArea.Left, MinimapArea.Right), top = Math.Clamp(MinimapArea.Y + (int)(a.Y * 154 / world.Height), MinimapArea.Top, MinimapArea.Bottom);
        int right = Math.Clamp(MinimapArea.X + (int)(b2.X * 154 / world.Width), MinimapArea.Left, MinimapArea.Right), bottom = Math.Clamp(MinimapArea.Y + (int)(b2.Y * 154 / world.Height), MinimapArea.Top, MinimapArea.Bottom);
        Outline(new(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)), Gold);
        Label($"SEED {world.Seed} / Z {level}", MinimapArea.X, MinimapArea.Bottom + 3, Muted, .61f);
    }
    private void DrawTime()
    {
        int x = UiWidth - 278, y = UiHeight - 112;
        Fill(new(x - 6, y - 5, 270, 76), Panel);
        Button(new(x, y, 65, 25), "Pause", () => clock.Paused = !clock.Paused, clock.Paused, settings.Keys["Pause"] + " pauses.", scale: .65f);
        for (int i = 0; i < 3; i++) { int speed = new[] { 1, 3, 6 }[i]; Button(new(x + 70 + 47 * i, y, 43, 25), speed + "x", () => clock.SetSpeed(speed), !clock.Paused && clock.Speed == speed, scale: .68f); }
        Button(new(x + 214, y, 38, 25), "Z"+level, () => {level=(level+1)%world.Levels;RebuildMinimap();}, hint: "Cycle elevations. Open Levels for stairs and support.", scale: .65f);
        Label(world.DateText + (clock.Paused ? "  PAUSED" : ""), x, y + 31, Text, .65f);
        Label(world.LevelName(level)+" | Light "+world.LightAt(world.Index((int)camera.Center.X,(int)camera.Center.Y,level)).ToString("0"), x, y + 52, Muted, .52f);
        if (tab == "") Label(Fit(status, UiWidth - 400, .62f), 382, UiHeight - 59, Muted, .62f);
    }
    private void DrawInspector()
    {
        var area = new Rectangle(12, UiHeight - 268, 350, 225); Fill(area, Panel); Outline(area, Border);
        int x = area.X + 11, y = area.Y + 9;
        var zone = world.Colony.Zones.Find(z => z.Id == zoneId);
        var blueprint = world.Colony.Blueprints.Find(b => b.Id == blueprintId);
        var e = world.Find(selectedId);
        if (zone != null) { DrawZoneInspector(zone, x, y); return; }
        if (blueprint != null)
        {
            var d = ColonyCatalog.Find(blueprint.BuildableId)!;
            Label(d.Name + " (blueprint)", x, y, Cyan, .86f);
            Label($"{blueprint.MaterialId} | rotation {blueprint.Rotation * 90}", x, y + 31, Text, .7f);
            Label($"Estimated cost: {d.Cost} units", x, y + 57, Muted, .7f);
            Paragraph("Workers collect and carry materials, then build this plan. Cancelling returns carried supplies.", new(x, y + 84, 320, 70), Muted, .69f);
            Button(new(x, y + 168, 156, 29), "Cancel blueprint", CancelSelectedPlan, hint: "Remove this plan without destroying a building.");
            return;
        }
        if (e == null)
        {
            Label("OBSERVATORY REFUGE", x, y, Gold, .87f);
            Paragraph("Select a settler, animal, resource, blueprint or zone. Drag to select settlers; Shift adds to selection.", new(x, y + 39, 322, 88), Muted, .73f);
            Button(new(x, y + 141, 151, 28), "Grid", () => grid = !grid, grid);
            Button(new(x + 160, y + 141, 151, 28), "Show zones", () => showZones = !showZones, showZones);
            Label($"{world.Entities.Count} objects / {world.Colony.Blueprints.Count} plans", x, y + 179, Muted, .67f); return;
        }
        Label(Fit(selection.Count > 1 ? $"{selection.Count} selected - {e.Name}" : e.Name, 315, .87f), x, y, Gold, .87f);
        var kind = definitions.Entities[e.DefinitionId].Kind;
        if (kind == EntityKind.Person)
        {
            string[] tabs = ["Bio", "Needs", "Health", "Gear", "Social", "Log"];
            for (int i = 0; i < tabs.Length; i++) { string t = tabs[i]; Button(new(x + i * 53, y + 28, 51, 25), t, () => inspectTab = t, inspectTab == t, scale: .6f); }
            var p = world.Colony.People[e.Id];
            switch (inspectTab)
            {
                case "Bio":
                    Label($"Age {p.Age} | {p.Background} | {p.Trait}", x, y + 63, Text, .67f);
                    var top = p.Skills.Select((v, i) => (v, i)).OrderByDescending(v => v.v).Take(3).ToList();
                    for (int i = 0; i < top.Count; i++) Label($"{ColonyCatalog.Work[top[i].i]}: {top[i].v}", x, y + 87 + i * 20, Muted, .7f); break;
                case "Health":
                    var body=world.Body(e.Id)!; var capacity=world.Capacity(e.Id);
                    Label($"Health {e.Health}% | Blood {body.Blood:0}%"+(body.Downed?" DOWNED":""),x,y+64,Text,.68f);
                    Label($"Wounds {body.Wounds.Count} | Pain {capacity.Pain:P0}",x,y+88,Muted,.68f);
                    Button(new(x,y+120,300,28),"Medical details",()=>{tab="Medical";rowOffset=0;},scale:.7f); break;
                case "Needs":
                    Label($"Food {p.Food/10:0}% | Rest {p.Rest/10:0}%", x, y + 67, Text, .72f);
                    Label($"Recreation {p.Recreation/10:0}% | Ate {p.MealsEaten}", x, y + 93, Muted, .68f);
                    Label($"Mood {p.Mood}% | "+p.Activity,x,y+119,Cyan,.67f);
                    Button(new(x,y+140,300,23),"Memories and relationships",()=>{tab="People";rowOffset=0;},scale:.65f);break;
                case "Gear": Label(world.Body(e.Id)!.WeaponId+" | Ammo "+world.Body(e.Id)!.Ammo,x,y+67,Text,.7f);
                    Button(new(x,y+117,300,29),"Equipment pickup",()=>{tab="Gear";rowOffset=0;},scale:.7f);break;
                case "Social": Label("Mood: "+p.Mood+" | "+p.Trait,x,y+67,Text,.7f);Button(new(x,y+117,300,29),"Memories and relationships",()=>{tab="People";rowOffset=0;},scale:.7f);break;
                default: Paragraph(world.Events.LastOrDefault(ev => ev.Message.Contains(e.Name))?.Message ?? "No personal commands recorded.", new(x, y + 66, 320, 95), Muted, .7f); break;
            }
            Button(new(x, y + 169, 98, 29), p.Drafted ? "Undraft " + settings.Keys["Draft"] : "Draft " + settings.Keys["Draft"], () => SetDrafted(!p.Drafted), p.Drafted, "Draft to suspend chores. Right-click reachable terrain to move.", scale: .69f);
            Button(new(x + 105, y + 169, 96, 29), "Rename", () => BeginRename("Rename settler", e.Name, name => Execute(new(CommandKind.Rename, e.Id, Text: name))), scale: .69f);
            Button(new(x + 208, y + 169, 100, 29), "Work", () => ToggleTab("Work"), scale: .69f);
        }
        else
        {
            if(e.DefinitionId=="raider")
            {Label(world.Body(e.Id) is {Dead:true}?"Dead scavenger":world.Body(e.Id) is {Downed:true}?"Downed scavenger":"HOSTILE SCAVENGER",x,y+40,Red,.76f);Label($"Health {e.Health} | Weapon {world.Body(e.Id)!.WeaponId}",x,y+70,Text,.7f);Button(new(x,y+120,300,30),"Medical details",()=>{tab="Medical";rowOffset=0;});return;}
            if(e.DefinitionId=="bed")
            {Label("Bed for rest and recovery",x,y+40,Muted,.73f);Button(new(x,y+100,306,30),world.Danger.MedicalBeds.Contains(e.Id)?"Medical bed: YES":"Medical bed: NO",()=>Execute(new(CommandKind.MedicalBed,e.Id,Quantity:world.Danger.MedicalBeds.Contains(e.Id)?0:1)),world.Danger.MedicalBeds.Contains(e.Id));return;}
            if(e.DefinitionId is "stove" or "workbench" or "f-mill" or "f-smelter" or "f-kiln" or "f-loom" or "f-fermenter") { DrawBillsInspector(e,x,y); return; }
            Label($"{kind} #{e.Id} | {e.X},{e.Y},{e.Z}", x, y + 34, Text, .73f);
            Label($"Quantity {e.Quantity} | Condition {e.Health}%", x, y + 60, Muted, .73f);
            Paragraph(kind == EntityKind.Artifact ? "Dormant daylight engine. Its internal mechanism and enchantments are unknown. Study systems arrive later." : kind == EntityKind.Creature ? "Wild herbivore. Select Wildlife to designate hunting or taming." : kind == EntityKind.Building ? "Existing ruined structure. Deconstruction can be designated in Architect > Orders." : "Supplies carried by the refugees. A stockpile must accept this type to count it as stored.", new(x, y + 90, 322, 67), Muted, .7f);
            Button(new(x, y + 169, 149, 29), e.Forbidden ? "Allow" : "Forbid", () => Execute(new(CommandKind.Forbid, e.Id, Quantity: e.Forbidden ? 0 : 1)), e.Forbidden);
            Button(new(x + 157, y + 169, 149, 29), kind == EntityKind.Creature ? "Wildlife" : "Architect", () => ToggleTab(kind == EntityKind.Creature ? "Wildlife" : "Architect"));
        }
    }
    private static string RecipeName(string id)=>id switch
    {
        "simple-meal"=>"Simple meal", "fungus-meal"=>"Fungus meal", "timber-frame"=>"Timber frame",
        "d-medicine"=>"Herbal medicine", "d-arrows"=>"Bone-tipped arrows", "d-bow"=>"Duskwood bow", "d-spear"=>"Bronze spear", "d-coat"=>"Padded coat", _=>id.Replace('-',' ')
    };
    private void DrawBillsInspector(Entity bench, int x, int y)
    {
        Label("Production bills: "+world.Autonomy.Bills.Count(b=>b.BenchId==bench.Id),x,y+38,Cyan,.75f);
        Paragraph("Workers collect ingredients and fulfill ordered bills. Stock targets include committed output from every bench.",new(x,y+72,310,70),Muted,.7f);
        Button(new(x,y+160,305,30),"Open bills",()=>{tab="Bills";rowOffset=0;},scale:.74f);
    }
    private void DrawBillsPanel(Rectangle panel)
    {
        var bench=world.Find(selectedId);
        if(bench==null || bench.DefinitionId is not ("stove" or "workbench" or "f-mill" or "f-smelter" or "f-kiln" or "f-loom" or "f-fermenter")) { Label("Select a stove or crafting bench.",panel.X+16,panel.Y+55,Muted);return; }
        int x=panel.X+16,y=panel.Y+48;
        var craftRecipes=definitions.Document.Recipes.Where(r=>world.Physical.Enabled?world.RecipeBench(r.Id)==bench.DefinitionId:r.OutputId!="meal"&&!r.Id.StartsWith("f-",StringComparison.Ordinal)).Select(r=>r.Id).ToList();
        if(!craftRecipes.Contains(craftingRecipe))craftingRecipe=craftRecipes[0];
        string recipe=bench.DefinitionId=="stove"&&!world.Physical.Enabled?"simple-meal":craftingRecipe;
        Button(new(x,y,200,27),"Add "+RecipeName(recipe),()=>Execute(new(CommandKind.EditBill,bench.Id,recipe,Text:"add")),scale:.65f);
        if(bench.DefinitionId=="stove"&&!world.Physical.Enabled)Button(new(x+208,y,200,27),"Add fungus meal",()=>Execute(new(CommandKind.EditBill,bench.Id,"fungus-meal",Text:"add")),scale:.65f);
        else Button(new(x+208,y,200,27),"Recipe: "+RecipeName(craftingRecipe),()=>craftingRecipe=craftRecipes[(craftRecipes.IndexOf(craftingRecipe)+1)%craftRecipes.Count],scale:.65f);
        Label("Ordered recipes | 1 unit radius = 1 tile | copies start suspended",x+425,y+6,Muted,.61f);
        var bills=world.Autonomy.Bills.Where(b=>b.BenchId==bench.Id).Skip(rowOffset).Take(3).ToList();
        y+=39;
        foreach(var bill in bills)
        {
            var b=bill;
            Label(RecipeName(b.RecipeId)+" | completed "+b.Completed,x,y,Cyan,.72f);
            int mode=b.Repeat?2:b.CountMode?1:0;
            Button(new(x+260,y-3,155,27),mode==2?"Forever":mode==1?"Do X times":"Until X stock",()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:(mode+1)%3,Text:"mode")),scale:.65f);
            int count=b.CountMode?b.Remaining:b.Target;string field=b.CountMode?"remaining":"target";
            Button(new(x+423,y-3,43,27),"-5",()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:Math.Max(b.CountMode?0:1,count-5),Text:field)),scale:.65f);
            Label(count.ToString(),x+475,y+2,Text,.7f);
            Button(new(x+523,y-3,43,27),"+5",()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:Math.Min(1000,count+5),Text:field)),scale:.65f);
            Button(new(x+576,y-3,115,27),b.Suspended?"Resume":"Suspend",()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:b.Suspended?0:1,Text:"suspend")),scale:.65f);
            Button(new(x+702,y-3,60,27),"Up",()=>Execute(new(CommandKind.EditBill,b.Id,Text:"up")),scale:.65f);
            Button(new(x+772,y-3,80,27),"Copy",()=>Execute(new(CommandKind.EditBill,b.Id,Text:"copy")),scale:.65f);
            Button(new(x+862,y-3,80,27),"Delete",()=>Execute(new(CommandKind.EditBill,b.Id,Text:"remove")),scale:.65f);
            var workers=new long[]{0}.Concat(world.Colony.People.Keys.Order()).ToList();int next=(workers.IndexOf(b.WorkerId)+1)%workers.Count;
            Button(new(x,y+31,240,27),b.WorkerId==0?"Worker: anyone":"Worker: "+world.Find(b.WorkerId)!.Name,()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:(int)workers[next],Text:"worker")),scale:.65f);
            Button(new(x+260,y+31,230,27),"Ingredient radius: "+b.IngredientRadius,()=>Execute(new(CommandKind.EditBill,b.Id,Quantity:b.IngredientRadius==256?10:b.IngredientRadius==10?30:256,Text:"radius")),scale:.65f);
            y+=90;
        }
        Label("Mouse wheel scrolls bills. Forbidden benches and ingredients are skipped.",x,panel.Bottom-54,Muted,.65f);
    }
    private void DrawZoneInspector(Zone zone, int x, int y)
    {
        Label(Fit(zone.Name, 313, .86f), x, y, Cyan, .86f);
        Label($"{zone.Kind} | {zone.Cells.Count} cells", x, y + 30, Text, .73f);
        if (zone.Kind == ZoneKind.Stockpile)
        {
            Button(new(x, y + 59, 150, 26), $"Timber: {(zone.AllowWood ? "yes" : "no")}", () => Execute(new(CommandKind.EditZone, zone.Id, Quantity: zone.AllowWood ? 0 : 1, Text: "wood")), zone.AllowWood, scale: .68f);
            Button(new(x + 156, y + 59, 150, 26), $"Grain: {(zone.AllowGrain ? "yes" : "no")}", () => Execute(new(CommandKind.EditZone, zone.Id, Quantity: zone.AllowGrain ? 0 : 1, Text: "grain")), zone.AllowGrain, scale: .68f);
            Button(new(x, y + 90, 150, 26), $"Priority: {zone.Priority}", () => Execute(new(CommandKind.EditZone, zone.Id, Quantity: (zone.Priority + 1) % 5, Text: "priority")), scale: .68f);
            Button(new(x + 156, y + 90, 150, 26), "Allow forbidden", () => Execute(new(CommandKind.EditZone, zone.Id, Quantity: zone.AllowForbidden ? 0 : 1, Text: "forbidden")), zone.AllowForbidden, scale: .68f);
        }
        else if (zone.Kind == ZoneKind.Growing)
            Button(new(x, y + 60, 306, 28), "Crop: " + zone.Crop, () => Execute(new(CommandKind.EditZone, zone.Id, DefinitionId: world.Physical.Enabled?(zone.Crop=="Duskgrain"?"Fungus":zone.Crop=="Fungus"?"Herbs":"Duskgrain"):zone.Crop=="Duskgrain"?"Fungus":"Duskgrain", Text: "crop")));
        else Paragraph(zone.Kind == ZoneKind.Allowed ? "Assign this allowed area to settlers in Assign." : "Home area marks colony maintenance territory for future workers.", new(x, y + 65, 305, 62), Muted, .73f);
        Button(new(x, y + 124, 99, 26), "Copy", () => { copyZoneId = zone.Id; status = "Zone settings copied."; }, scale: .69f);
        Button(new(x + 105, y + 124, 99, 26), "Paste", copyZoneId == 0 ? null : () => Execute(new(CommandKind.EditZone, zone.Id, Quantity: (int)copyZoneId, Text: "copy")), hint: copyZoneId == 0 ? "Copy another zone first." : "Paste compatible zone settings.", scale: .69f);
        Button(new(x + 210, y + 124, 99, 26), "Rename", () => BeginRename("Rename zone", zone.Name, name => Execute(new(CommandKind.Rename, zone.Id, DefinitionId: "zone", Text: name))), scale: .69f);
        Button(new(x, y + 168, 149, 29), "Delete zone", CancelSelectedPlan, hint: "Removes only the zone, not its contents.");
        Button(new(x + 157, y + 168, 149, 29), "Show zones", () => showZones = !showZones, showZones);
    }
    private void DrawPanel()
    {
        var p = PanelArea; Fill(p, Panel); Outline(p, Border);
        Label(tab.ToUpperInvariant(), p.X + 15, p.Y + 9, Gold, 1);
        Button(new(p.Right - 47, p.Y + 8, 32, 27), "X", () => { tab = ""; tool = ""; }, hint: "Close panel; Escape also closes it.");
        Label(Fit(status, p.Width - 35, .61f), p.X + 14, p.Bottom - 27, Muted, .61f);
        switch (tab)
        {
            case "Danger": DrawDangerPanel(p); break;
            case "Engineering": DrawEngineeringPanel(p); break;
            case "Levels": case "Formulae": case "Ruin": case "Tutorial": DrawSlicePanel(p); break;
            case "Medical": DrawMedicalPanel(p); break;
            case "People": DrawPeoplePanel(p); break;
            case "Gear": DrawGearPanel(p); break;
            case "Bills": DrawBillsPanel(p); break;
            case "Architect": DrawArchitect(p); break;
            case "Work": DrawWork(p); break;
            case "Schedule": DrawSchedule(p); break;
            case "Assign": DrawAssign(p); break;
            case "Animals": DrawAnimals(p, false); break;
            case "Wildlife": DrawAnimals(p, true); break;
            case "Research": DrawResearch(p); break;
            case "Quests": DrawQuests(p); break;
            case "World": DrawWorldPanel(p); break;
            case "History": DrawHistory(p); break;
            case "Factions": DrawFactions(p); break;
            case "Menu": DrawMenu(p); break;
        }
    }
    private void DrawArchitect(Rectangle p)
    {
        string[] categories = ["Orders", "Structure", "Furniture", "Production", "Power", "Security", "Floors", "Zones", "Temperature", "Ritual", "Transport"];
        for (int i = 0; i < categories.Length; i++)
        {
            string name = categories[i]; Button(new(p.X + 13, p.Y + 47 + i * 29, 144, 26), name, () => { category = name; tool = ""; }, category == name, scale: .72f);
        }
        int x = p.X + 176, y = p.Y + 47, width = p.Width - 196;
        if (category == "Zones")
        {
            foreach (ZoneKind kind in Enum.GetValues<ZoneKind>())
            {
                string name = kind.ToString();
                Button(new(x, y, 185, 31), name + " zone", () => { tool = "zone:" + name; tab = ""; }, tool == "zone:" + name, "Select then drag a rectangle on the map.");
                y += 38;
            }
            Paragraph("Stockpile filters affect displayed stored quantities immediately. Growing zones accept soil. Allowed areas can be assigned in the Assign tab.", new(x + 215, p.Y + 51, Math.Max(220, width - 235), 150), Muted, .76f);
        }
        else if (category == "Orders")
        {
            foreach (string name in World.DesignationKinds.Append("Cancel"))
            {
                string n = name; Button(new(x, y, 205, 31), n, () => { tool = "order:" + n; tab = ""; }, hint: "Create or cancel persistent work designations; workers execute reachable construction, mining and hauling tasks."); y += 36;
            }
            Paragraph("Mine rock, mark items for hauling, mark animals for hunting/taming, deconstruct ruins, or draw planning marks. Drag to paint multiple tiles.", new(x + 235, p.Y + 55, Math.Max(220, width - 250), 160), Muted, .76f);
        }
        else
        {
            var defs = ColonyCatalog.Buildings.Where(b => b.Category == category).ToList();
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                Button(new(x + i % 3 * 208, y + i / 3 * 72, 196, 42), d.Name, () => { tool = "build:" + d.Id; material = d.Material; rotation = 0; tab = ""; }, hint: $"{d.Width}x{d.Height} footprint; {d.Cost} {d.Material} units. Materials are consumed by future construction.");
                Label($"{d.Width}x{d.Height} / {d.Cost} {d.Material}", x + i % 3 * 208 + 3, y + i / 3 * 72 + 43, Muted, .62f);
            }
            Paragraph("Choose a building, then click or drag the map. Q/E rotates its footprint. Cyan previews are valid; red previews explain an invalid placement. Plans persist without spending supplies.", new(x, p.Bottom - 134, width, 72), Muted, .76f);
        }
        Label("PLANS: " + world.Colony.Blueprints.Count + "    ZONES: " + world.Colony.Zones.Count, x, p.Bottom - 49, Cyan, .72f);
    }
    private IEnumerable<Entity> VisiblePeople(Rectangle p) => world.Entities.Where(e => world.Colony.People.ContainsKey(e.Id)).Skip(rowOffset).Take(Math.Max(1, (p.Height - 154) / 35));
    private void DrawWork(Rectangle p)
    {
        Button(new(p.X + 178, p.Y + 10, 182, 27), "Manual priorities", () => Execute(new(CommandKind.SetWorkMode, Quantity: world.Colony.ManualPriorities ? 0 : 1)), world.Colony.ManualPriorities, "1 highest, 4 lowest, blank disabled. Simple mode uses checkboxes.");
        int x = p.X + 170, top = p.Y + 53, cw = Math.Min(65, (p.Width - 190) / ColonyCatalog.Work.Length);
        for (int i = 0; i < ColonyCatalog.Work.Length; i++) Label(Fit(ColonyCatalog.Work[i], cw - 2, .57f), x + i * cw, top, Muted, .57f);
        int y = top + 29;
        foreach (var e in VisiblePeople(p))
        {
            var pawn = world.Colony.People[e.Id]; long id = e.Id;
            Button(new(p.X + 13, y, 145, 29), e.Name, () => Focus(id), selection.Contains(id), scale: .73f);
            for (int i = 0; i < ColonyCatalog.Work.Length; i++)
            {
                int index = i; int value = pawn.Priorities[i];
                Button(new(x + i * cw, y, cw - 3, 29), world.Colony.ManualPriorities ? value == 0 ? "-" : value.ToString() : value == 0 ? "-" : "X",
                    () => Execute(new(CommandKind.SetPriority, id, X: index, Quantity: world.Colony.ManualPriorities ? (pawn.Priorities[index] + 1) % 5 : pawn.Priorities[index] == 0 ? 3 : 0)),
                    value == 1, $"{ColonyCatalog.Work[i]}: skill {pawn.Skills[i]}/20; click cycles priority, blank disables.", scale: .83f);
                Fill(new(x + i * cw + 2, y + 26, (cw - 7) * pawn.Skills[i] / 20, 2), Gold);
            }
            y += 35;
        }
        Label("Lower numbers work first; 0 disables work. Wheel scrolls additional settlers.", p.X + 15, p.Bottom - 57, Muted, .7f);
    }
    private static Color ActivityColor(ScheduleActivity activity) => activity switch { ScheduleActivity.Work => new(153, 112, 74), ScheduleActivity.Sleep => new(77, 102, 145), ScheduleActivity.Recreation => new(115, 155, 93), ScheduleActivity.Study => new(142, 105, 169), _ => new(83, 104, 104) };
    private void DrawSchedule(Rectangle p)
    {
        int x = p.X + 170, cw = Math.Min(44, (p.Width - 187) / 24), top = p.Y + 94;
        int bx = p.X + 170;
        foreach (var activity in Enum.GetValues<ScheduleActivity>())
        {
            var a = activity; Button(new(bx, p.Y + 48, 120, 29), a.ToString(), () => scheduleBrush = a, scheduleBrush == a); bx += 127;
        }
        for (int i = 0; i < 24; i++) Label(i.ToString("00"), x + i * cw + 4, top, Muted, .63f);
        int y = top + 28;
        foreach (var e in VisiblePeople(p))
        {
            long id = e.Id; var pawn = world.Colony.People[id];
            Button(new(p.X + 13, y, 145, 30), e.Name, () => Focus(id), scale: .73f);
            for (int hour = 0; hour < 24; hour++)
            {
                int h = hour; var rect = new Rectangle(x + hour * cw, y, cw - 2, 30);
                Button(rect, pawn.Schedule[h].ToString()[..1], () => { if (pawn.Schedule[h] != scheduleBrush) Execute(new(CommandKind.SetSchedule, id, X: h, Quantity: (int)scheduleBrush)); }, hint: $"{h:00}:00 - {pawn.Schedule[h]}. Click/drag paints {scheduleBrush}.", paint: true, scale: .72f);
                Fill(new(rect.X + 2, rect.Bottom - 5, rect.Width - 4, 3), ActivityColor(pawn.Schedule[h]));
            }
            y += 35;
        }
        Label("Choose a brush, then click/drag across hours. Schedules survive save/load.", p.X + 15, p.Bottom - 57, Muted, .7f);
    }
    private void DrawAssign(Rectangle p)
    {
        if(world.Society.Enabled){Button(new(p.Right-244,p.Y+10,220,26),institutionsPane?"Policies":"Institutions",()=>institutionsPane=!institutionsPane,scale:.65f);if(institutionsPane){DrawInstitutions(p);return;}}
        int x = p.X + 164, cw = Math.Min(146, (p.Width - 181) / 7), y = p.Y + 73;
        for (int i = 0; i < 6; i++) Label(ColonyCatalog.PolicyKinds[i], x + i * cw + 3, p.Y + 48, Muted, .63f);
        Label("Allowed area", x + 6 * cw, p.Y + 48, Muted, .63f);
        foreach (var e in world.Entities.Where(e => world.Colony.People.ContainsKey(e.Id)).Skip(rowOffset).Take(4))
        {
            long id = e.Id; var pawn = world.Colony.People[id];
            Button(new(p.X + 13, y, 141, 28), e.Name, () => Focus(id), scale: .72f);
            for (int i = 0; i < 6; i++)
            {
                string kind = ColonyCatalog.PolicyKinds[i]; var presets = world.Colony.Policies.Where(q => q.Kind == kind).ToList();
                var current = presets.Find(q => q.Id == pawn.Policies[kind])!;
                Button(new(x + i * cw, y, cw - 4, 28), current.Name, () =>
                {
                    int index = presets.FindIndex(q => q.Id == pawn.Policies[kind]); Execute(new(CommandKind.AssignPolicy, id, DefinitionId: presets[(index + 1) % presets.Count].Id, Text: kind));
                }, hint: $"Click to cycle {kind} preset. Edit or copy presets below.", scale: .65f);
            }
            var zones = world.Colony.Zones.Where(z => z.Kind == ZoneKind.Allowed).ToList();
            Button(new(x + 6 * cw, y, cw - 4, 28), pawn.AllowedZoneId == 0 ? "Unrestricted" : zones.Find(z => z.Id == pawn.AllowedZoneId)!.Name, () =>
            {
                int index = zones.FindIndex(z => z.Id == pawn.AllowedZoneId); long next = index + 1 < zones.Count ? zones[index + 1].Id : 0;
                Execute(new(CommandKind.AssignPolicy, id, Quantity: (int)next, Text: "Area"));
            }, hint: "Cycle allowed areas created in Architect > Zones.", scale: .65f); y += 33;
        }
        int editorY = Math.Max(y + 12, p.Y + 221);
        Button(new(p.X + 14, editorY, 146, 27), policyKind, () =>
        {
            policyKind = ColonyCatalog.PolicyKinds[(Array.IndexOf(ColonyCatalog.PolicyKinds, policyKind) + 1) % 6]; policyId = world.Colony.Policies.First(q => q.Kind == policyKind).Id;
        }, hint: "Change the policy category.", scale: .7f);
        var choices = world.Colony.Policies.Where(q => q.Kind == policyKind).ToList();
        var preset = choices.Find(q => q.Id == policyId) ?? choices[0]; policyId = preset.Id;
        Button(new(p.X + 170, editorY, 180, 27), preset.Name, () => policyId = choices[(choices.FindIndex(q => q.Id == policyId) + 1) % choices.Count].Id, scale: .7f);
        Button(new(p.X + 358, editorY, 113, 27), "Copy preset", () =>
        {
            if (Execute(new(CommandKind.ClonePolicy, DefinitionId: policyId)).Accepted) policyId = world.Colony.Policies[^1].Id;
        }, hint: "Create an independent copy before editing.", scale: .68f);
        Button(new(p.X + 478, editorY, 108, 27), "Rename", () => BeginRename("Rename policy", preset.Name, name => Execute(new(CommandKind.Rename, DefinitionId: "policy:" + preset.Id, Text: name))), scale: .68f);
        int vx = p.X + 14;
        foreach (var entry in preset.Values)
        {
            string key = entry.Key;
            Button(new(vx, editorY + 40, 222, 28), key + ": " + entry.Value, () =>
            {
                int value = preset.Values[key]; int max = key == "Care level" ? 3 : key is "Min condition" or "Max quality" or "Minimum age" or "Strain limit" ? 100 : 1;
                int step = max == 100 ? 5 : 1; Execute(new(CommandKind.EditPolicy, DefinitionId: preset.Id, Quantity: value + step > max ? 0 : value + step, Text: key));
            }, hint: "Click to change. This preset is shared by every settler assigned to it.", scale: .68f); vx += 232;
        }
        Label("Food, medicine, allowed-area and equipment policies affect worker decisions.", p.X + 15, p.Bottom - 57, Muted, .69f);
    }
    private void DrawAnimals(Rectangle p, bool wildlife)
    {
        string[] headings = wildlife ? ["Name", "Location", "Disposition", "Hunt", "Tame"] : ["Name", "Handler", "Train", "Breed", "Slaughter", "Medicine"];
        int cw = (p.Width - 30) / headings.Length;
        for (int i = 0; i < headings.Length; i++) Label(headings[i], p.X + 17 + i * cw, p.Y + 53, Muted, .71f);
        int y = p.Y + 82;
        foreach (var e in world.Entities.Where(e => world.Colony.Animals.ContainsKey(e.Id)).Skip(rowOffset).Take(Math.Max(1, (p.Height - 158) / 33)))
        {
            long id = e.Id; var a = world.Colony.Animals[id];
            Rectangle Cell(int col) => new(p.X + 13 + col * cw, y, cw - 8, 27);
            Button(Cell(0), e.Name, () => Focus(id), hint: $"#{id}; health {e.Health}", scale: .65f);
            if (wildlife)
            {
                Label($"{e.X}, {e.Y}", Cell(1).X + 5, y + 3, Text, .7f); Label(Fit("Herbivore / wild", cw - 10, .69f), Cell(2).X + 5, y + 3, Muted, .69f);
                Button(Cell(3), "Hunt", () => Execute(new(CommandKind.Designate, DefinitionId: "Hunt", X: e.X, Y: e.Y, Z: e.Z)), world.Colony.Designations.Any(d => d.Kind == "Hunt" && d.X == e.X && d.Y == e.Y), "Use drafted attacks to hunt; manage husbandry through Engineering.", scale: .7f);
                Button(Cell(4), "Tame", () => Execute(new(CommandKind.Designate, DefinitionId: "Tame", X: e.X, Y: e.Y, Z: e.Z)), world.Colony.Designations.Any(d => d.Kind == "Tame" && d.X == e.X && d.Y == e.Y), "Marks taming; this does not instantly domesticate the animal.", scale: .7f);
            }
            else
            {
                var people = world.Colony.People.Keys.ToList();
                Button(Cell(1), a.HandlerId == 0 ? "None" : world.Find(a.HandlerId)!.Name, () => { int i = people.IndexOf(a.HandlerId); Execute(new(CommandKind.SetAnimalPolicy, id, Quantity: (int)(i + 1 < people.Count ? people[i + 1] : 0), Text: "handler")); }, scale: .7f);
                Button(Cell(2), a.Train ? "Yes" : "No", () => Execute(new(CommandKind.SetAnimalPolicy, id, Quantity: a.Train ? 0 : 1, Text: "train")), a.Train, scale: .7f);
                Button(Cell(3), a.Breed ? "Yes" : "No", () => Execute(new(CommandKind.SetAnimalPolicy, id, Quantity: a.Breed ? 0 : 1, Text: "breed")), a.Breed, scale: .7f);
                Button(Cell(4), a.Slaughter ? "Yes" : "No", () => Execute(new(CommandKind.SetAnimalPolicy, id, Quantity: a.Slaughter ? 0 : 1, Text: "slaughter")), a.Slaughter, scale: .7f);
                Button(Cell(5), "Level " + a.Medical, () => Execute(new(CommandKind.SetAnimalPolicy, id, Quantity: (a.Medical + 1) % 4, Text: "medical")), scale: .7f);
            }
            y += 33;
        }
        Label(wildlife ? "Hunt/tame are persistent orders; wheel scrolls the list." : "Planning registry includes wild animals. Policies activate after taming.", p.X + 15, p.Bottom - 57, Muted, .68f);
    }
    private void DrawResearch(Rectangle p)
    {
        var nodes = new[] { ("restoration", "Ancient restoration", "Survey the dormant engine; gather intact components."), ("agriculture", "Dusk agriculture", "Study low-light crops and fungal substrates."), ("formulae", "Prepared formulae", "Acquire a readable formula and a suitable study station.") };
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i]; int x = p.X + 17 + i * (p.Width - 30) / 3, width = (p.Width - 48) / 3;
            Button(new(x, p.Y + 56, width, 52), n.Item2, () => Execute(new(CommandKind.SetResearch, DefinitionId: n.Item1)), world.Colony.ResearchProject == n.Item1, "Select the current research intent; progress begins with scholarly jobs.");
            Paragraph(n.Item3, new(x + 5, p.Y + 125, width - 10, 110), Muted, .77f);
        }
        Label("Current project: " + (world.Colony.ResearchProject == "" ? "none" : world.Colony.ResearchProject), p.X + 19, p.Bottom - 97, Cyan);
        Label($"Study work: {world.Autonomy.Research.GetValueOrDefault(world.Colony.ResearchProject)}/1000. Build a study desk and assign Research.", p.X + 19, p.Bottom - 62, Muted, .7f);
    }
    private void DrawQuests(Rectangle p)
    {
        if(world.Region.Enabled){DrawRegionalQuests(p);return;}
        Label("The Dormant Observatory", p.X + 20, p.Y + 57, Cyan, .94f);
        Paragraph("Founding journal: an ancient daylight engine survives within the ruined observatory. Organize a refuge, mark a storage area and plan a study desk. The journal records your chosen intent; discovery and expedition rewards arrive later.", new(p.X + 20, p.Y + 95, p.Width - 55, 120), Muted, .8f);
        Label("Status: " + world.Colony.QuestState, p.X + 20, p.Y + 227, Text);
        Button(new(p.X + 20, p.Y + 266, 160, 30), "Accept", () => Execute(new(CommandKind.SetQuest, DefinitionId: "Accepted")), world.Colony.QuestState == "Accepted");
        Button(new(p.X + 188, p.Y + 266, 160, 30), "Decline", () => Execute(new(CommandKind.SetQuest, DefinitionId: "Declined")), world.Colony.QuestState == "Declined");
        Button(new(p.X + 356, p.Y + 266, 160, 30), "Track", () => Execute(new(CommandKind.SetQuest, Text: "track", Quantity: world.Colony.TrackQuest ? 0 : 1)), world.Colony.TrackQuest);
        Button(new(p.X + 524, p.Y + 266, 180, 30), "Locate engine", () => { var e = world.Entities.FirstOrDefault(e => e.DefinitionId == "relic"); if (e != null) Focus(e.Id); });
    }
    private void DrawWorldPanel(Rectangle p)
    {
        if(world.Society.Enabled&&!world.Solar.Enabled)Button(new(p.Right-274,p.Y+10,210,26),"Enable solar campaign",()=>Execute(new(CommandKind.CampaignAction,Text:"enable")),scale:.65f);
        if(world.Solar.Enabled){Button(new(p.Right-274,p.Y+10,210,26),solarWorldPane?"Regional map":"Solar campaign",()=>solarWorldPane=!solarWorldPane,scale:.65f);if(solarWorldPane){DrawSolarCampaign(p);return;}}
        if(world.Region.Enabled){DrawRegionalWorld(p);return;}
        Label("LOCAL SITE MAP", p.X + 20, p.Y + 54, Cyan, .85f);
        var map = new Rectangle(p.X + 22, p.Y + 89, 225, 225); batch.Draw(minimap, map, Color.White); Outline(map, Border);
        hits.Add(new(map, () =>
        {
            var point = ToUi(Mouse.GetState().Position); camera.Center = new((point.X - map.X) * world.Width / (float)map.Width, (point.Y - map.Y) * world.Height / (float)map.Height); camera.Clamp(world); tab = "";
        }, "Click to navigate the current local site.", false));
        Paragraph($"Observatory Refuge. Seed {world.Seed}. {world.Width} x {world.Height} tiles. The global regional simulation is not generated yet; this panel provides site navigation.", new(p.X + 280, p.Y + 89, p.Width - 310, 120), Muted, .8f);
        Button(new(p.X + 280, p.Y + 238, 250, 31), "Center on refuge", () => { CenterCamp(); tab = ""; });
        Button(new(p.X + 280, p.Y + 279, 250, 31), "Form caravan", null, hint: "Enable the living region through Factions to arrange expeditions.");
    }
    private void DrawHistory(Rectangle p)
    {
        if(world.Region.Enabled&&!world.Society.Enabled)Button(new(p.Right-264,p.Y+10,220,26),"Enable institutions",()=>Execute(new(CommandKind.InstitutionAction,Text:"enable")),scale:.65f);
        if(world.Society.Enabled){DrawLegacyHistory(p);return;}
        if(world.Region.Enabled){DrawRegionalHistory(p);return;}
        Label($"{world.Colony.People.Count} settlers | {world.Entities.Count} objects | {world.Colony.Blueprints.Count} plans | Day {world.Tick / World.TicksPerDay + 1}", p.X + 18, p.Y + 51, Cyan, .73f);
        int y = p.Y + 87;
        foreach (var ev in world.Events.AsEnumerable().Reverse().Skip(rowOffset).Take(Math.Max(1, (p.Height - 140) / 27)))
        {
            Label(Fit($"Tick {ev.Tick}: {ev.Message}", p.Width - 38, .7f), p.X + 18, y, Text, .7f); y += 27;
        }
    }
    private void DrawFactions(Rectangle p)
    {
        if(world.Region.Enabled){DrawRegionalFactions(p);return;}
        string[] names = ["Courts of the Vermilion Sun", "Pale Collegia", "Brass Pilgrimage", "Garden Remnants", "Salt Confederacies", "Choir Below"];
        for (int i = 0; i < names.Length; i++) Label(names[i] + " - no established contact", p.X + 20, p.Y + 59 + i * 35, i == 4 ? Gold : Muted, .78f);
        Button(new(p.X + 600, p.Y + 69, 205, 32), "Enable living region", ()=>Execute(new(CommandKind.RegionAction,Text:"enable")));
        Paragraph("This is the setting's faction reference. No fictional goodwill or agreements are reported as simulated state.", new(p.X + 600, p.Y + 125, p.Width - 625, 130), Muted, .76f);
    }
    private void DrawMenu(Rectangle p)
    {
        int x = p.X + 20, y = p.Y + 53;
        Button(new(x, y, 230, 33), $"Save settlement ({settings.Keys["Save"]})", Save); y += 41;
        Button(new(x, y, 230, 33), $"Load settlement ({settings.Keys["Load"]})", () => Load(false)); y += 41;
        Button(new(x, y, 230, 33), "Load previous backup", () => Load(true), hint: "Available after at least two saves."); y += 41;
        Button(new(x, y, 230, 33), "Resume", () => tab = ""); y += 41;
        Button(new(x, y, 230, 33), $"Help ({settings.Keys["Help"]})", () => { help = !help; tab = ""; }); y += 41;
        Button(new(x, y, 230, 33), "Save and quit", () => { if (TrySave()) Exit(); }); y += 41;
        Button(new(x, y, 230, 33), menuQuit ? "Confirm quit, unsaved" : "Quit without saving", () => { if (menuQuit) Exit(); else { menuQuit = true; status = "Click again to confirm quitting without saving."; } });
        int sx = p.X + 280;
        Button(new(sx, p.Y + 54, 236, 32), $"UI scale: {settings.Scale:0.00}", () => { settings.Scale = settings.Scale == 1 ? 1.1f : settings.Scale == 1.1f ? 1.25f : 1; SaveSettings(); }, hint: "Scales text, controls and hit testing.");
        Button(new(sx, p.Y + 95, 236, 32), "Reset key bindings", () => { settings.Keys = InterfaceSettings.Defaults(); SaveSettings(); });
        Button(new(sx + 245, p.Y + 54, 220, 32), settings.HighContrast ? "High contrast: ON" : "High contrast: OFF", () => { settings.HighContrast = !settings.HighContrast; SaveSettings(); }, hint: "Bright text and control outlines; persistent preference.", scale: .67f);
        Label("Ctrl+Up/Down: focus | Enter: activate", sx + 245, p.Y + 95, Text, .62f);
        Label("Content: " + (definitions.LoadedPacks.Count == 0 ? "base" : string.Join(", ", definitions.LoadedPacks)) + " | " + definitions.ManifestHash[..12], x, p.Bottom - 54, Muted, .58f);
        int columns = Math.Max(1, (p.Width - 292) / 222), rows = Math.Max(1, Math.Min(7, (p.Height - 219) / 27));
        int perPage = columns * rows, pages = (settings.Keys.Count + perPage - 1) / perPage;
        bindingPage = Math.Clamp(bindingPage, 0, pages - 1);
        Label("KEY BINDINGS (click, then press a key)", sx, p.Y + 139, Muted, .66f);
        Button(new(p.Right - 148, p.Y + 98, 132, 27), $"Keys {bindingPage + 1}/{pages}", () => bindingPage = (bindingPage + 1) % pages, hint: "Cycle pages of remappable controls.", scale: .65f);
        int i = 0;
        foreach (var entry in settings.Keys.Skip(bindingPage * perPage).Take(perPage))
        {
            string name = entry.Key; int col = i / rows, row = i % rows;
            Button(new(sx + col * 222, p.Y + 167 + row * 27, 214, 24), name + ": " + entry.Value, () => captureBinding = name, hint: "Escape cancels; duplicate bindings are rejected.", scale: .64f);
            i++;
        }
    }
    private void DrawContext()
    {
        if (context.Count == 0) return;
        var rect = new Rectangle(contextPoint.X, contextPoint.Y, 300, context.Count * 31 + 10); Fill(rect, Ink); Outline(rect, Border);
        for (int i = 0; i < context.Count; i++)
        {
            var entry = context[i];
            Button(new(rect.X + 5, rect.Y + 5 + i * 31, 290, 28), entry.Text, entry.Action == null ? null : () => { var action = entry.Action; context.Clear(); action(); }, hint: entry.Reason, scale: .69f);
        }
    }
    private void DrawLetter()
    {
        hits.Clear();
        var letter = world.Colony.Letters.Find(l => l.Id == letterId); if (letter == null) { letterId = 0; return; }
        Fill(new(0, 0, UiWidth, UiHeight), new Color(0, 0, 0, 180));
        var p = new Rectangle(UiWidth / 2 - 300, UiHeight / 2 - 160, 600, 320); Fill(p, Panel); Outline(p, Gold);
        Label(letter.Title, p.X + 23, p.Y + 18, Gold, 1);
        Paragraph(letter.Body, new(p.X + 23, p.Y + 65, 554, 166), Text, .83f);
        Button(new(p.X + 24, p.Bottom - 52, 170, 32), "Go to location", () => { camera.Center = new(letter.X, letter.Y); tab = ""; letterId = 0; });
        Button(new(p.X + 211, p.Bottom - 52, 170, 32), "Archive", () => { Execute(new(CommandKind.AcknowledgeLetter, letter.Id, Quantity: 1)); letterId = 0; });
        Button(new(p.X + 397, p.Bottom - 52, 170, 32), "Close", () => letterId = 0);
    }
    private void DrawRename()
    {
        Fill(new(0, 0, UiWidth, UiHeight), new Color(0, 0, 0, 180)); popupBounds = new(UiWidth / 2 - 240, UiHeight / 2 - 90, 480, 180); Fill(popupBounds, Panel); Outline(popupBounds, Gold);
        Label(textTitle, popupBounds.X + 20, popupBounds.Y + 17, Gold, .95f);
        Label(textValue + "_", popupBounds.X + 20, popupBounds.Y + 60, Text, .85f);
        Label("Enter confirms; Escape cancels; Backspace edits.", popupBounds.X + 20, popupBounds.Y + 108, Muted, .69f);
    }
    private void DrawCapture()
    {
        Fill(new(0, 0, UiWidth, UiHeight), new Color(0, 0, 0, 190));
        var rect = new Rectangle(UiWidth / 2 - 260, UiHeight / 2 - 80, 520, 160); Fill(rect, Panel); Outline(rect, Cyan);
        Label("Press a key for: " + captureBinding, rect.X + 20, rect.Y + 25, Cyan, .85f);
        Label("Escape cancels. Duplicate bindings are rejected.", rect.X + 20, rect.Y + 74, Muted, .7f);
    }
    private void DrawHelp()
    {
        var rect = new Rectangle(235, 83, 515, 237); Fill(rect, Panel); Outline(rect, Gold);
        Label("REFUGE CONTROLS", rect.X + 14, rect.Y + 9, Gold, .9f);
        string[] lines = ["WASD / middle drag: pan. Wheel: zoom.", "Click selects. Shift adds. Drag selects settlers.", "Right-click opens actions with failure reasons.", "Tab: Architect. F1-F9: management tabs.", "F10 saves. F11 loads. Shift+F11: backup.", "R drafts. Q/E rotates a blueprint. Escape closes.", "Space pauses. 1/2/3: speed. Period: one tick.", "F12 closes help. Menu edits scale and keys."];
        for (int i = 0; i < lines.Length; i++) Label(lines[i], rect.X + 14, rect.Y + 44 + i * 22, Muted, .75f);
    }
    private void DrawTooltip(string text)
    {
        var point = ToUi(Mouse.GetState().Position); int width = Math.Min(540, UiWidth - 30);
        var rect = new Rectangle(Math.Clamp(point.X + 15, 10, UiWidth - width - 10), Math.Clamp(point.Y + 22, 10, UiHeight - 92), width, 76);
        Fill(rect, Ink); Outline(rect, Border); Paragraph(text, new(rect.X + 10, rect.Y + 7, rect.Width - 20, 62), Text, .7f);
    }
}
