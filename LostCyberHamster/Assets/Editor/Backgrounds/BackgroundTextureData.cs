using UnityEngine;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Подготовленные пиксели и точный видимый прямоугольник слоя.</summary>
    public sealed class BackgroundTextureData
    {
        public BackgroundTextureData(RectInt sourceBounds, int width, int height, Color32[] pixels)
        {
            SourceBounds = sourceBounds;
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public RectInt SourceBounds { get; }
        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }
        public Rect SpriteRect => new Rect(BackgroundTexturePreparation.Gutter, BackgroundTexturePreparation.Gutter,
            SourceBounds.width, SourceBounds.height);

        /// <summary>Создаёт текстуру для черновой композиции и экспорта PNG.</summary>
        public Texture2D CreateTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(Pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
