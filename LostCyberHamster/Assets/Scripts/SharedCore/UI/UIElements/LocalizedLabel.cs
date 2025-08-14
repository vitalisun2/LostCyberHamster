using UnityEngine.UIElements;

namespace Vues.GameCore
{
    [UxmlElement]
    public partial class LocalizedLabel : Label
    {
        public static BindingId keyProperty = nameof(key);

        [UxmlAttribute]
        public string key;
        public LocalizedLabel()
        {
            schedule.Execute(() =>
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                text = LocalizationManager.GetLocalizedString(key);
            });
        }
    }
}