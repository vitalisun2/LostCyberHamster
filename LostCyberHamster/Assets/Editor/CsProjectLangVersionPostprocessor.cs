using System.Text.RegularExpressions;
using UnityEditor;

namespace Assets.EditorTools
{
    /// <summary>
    /// Закрепляет версию C# в генерируемых проектах для IDE.
    /// </summary>
    public sealed class CsProjectLangVersionPostprocessor : AssetPostprocessor
    {
        private const string LangVersionElement = "<LangVersion>9.0</LangVersion>";

        public static string OnGeneratedCSProject(string path, string content)
        {
            const string pattern = @"<LangVersion>.*?</LangVersion>";
            return Regex.Replace(content, pattern, LangVersionElement);
        }
    }
}
