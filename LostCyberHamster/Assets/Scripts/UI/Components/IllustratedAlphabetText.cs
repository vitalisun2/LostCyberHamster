using System;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Собирает локализованную строку из рисованных латинских и русских глифов.</summary>
    public static class IllustratedAlphabetText
    {
        private const string GlyphClass = "illustrated-alphabet__glyph";
        private const string SpaceClass = "illustrated-alphabet__space";
        private const string FallbackClass = "illustrated-alphabet__fallback";

        public static void Render(VisualElement host, string text)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            string normalized = (text ?? string.Empty).ToUpperInvariant();
            host.Clear();
            host.tooltip = normalized;

            // Неподдержанный знак сохраняет читаемость будущей локализации обычным текстом.
            foreach (char character in normalized)
            {
                if (!char.IsWhiteSpace(character) && !IsSupported(character))
                {
                    var fallback = new Label(normalized)
                    {
                        pickingMode = PickingMode.Ignore
                    };
                    fallback.AddToClassList(FallbackClass);
                    host.Add(fallback);
                    return;
                }
            }

            // Каждая буква получает собственный художественный Sprite через USS-класс Unicode.
            foreach (char character in normalized)
            {
                var element = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                if (char.IsWhiteSpace(character))
                {
                    element.AddToClassList(SpaceClass);
                }
                else
                {
                    element.AddToClassList(GlyphClass);
                    element.AddToClassList($"{GlyphClass}--u{(int)character:x4}");
                }
                host.Add(element);
            }
        }

        private static bool IsSupported(char character)
        {
            return character >= 'A' && character <= 'Z' ||
                   character >= 'А' && character <= 'Я' ||
                   character == 'Ё';
        }
    }
}
