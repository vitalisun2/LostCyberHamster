using System;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Собирает локализованную строку из рисованных латинских и русских глифов.</summary>
    public static class IllustratedAlphabetText
    {
        private const string GlyphClass = "illustrated-alphabet__glyph";
        private const string WordClass = "illustrated-alphabet__word";
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
            char previous = '\0';
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

            var word = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            word.AddToClassList(WordClass);
            host.Add(word);

            // Все глифы имеют одну высоту и базовую линию. Ширина берётся из
            // нормализованного рисунка, поэтому буквы не растягиваются и не скачут.
            foreach (char character in normalized)
            {
                var element = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                if (char.IsWhiteSpace(character))
                {
                    element.AddToClassList(SpaceClass);
                    element.style.width = 38f;
                    previous = '\0';
                }
                else
                {
                    element.AddToClassList(GlyphClass);
                    element.AddToClassList($"{GlyphClass}--u{(int)character:x4}");
                    element.style.width = GlyphAspect(character) * 100f;
                    if (previous != '\0')
                    {
                        element.style.marginLeft = IllustratedAlphabetKerning.GetMargin(previous, character);
                    }
                    previous = character;
                }
                word.Add(element);
            }
        }

        private static float GlyphAspect(char character)
        {
            return character switch
            {
                'A' => 0.8242f, 'B' => 0.7930f, 'C' => 0.7695f, 'D' => 0.7891f,
                'E' => 0.6680f, 'F' => 0.6602f, 'G' => 0.8320f, 'H' => 0.7734f,
                'I' => 0.4609f, 'J' => 0.7266f, 'K' => 0.7852f, 'L' => 0.6602f,
                'M' => 0.9141f, 'N' => 0.7695f, 'O' => 0.8398f, 'P' => 0.7500f,
                'Q' => 0.7930f, 'R' => 0.7656f, 'S' => 0.7344f, 'T' => 0.7539f,
                'U' => 0.7930f, 'V' => 0.8047f, 'W' => 0.9258f, 'X' => 0.8164f,
                'Y' => 0.8359f, 'Z' => 0.7227f,
                'А' => 0.9492f, 'Б' => 0.8164f, 'В' => 0.8242f, 'Г' => 0.7148f,
                'Д' => 0.9375f, 'Е' => 0.7773f, 'Ё' => 0.6328f, 'Ж' => 1.0820f,
                'З' => 0.7773f, 'И' => 0.8555f, 'Й' => 0.7227f, 'К' => 0.8633f,
                'Л' => 0.9336f, 'М' => 0.9961f, 'Н' => 0.8711f, 'О' => 0.9375f,
                'П' => 0.8516f, 'Р' => 0.8242f, 'С' => 0.8984f, 'Т' => 0.8555f,
                'У' => 0.8828f, 'Ф' => 1.0352f, 'Х' => 0.9219f, 'Ц' => 0.9062f,
                'Ч' => 0.8945f, 'Ш' => 1.0898f, 'Щ' => 1.0195f, 'Ъ' => 0.9492f,
                'Ы' => 1.0273f, 'Ь' => 0.8203f, 'Э' => 0.8594f, 'Ю' => 1.1523f,
                'Я' => 0.8711f,
                _ => 0f
            };
        }

        private static bool IsSupported(char character)
        {
            return character >= 'A' && character <= 'Z' ||
                   character >= 'А' && character <= 'Я' ||
                   character == 'Ё';
        }
    }
}
