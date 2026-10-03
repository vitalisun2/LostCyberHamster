using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Восстанавливает полный растровый холст выбранного слоя Procreate.</summary>
    public static class ProcreateLayerDecoder
    {
        /// <summary>Собирает RGBA-тайлы в straight-alpha RGBA с нижним началом координат.</summary>
        public static ProcreateRasterLayer Decode(ProcreateDocument document, ProcreateNode node)
        {
            // Выбранный слой должен принадлежать прочитанному и неизменённому исходнику.
            if (document == null || node == null)
                throw new ArgumentNullException(document == null ? nameof(document) : nameof(node));
            if (node.IsGroup || !ContainsNode(document.Roots, node))
                throw new ArgumentException("Выберите растровый слой этого Procreate.", nameof(node));
            if (node.UnsupportedReason != null)
                throw new NotSupportedException($"Слой '{node.Name}' нельзя экспортировать: {node.UnsupportedReason}.");
            var file = new FileInfo(document.FilePath);
            if (file.Length != document.FileLength || file.LastWriteTimeUtc.Ticks != document.LastWriteTicks)
                throw new IOException("Procreate изменился после открытия. Откройте файл заново.");

            // Отсутствующие тайлы представляют прозрачные части исходного холста.
            var pixels = new Color32[checked(document.Width * document.Height)];
            var coordinates = new HashSet<long>();
            string prefix = node.Id + "/";
            using var stream = new FileStream(document.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) ||
                    !entry.FullName.EndsWith(".lz4", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Крайние тайлы хранят ширину и высоту остатка холста, а не полный tileSize.
                ReadCoordinates(entry.FullName.Substring(prefix.Length), out int column, out int row);
                if (column < 0 || row < 0 || column >= (document.Width + document.TileSize - 1) / document.TileSize ||
                    row >= (document.Height + document.TileSize - 1) / document.TileSize ||
                    !coordinates.Add(((long)column << 32) | (uint)row))
                    throw new InvalidDataException($"Некорректные или повторные координаты тайла '{entry.FullName}'.");
                int x = column * document.TileSize;
                int y = row * document.TileSize;
                int width = Math.Min(document.TileSize, document.Width - x);
                int height = Math.Min(document.TileSize, document.Height - y);
                int expectedBytes = checked(width * height * 4);
                if (entry.Length < 4 || entry.Length > expectedBytes * 2L + 4096)
                    throw new InvalidDataException($"Некорректный размер тайла '{entry.FullName}'.");
                using var tileStream = entry.Open();
                using var buffer = new MemoryStream((int)entry.Length);
                tileStream.CopyTo(buffer);
                byte[] tile = ProcreateLz4Decoder.Decode(buffer.ToArray(), expectedBytes);
                CopyTile(tile, pixels, document.Width, x, y, width, height, node.Opacity);
            }
            return new ProcreateRasterLayer(document.Width, document.Height, pixels);
        }

        /// <summary>Проверяет принадлежность выбранного объекта дереву документа.</summary>
        private static bool ContainsNode(IReadOnlyList<ProcreateNode> nodes, ProcreateNode selected)
        {
            foreach (var node in nodes)
                if (ReferenceEquals(node, selected) || ContainsNode(node.Children, selected))
                    return true;
            return false;
        }

        /// <summary>Читает пару column~row из имени тайла.</summary>
        private static void ReadCoordinates(string name, out int column, out int row)
        {
            string[] coordinates = name.Substring(0, name.Length - 4).Split('~');
            if (coordinates.Length != 2 ||
                !int.TryParse(coordinates[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out column) ||
                !int.TryParse(coordinates[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
                throw new InvalidDataException($"Некорректное имя тайла Procreate: '{name}'.");
        }

        /// <summary>Преобразует premultiplied RGBA тайла в straight-alpha RGBA Unity.</summary>
        private static void CopyTile(byte[] tile, Color32[] pixels, int canvasWidth,
            int x, int y, int width, int height, double opacity)
        {
            for (int row = 0; row < height; row++)
            {
                // Procreate хранит строки и координаты тайлов от нижнего края холста.
                int source = row * width * 4;
                int destination = (y + row) * canvasWidth + x;
                for (int column = 0; column < width; column++, source += 4)
                {
                    byte alpha = tile[source + 3];
                    if (alpha == 0)
                        continue;

                    // Восстанавливаем цвет до применения прозрачности слоя и группы.
                    pixels[destination + column] = new Color32(
                        Unpremultiply(tile[source], alpha), Unpremultiply(tile[source + 1], alpha),
                        Unpremultiply(tile[source + 2], alpha), (byte)Math.Round(alpha * opacity));
                }
            }
        }

        /// <summary>Восстанавливает канал straight-alpha с округлением и ограничением 255.</summary>
        private static byte Unpremultiply(byte color, byte alpha)
        {
            return (byte)Math.Min(255, (color * 255 + alpha / 2) / alpha);
        }
    }
}
