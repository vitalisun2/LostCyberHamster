using System.Collections.Generic;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Метаданные исходного Procreate и дерево его слоёв.</summary>
    public sealed class ProcreateDocument
    {
        public string FilePath { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<ProcreateNode> Roots { get; }
        internal int TileSize { get; }
        internal long FileLength { get; }
        internal long LastWriteTicks { get; }

        /// <summary>Сохраняет размеры, дерево и состояние прочитанного исходника.</summary>
        internal ProcreateDocument(string filePath, int width, int height, int tileSize,
            IReadOnlyList<ProcreateNode> roots, long fileLength, long lastWriteTicks)
        {
            FilePath = filePath;
            Width = width;
            Height = height;
            TileSize = tileSize;
            Roots = roots;
            FileLength = fileLength;
            LastWriteTicks = lastWriteTicks;
        }
    }
}
