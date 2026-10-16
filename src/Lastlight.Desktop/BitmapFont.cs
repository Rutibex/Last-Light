using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Lastlight.Desktop;

// Bundled raster atlas avoids MGCB, installed-font dependencies, and runtime content compilation.
public sealed class BitmapFont : IDisposable
{
    private readonly Texture2D texture;
    public BitmapFont(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path); texture = Texture2D.FromStream(device, stream);
    }
    public void Draw(SpriteBatch batch, string text, Vector2 position, Color color, float scale = 1)
    {
        Vector2 cursor = position;
        foreach (char character in text)
        {
            if (character == '\n') { cursor.X = position.X; cursor.Y += 25 * scale; continue; }
            int code = character is >= ' ' and <= '~' ? character : '?';
            int index = code - 32;
            batch.Draw(texture, cursor, new Rectangle(index % 16 * 24, index / 16 * 28, 24, 28), color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            // Deliberately fixed spacing gives consistent tool labels; glyph atlas includes generous bounds.
            cursor.X += (character == ' ' ? 6 : 11) * scale;
        }
    }
    public static float Width(string text, float scale = 1) => text.Sum(c => c == ' ' ? 6 : 11) * scale;
    public void Dispose() => texture.Dispose();
}
