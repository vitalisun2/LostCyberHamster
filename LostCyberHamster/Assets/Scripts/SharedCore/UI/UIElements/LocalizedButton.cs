using UnityEngine.UIElements;

namespace Vues.GameCore
{
    [UxmlElement]
    public partial class LocalizedButton : Button
    {
        public static BindingId keyProperty = nameof(key);

        [UxmlAttribute]
        public string key;

        [UxmlAttribute]
        public bool uppercase;
        public LocalizedButton()
        {
            schedule.Execute(() =>
            {
                if (string.IsNullOrEmpty(key))
                {
                    return;
                }
                text = LocalizationManager.GetLocalizedString(key);
                if (uppercase)
                    text = text.ToUpperInvariant();
            });
        }
    }
}
