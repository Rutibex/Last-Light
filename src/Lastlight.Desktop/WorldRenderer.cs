using Lastlight.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Lastlight.Desktop;
public sealed partial class LastlightGame
{
    private void DrawWorld()
    {
        var a = camera.ScreenToTile(new(MapArea.Left, MapArea.Top));
        var b = camera.ScreenToTile(new(MapArea.Right, MapArea.Bottom));
        int minX = Math.Max(0, (int)MathF.Floor(a.X) - 1), minY = Math.Max(0, (int)MathF.Floor(a.Y) - 1);
        int maxX = Math.Min(world.Width - 1, (int)MathF.Ceiling(b.X)), maxY = Math.Min(world.Height - 1, (int)MathF.Ceiling(b.Y));
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
        {
            var cell = world.CellAt(x, y, level);
            var tint = ColorFrom(definitions.Terrain[cell.TerrainId].Color);
            var rect = new Rectangle(x * 32, y * 32, 32, 32);
            if(level==0&&world.Solar.Enabled&&world.Tick<world.Solar.FlareUntil)tint=Color.Lerp(tint,new Color(180,76,58),.25f);
            Fill(rect, tint * (.9f + cell.Variant * .016f) * (level==0?(world.Solar.Enabled?(float)(.25+world.LightAt(world.Index(x,y,level))*.0075):1f):(float)(.52+world.LightAt(world.Index(x,y,level))*.0048)));
            switch (cell.TerrainId)
            {
                case "soil":
                    if (cell.Variant < 4)
                    {
                        Fill(new(x * 32 + 4 + cell.Variant * 4, y * 32 + 10, 2, 5), new(83, 88, 54));
                        Fill(new(x * 32 + 18, y * 32 + 20 + cell.Variant, 2, 6), new(111, 111, 64));
                    }
                    if (cell.Variant == 0 && (x * 13 + y * 17) % 7 == 0) DrawShrub(x * 32, y * 32);
                    break;
                case "water":
                    Fill(new(x * 32 + 3, y * 32 + 10 + cell.Variant, 17, 1), new(74, 116, 122));
                    Fill(new(x * 32 + 12, y * 32 + 24, 15, 1), new(60, 94, 104)); break;
                case "stone":
                    Fill(new(x * 32 + 2, y * 32 + 3, 27, 25), new(71, 72, 79));
                    Fill(new(x * 32 + 4, y * 32 + 3, 23, 3), new(106, 103, 111));
                    Fill(new(x * 32 + 20, y * 32 + 7, 2, 13), new(49, 50, 58)); break;
                case "floor":
                    Outline(new(x * 32 + 1, y * 32 + 1, 30, 30), new(92, 87, 85), 1); break;
                case "sand":
                    Fill(new(x * 32 + 5 + cell.Variant, y * 32 + 15, 12, 1), new(140, 118, 85)); break;
            }
            DrawSliceOverlay(x,y);DrawPhysicalOverlay(x,y);
            if (grid) Outline(rect, new Color(0, 0, 0, 35), 1);
        }
        foreach(var crop in world.Autonomy.Crops.Values)
        {
            int cx=crop.Cell%world.Width,cy=crop.Cell/world.Width%world.Height;
            if(crop.Cell/(world.Width*world.Height)!=level || cx<minX || cx>maxX || cy<minY || cy>maxY)continue;
            bool ripe=world.CropReady(crop);
            for(int i=0;i<3;i++)
            { Fill(new(cx*32+6+i*8,cy*32+10,3,17),ripe?Gold:new Color(108,148,81)); Fill(new(cx*32+4+i*8,cy*32+10,7,5),ripe?Gold:new Color(118,160,93)); }
        }
        foreach (var e in world.Entities.OrderBy(e => e.Y).ThenBy(e => e.Id))
        {
            if (world.IsAway(e.Id) || e.Z != level || e.X < minX || e.X > maxX || e.Y < minY || e.Y > maxY) continue;
            int x = e.X * 32, y = e.Y * 32; var def = definitions.Entities[e.DefinitionId];
            batch.Draw(circle, new Rectangle(x + 4, y + 22, 24, 8), new Color(0, 0, 0, 80));
            var color = ColorFrom(def.Color);
            switch (def.Kind)
            {
                case EntityKind.Person:
                    batch.Draw(circle, new Rectangle(x + 6, y + 11, 21, 20), e.Id % 2 == 0 ? new Color(113, 153, 147) : new Color(153, 103, 90));
                    batch.Draw(circle, new Rectangle(x + 9, y + 1, 15, 16), color);
                    Fill(new(x + 11, y + 3, 11, 3), new(57, 40, 37)); break;
                case EntityKind.Creature:
                    if(e.DefinitionId=="vault-guardian"){Fill(new(x+7,y+7,18,21),new Color(88,130,135));Outline(new(x+7,y+7,18,21),Gold,2);Fill(new(x+10,y+2,12,10),Cyan);Fill(new(x+8,y+24,4,8),Gold);Fill(new(x+20,y+24,4,8),Gold);break;}
                    if(e.DefinitionId=="raider")
                    {batch.Draw(circle,new Rectangle(x+6,y+11,21,20),new Color(159,62,54));batch.Draw(circle,new Rectangle(x+9,y+1,15,16),new Color(181,143,108));Fill(new(x+9,y+7,15,5),Ink);break;}
                    batch.Draw(circle, new Rectangle(x + 3, y + 9, 25, 18), color);
                    batch.Draw(circle, new Rectangle(x + 21, y + 5, 11, 12), color * .8f);
                    Fill(new(x + 23, y + 3, 2, 5), Gold); Fill(new(x + 28, y + 2, 2, 5), Gold); break;
                case EntityKind.Building:
                    foreach(var cell in world.BuildingCells(e))
                    { Fill(new(cell.X*32+1,cell.Y*32+2,30,29),color*.7f); Outline(new(cell.X*32+2,cell.Y*32+3,28,26),color,2); }
                    if(e.DefinitionId=="bed") { Fill(new(x+5,y+5,22,12),Text); Fill(new(x+5,y+18,22,9),new Color(90,124,158)); }
                    else if(e.DefinitionId=="stove") { batch.Draw(circle,new Rectangle(x+6,y+7,20,20),Ink); batch.Draw(circle,new Rectangle(x+11,y+12,10,10),Gold); }
                    else if(e.DefinitionId is "wall" or "door") { Fill(new(x+1,y,30,23),color); Outline(new(x+1,y,30,23),new Color(155,146,141),2); }
                    else Fill(new(x+7,y+7,18,16),color);
                    break;
                case EntityKind.ItemStack:
                    for (int i = 0; i < 3; i++)
                    {
                        if (e.DefinitionId == "wood") { Fill(new(x + 4, y + 10 + i * 6, 25, 5), color); Fill(new(x + 7, y + 11 + i * 6, 17, 1), Gold * .65f); }
                        else batch.Draw(circle, new Rectangle(x + 4 + i * 6, y + 8 + (i % 2) * 5, 14, 17), color);
                    }
                    break;
                case EntityKind.Artifact:
                    batch.Draw(circle, new Rectangle(x + 3, y + 3, 27, 27), color * .4f);
                    Outline(new(x + 7, y + 7, 19, 19), color, 3);
                    batch.Draw(circle, new Rectangle(x + 11, y + 11, 11, 11), new(221, 253, 222)); break;
            }
            if(world.Body(e.Id) is { } body)
            {
                if(body.WeaponId!="unarmed") {Fill(new(x+25,y+12,3,18),body.WeaponId=="bow"?Gold:Text);Fill(new(x+23,y+20,8,2),Text);}
                if(body.ArmorId!="")Outline(new(x+6,y+13,20,16),Cyan,1);
                if(body.Dead){Fill(new(x+4,y+20,24,9),Ink*.8f);Stroke(new Vector2(x+10,y+10),new Vector2(x+22,y+22),Muted,2);Stroke(new Vector2(x+22,y+10),new Vector2(x+10,y+22),Muted,2);}
                else if(body.Downed){Outline(new(x+2,y+1,28,30),Red,2);Label("+",x+11,y+10,Red,.7f);}
                if(body.Captured)Outline(new(x+2,y+1,28,30),Gold,1);
                if(e.Health<100){Fill(new(x+2,y+31,28,3),Ink);Fill(new(x+2,y+31,(int)(28*e.Health/100f),3),Red);}
            }
            if(def.Kind==EntityKind.Person && world.Autonomy.Jobs.Find(j=>j.PawnId==e.Id) is {Cargo.Count:>0}) Fill(new(x+22,y+21,9,9),Gold);
            if (e.Forbidden) { Fill(new(x + 24, y + 2, 5, 8), Red); Fill(new(x + 24, y + 12, 5, 3), Red); }
            if (selection.Contains(e.Id)) Outline(new(x - 2, y - 2, 36, 36), new(237, 203, 128), 2);
        }
        DrawPhysicalNetworkEdges();
        foreach(var trace in world.Danger.Traces)
        {
            if(trace.FromCell/(world.Width*world.Height)!=level || trace.ToCell/(world.Width*world.Height)!=level)continue;
            var start=new Vector2(trace.FromCell%world.Width*32+16,trace.FromCell/world.Width%world.Height*32+16);
            var end=new Vector2(trace.ToCell%world.Width*32+16,trace.ToCell/world.Width%world.Height*32+16);
            Stroke(start,end,(trace.Hit?Gold:Muted)*.75f,2);
        }
        foreach(long id in selection)
        {
            var pawn=world.Find(id);if(pawn==null || pawn.Z!=level || !world.Colony.People.ContainsKey(id) || !world.Colony.People[id].Drafted)continue;
            int range=world.Weapon(id,definitions).Range;var center=new Vector2(pawn.X*32+16,pawn.Y*32+16);float r=range*32;
            Stroke(center+new Vector2(0,-r),center+new Vector2(r,0),Cyan*.35f,1);Stroke(center+new Vector2(r,0),center+new Vector2(0,r),Cyan*.35f,1);Stroke(center+new Vector2(0,r),center+new Vector2(-r,0),Cyan*.35f,1);Stroke(center+new Vector2(-r,0),center+new Vector2(0,-r),Cyan*.35f,1);
        }
        var m = Mouse.GetState();
        if (MapArea.Contains(ToUi(m.Position)) && !InterfaceContains(ToUi(m.Position)) && !MinimapArea.Contains(ToUi(m.Position)))
        {
            var t = camera.ScreenToTile(ToUiVector(m.Position)); int x = (int)MathF.Floor(t.X), y = (int)MathF.Floor(t.Y);
            if (world.Contains(x, y, level)) Outline(new(x * 32, y * 32, 32, 32), tool == "" ? new Color(215, 230, 224, 160) : Gold, 1);
        }
    }
    private void Stroke(Vector2 start,Vector2 end,Color color,float thickness)
    {
        var delta=end-start;float length=delta.Length();if(length<.01f)return;
        batch.Draw(pixel,start,null,color,MathF.Atan2(delta.Y,delta.X),Vector2.Zero,new Vector2(length,thickness),SpriteEffects.None,0);
    }
    private void DrawShrub(int x, int y)
    {
        batch.Draw(circle, new Rectangle(x + 3, y + 17, 28, 13), new Color(0, 0, 0, 35));
        Fill(new(x + 14, y + 9, 4, 18), new(80, 60, 42));
        batch.Draw(circle, new Rectangle(x + 1, y - 4, 30, 27), new(58, 68, 45));
        batch.Draw(circle, new Rectangle(x + 6, y - 6, 22, 20), new(89, 96, 53));
    }
}
