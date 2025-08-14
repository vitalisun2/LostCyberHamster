using UnityEngine.UIElements;

namespace Vues.GameCore
{
    [UxmlElement]
    public partial class LocalizedDropdown : DropdownField
    {
        public static BindingId keyProperty = nameof(key);

        [UxmlAttribute]
        public string key;
        public LocalizedDropdown()
        {
            schedule.Execute(() =>
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                label = LocalizationManager.GetLocalizedString(key);
            });
        }
    }
}
