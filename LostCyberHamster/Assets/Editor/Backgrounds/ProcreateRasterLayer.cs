using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Полный холст слоя: RGBA без premultiply, строки от нижнего края.</summary>
    public sealed class ProcreateRasterLayer
    {
        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }

        /// <summary>Сохраняет декодированные пиксели в координатах Unity.</summary>
        internal ProcreateRasterLayer(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }
}
