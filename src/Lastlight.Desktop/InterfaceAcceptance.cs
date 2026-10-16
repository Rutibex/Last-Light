using Lastlight.Core;
using Microsoft.Xna.Framework;

namespace Lastlight.Desktop;

public sealed partial class LastlightGame
{
    private string interfaceFingerprint = "";
    private void RunInterfaceChecks()
    {
        void Require(bool valid, string message) { if (!valid) throw new InvalidDataException("UI workflow failed: " + message); }
        void Click(string text)
        {
            var hit = hits.LastOrDefault(h => h.Text == text && h.Action != null);
            Require(hit != null, "Control not found: " + text); hit!.Action!();
        }
        void ClickAt(Point point)
        {
            var hit = hits.LastOrDefault(h => h.Bounds.Contains(point) && h.Action != null);
            Require(hit != null, "Grid control not found."); hit!.Action!();
        }
        long pawn = world.Colony.People.Keys.First();
        switch (frame)
        {
            case 2: Click("Work"); break;
            case 4:
                int cw = Math.Min(65, (PanelArea.Width - 190) / ColonyCatalog.Work.Length);
                ClickAt(new(PanelArea.X + 170 + cw * 6 + 5, PanelArea.Y + 86));
                Require(world.Colony.People[pawn].Priorities[6] == 4, "Work matrix callback did not edit priority.");
                Click("Schedule"); break;
            case 6: Click("Study"); break;
            case 8:
                int hourWidth = Math.Min(44, (PanelArea.Width - 187) / 24);
                for (int h = 8; h < 12; h++) ClickAt(new(PanelArea.X + 170 + h * hourWidth + 5, PanelArea.Y + 132));
                Require(world.Colony.People[pawn].Schedule.Skip(8).Take(4).All(s => s == ScheduleActivity.Study), "Schedule callbacks failed.");
                Click("Assign"); break;
            case 10: Click("Copy preset"); break;
            case 12:
                Click("Duskgrain: 1");
                Require(world.Colony.Policies.First(p => p.Id == "food-default").Values["Duskgrain"] == 1, "Copied preset is not independent.");
                Click("Architect"); category = "Furniture"; break;
            case 14: Click("Bed"); break;
            case 16:
                Click("Sun-brass"); rotation = 1;
                dragStart = PointAt(70, 68); dragTool = tool;
                CompleteMapDrag(dragStart, false, 1);
                Require(world.Colony.Blueprints.Count == 1 && world.Colony.Blueprints[0].MaterialId == "sun-brass" && World.BlueprintCells(world.Colony.Blueprints[0]).Contains((71, 68)), "Architect footprint/material failed.");
                tool = ""; Click("Architect"); break;
            case 18: Click("Zones"); break;
            case 20: Click("Stockpile zone"); break;
            case 22:
                var wood = world.Entities.First(e => e.DefinitionId == "wood");
                dragStart = PointAt(wood.X, wood.Y); dragTool = tool; CompleteMapDrag(dragStart, false, 2);
                Require(world.Resources("wood").Stored == 40, "Zone placement did not update stored count."); tool = ""; break;
            case 24: Click("Timber: yes"); Require(world.Resources("wood").Stored == 0, "Filter callback did not update HUD quantities."); break;
            case 26: Click("Timber: no"); break;
            case 28:
                SelectEntity(pawn); SetDrafted(true); Require(world.Colony.People[pawn].Drafted, "Draft callback failed.");
                var target = world.Entities.First(e => e.DefinitionId == "grain"); OpenContext(PointAt(target.X, target.Y), false); break;
            case 30: Click("Forbid object"); Require(world.Entities.First(e => e.DefinitionId == "grain").Forbidden, "Context forbid failed."); break;
            case 32:
                var a = PointAt(59, 62); var b = PointAt(69, 66); dragStart = a; dragTool = ""; CompleteMapDrag(b, false, 4);
                Require(selection.Count == 5, "Drag selection did not select five settlers."); break;
            case 34: Click("NEW: The Observatory Refuge"); break;
            case 36: Click("Archive"); Require(world.Colony.Letters[0].Archived, "Letter callback did not persist archive."); break;
            case 38:
                var snapshotPath = Path.Combine(SaveStore.DefaultDirectory, "stage-b-ui-check.lastlight");
                SaveStore.Write(snapshotPath, world, CurrentView(), definitions); interfaceFingerprint = SaveStore.Fingerprint(world);
                Execute(new(CommandKind.CancelBlueprint, world.Colony.Blueprints[0].Id));
                Restore(SaveStore.Read(snapshotPath, definitions));
                Require(SaveStore.Fingerprint(world) == interfaceFingerprint && selection.Count == 5, "UI-facing save/load restoration differs."); break;
            case 40: Click("Menu"); break;
            case 42:
                Click("UI scale: 1.00"); Require(settings.Scale == 1.1f, "Scale callback failed.");
                settings.Scale = 1.25f; break;
            case 44:
                Require(hits.Where(h => h.Action != null).All(h => h.Bounds.Right <= UiWidth && h.Bounds.Bottom <= UiHeight), "A scaled control extends off-screen.");
                settings.Scale = 1; tab = ""; break;
            case 46:
                world.Validate(definitions);
                if (screenshot != null)
                {
                    CaptureImage(screenshot);
                }
                Console.WriteLine("UI WORKFLOW PASS: actual rendered callbacks, work/schedule/policy grids, blueprints, stockpile filters, draft, context, selection, letter, persistence, UI scaling.");
                Exit(); break;
        }
    }
    private Point PointAt(int x, int y)
    {
        var point = camera.TileToScreen(new(x + .5f, y + .5f)); return new((int)point.X, (int)point.Y);
    }
}
