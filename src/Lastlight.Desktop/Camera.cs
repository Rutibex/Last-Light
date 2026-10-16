using Lastlight.Core;
using Microsoft.Xna.Framework;

namespace Lastlight.Desktop;

public sealed class Camera
{
    public const int TileSize = 32;
    public Vector2 Center { get; set; }
    public float Zoom { get; private set; } = 1;
    public Rectangle Viewport { get; set; }
    public Vector2 ScreenToTile(Vector2 screen) => Center + (screen - new Vector2(Viewport.Center.X, Viewport.Center.Y)) / (TileSize * Zoom);
    public Vector2 TileToScreen(Vector2 tile) => new Vector2(Viewport.Center.X, Viewport.Center.Y) + (tile - Center) * TileSize * Zoom;
    public void SetZoom(float zoom) => Zoom = Math.Clamp(zoom, .35f, 3f);
    public void ZoomAt(Vector2 mouse, float factor, World world)
    {
        var before = ScreenToTile(mouse); SetZoom(Zoom * factor);
        Center += before - ScreenToTile(mouse); Clamp(world);
    }
    public void Clamp(World world) => Center = Vector2.Clamp(Center, Vector2.Zero, new(world.Width, world.Height));
    public Matrix Transform => Matrix.CreateTranslation(-Center.X * TileSize, -Center.Y * TileSize, 0) *
        Matrix.CreateScale(Zoom) * Matrix.CreateTranslation(Viewport.Center.X, Viewport.Center.Y, 0);
}
