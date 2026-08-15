#if UNITY_EDITOR
namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Хранит введённые пользователем параметры нового скина.
    /// </summary>
    internal sealed class SkinAddRequest
    {
        public SkinAddRequest(
            string skinName,
            int price,
            string normalSourceFolder,
            string skateboardSourceFolder)
        {
            SkinName = skinName;
            Price = price;
            NormalSourceFolder = normalSourceFolder;
            SkateboardSourceFolder = skateboardSourceFolder;
        }

        public string SkinName { get; }
        public int Price { get; }
        public string NormalSourceFolder { get; }
        public string SkateboardSourceFolder { get; }
    }
}
#endif
