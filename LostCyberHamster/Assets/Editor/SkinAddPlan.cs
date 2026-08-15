#if UNITY_EDITOR
using System.Collections.Generic;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Хранит полностью проверенный план одной Add Skin операции.
    /// </summary>
    internal sealed class SkinAddPlan
    {
        public SkinAddPlan(
            SkinAddRequest request,
            string slug,
            int id,
            SkinDataList catalog,
            SkinData defaultSkin,
            IReadOnlyList<string> normalTemplateSheets,
            IReadOnlyList<string> skateboardTemplateSheets,
            IReadOnlyList<string> localizationPaths)
        {
            Request = request;
            Slug = slug;
            Id = id;
            Catalog = catalog;
            DefaultSkin = defaultSkin;
            NormalTemplateSheets = normalTemplateSheets;
            SkateboardTemplateSheets = skateboardTemplateSheets;
            LocalizationPaths = localizationPaths;
        }

        public SkinAddRequest Request { get; }
        public string Slug { get; }
        public int Id { get; }
        public SkinDataList Catalog { get; }
        public SkinData DefaultSkin { get; }
        public IReadOnlyList<string> NormalTemplateSheets { get; }
        public IReadOnlyList<string> SkateboardTemplateSheets { get; }
        public IReadOnlyList<string> LocalizationPaths { get; }
    }
}
#endif
