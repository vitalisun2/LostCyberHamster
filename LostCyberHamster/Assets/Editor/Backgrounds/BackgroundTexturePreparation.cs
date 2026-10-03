using System;
using Assets.Scripts;
using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Обрезает прозрачные поля и добавляет служебные пиксели вне спрайта.</summary>
    public static class BackgroundTexturePreparation
    {
        public const int Gutter = 4;
        public const float PixelsPerUnit = Consts.PixelsPerUnit;

        /// <summary>Сохраняет рисунок, смещение на холсте и точную длину горизонтального повтора.</summary>
        public static BackgroundTextureData Prepare(ProcreateRasterLayer raster)
        {
            // Находим границы рисунка в исходном холсте.
            var minX = raster.Width;
            var minY = raster.Height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < raster.Height; y++)
            for (var x = 0; x < raster.Width; x++)
            {
                if (raster.Pixels[y * raster.Width + x].a == 0)
                    continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY)
                throw new InvalidOperationException("Выбранный слой полностью прозрачный.");

            // Повторяем соседние пиксели за пределами точного SpriteRect.
            var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var width = RoundToFour(bounds.width + Gutter * 2);
            var height = RoundToFour(bounds.height + Gutter * 2);
            if (width > 16384 || height > 16384)
                throw new InvalidOperationException($"Размер слоя {width}×{height} превышает предел импорта 16384.");
            var pixels = new Color32[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                var sourceY = bounds.y + Mathf.Clamp(y - Gutter, 0, bounds.height - 1);
                for (var x = 0; x < width; x++)
                {
                    var wrappedX = (x - Gutter) % bounds.width;
                    if (wrappedX < 0)
                        wrappedX += bounds.width;
                    pixels[y * width + x] = raster.Pixels[sourceY * raster.Width + bounds.x + wrappedX];
                }
            }
            return new BackgroundTextureData(bounds, width, height, pixels);
        }

        /// <summary>Округляет служебную текстуру до блока 4×4.</summary>
        private static int RoundToFour(int value) => (value + 3) / 4 * 4;
    }
}
