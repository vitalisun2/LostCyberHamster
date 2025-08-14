using UnityEngine.UIElements;

namespace Vues.GameCore
{
    [UxmlElement]
    public partial class LocalizedToggle : Toggle
    {
        public static BindingId keyProperty = nameof(key);

        [UxmlAttribute]
        public string key;
        public LocalizedToggle()
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
