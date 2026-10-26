using System;
using System.IO;
using System.Reflection;
using FXDSP.Core;
using UnityEngine;

namespace FXDSP.DeliveryBootstrap
{
    internal static class TechnologyIcons
    {
        private const int Columns = 4, Rows = 3, Count = 12;
        private const int NativeIconSize = 80;
        private const string ResourceName = "FXDSP.DeliveryBootstrap.Assets.technology-icons.png";
        private static readonly FieldInfo IconField = typeof(TechProto).GetField("_iconSprite", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static Sprite[] _sprites;

        internal static void Load()
        {
            if (_sprites != null) return;
            if (IconField == null) throw new MissingFieldException(typeof(TechProto).FullName, "_iconSprite");
            byte[] bytes;
            using (var stream = typeof(TechnologyIcons).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidDataException("Embedded technology icon atlas missing.");
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); bytes = memory.ToArray(); }
            }
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var sprites = new Sprite[Count];
            var textures = new Texture2D[Count];
            try
            {
                if (!ImageConversion.LoadImage(atlas, bytes, false) || atlas.width % Columns != 0 || atlas.height % Rows != 0)
                    throw new InvalidDataException("Technology atlas must have twelve equal cells in a 4 by 3 grid.");
                var pixels = atlas.GetPixels32();
                // A white UI mask avoids colored fringes, and alpha cutoff removes extraction residue.
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    int brightness = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                    int alpha = pixel.a * (brightness >= 224 ? 255 : brightness) / 255;
                    pixels[i] = new Color32(245, 245, 245, alpha <= 8 ? (byte)0 : alpha >= 248 ? (byte)255 : (byte)alpha);
                }
                int cellWidth = atlas.width / Columns, cellHeight = atlas.height / Rows;
                for (int index = 0; index < Count; index++)
                {
                    int left = index % Columns * cellWidth;
                    int bottom = atlas.height - (index / Columns + 1) * cellHeight;
                    int minX = cellWidth, minY = cellHeight, maxX = -1, maxY = -1;
                    for (int y = 0; y < cellHeight; y++)
                        for (int x = 0; x < cellWidth; x++)
                            if (pixels[(bottom + y) * atlas.width + left + x].a > 32)
                            { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                    if (maxX < minX || maxY < minY) throw new InvalidDataException("Empty technology icon cell " + index);
                    minX = Math.Max(0, minX - 2); minY = Math.Max(0, minY - 2);
                    maxX = Math.Min(cellWidth - 1, maxX + 2); maxY = Math.Min(cellHeight - 1, maxY + 2);
                    int width = maxX - minX + 1, height = maxY - minY + 1;
                    int extent = Math.Max(width, height), padding = Math.Max(8, (extent + 11) / 12);
                    int size = extent + 2 * padding;
                    var tile = new Color32[size * size];
                    for (int i = 0; i < tile.Length; i++) tile[i] = new Color32(245, 245, 245, 0);
                    int offsetX = (size - width) / 2, offsetY = (size - height) / 2;
                    for (int y = 0; y < height; y++)
                        Array.Copy(pixels, (bottom + minY + y) * atlas.width + left + minX, tile, (offsetY + y) * size + offsetX, width);
                    // IconSet.Create copies exactly (0, 0, 80, 80) from each technology texture.
                    // Supplying a larger centered sprite leaves that native inline-icon copy empty.
                    var texture = new Texture2D(NativeIconSize, NativeIconSize, TextureFormat.RGBA32, false) {
                        name = "FXDSP.TechIcon." + index, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    textures[index] = texture;
                    texture.SetPixels32(ResizeMask(tile, size)); texture.Apply(false, false);
                    sprites[index] = Sprite.Create(texture, new Rect(0, 0, NativeIconSize, NativeIconSize), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    sprites[index].name = "FXDSP.TechIcon." + index;
                    sprites[index].hideFlags = HideFlags.HideAndDontSave;
                }
                _sprites = sprites;
            }
            catch
            {
                foreach (var sprite in sprites)
                    if (sprite != null) UnityEngine.Object.Destroy(sprite);
                foreach (var texture in textures)
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
            finally { UnityEngine.Object.Destroy(atlas); }
        }

        private static Color32[] ResizeMask(Color32[] source, int side)
        {
            var result = new Color32[NativeIconSize * NativeIconSize];
            double scale = (double)side / NativeIconSize, area = scale * scale;
            // Area filtering preserves thin rank strokes when reducing to the native 80px slot.
            for (int y = 0; y < NativeIconSize; y++)
            {
                double y0 = y * scale, y1 = (y + 1) * scale;
                int firstY = (int)Math.Floor(y0), lastY = Math.Min(side, (int)Math.Ceiling(y1));
                for (int x = 0; x < NativeIconSize; x++)
                {
                    double x0 = x * scale, x1 = (x + 1) * scale, alpha = 0;
                    int firstX = (int)Math.Floor(x0), lastX = Math.Min(side, (int)Math.Ceiling(x1));
                    for (int sy = firstY; sy < lastY; sy++)
                    {
                        double wy = Math.Min(y1, sy + 1) - Math.Max(y0, sy);
                        for (int sx = firstX; sx < lastX; sx++)
                        {
                            double wx = Math.Min(x1, sx + 1) - Math.Max(x0, sx);
                            alpha += source[sy * side + sx].a * wx * wy;
                        }
                    }
                    result[y * NativeIconSize + x] = new Color32(245, 245, 245, (byte)Math.Max(0, Math.Min(255, Math.Round(alpha / area))));
                }
            }
            return result;
        }

        internal static void Apply(TechProto tech)
        {
            if (_sprites == null) return;
            var definition = TradeResearch.Find(tech.ID);
            if (definition == null) return;
            // Atlas order: access, exchange I-V, efficiency I-VI.
            int index = !definition.Upgrade ? 0 : definition.ExchangeTier > 0 ? definition.ExchangeTier : 5 + definition.Level;
            if (index < 0 || index >= _sprites.Length) throw new InvalidDataException("Technology icon rank is out of range.");
            IconField.SetValue(tech, _sprites[index]);
        }
    }
}
