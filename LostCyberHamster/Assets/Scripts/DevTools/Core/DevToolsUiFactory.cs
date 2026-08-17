#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Создаёт общие uGUI primitives и layout-контейнеры для runtime DEV-экранов.
    /// </summary>
    internal sealed class DevToolsUiFactory
    {
        private readonly Font _font;

        public DevToolsUiFactory(Font font)
        {
            _font = font;
        }

        public GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject uiObject = new GameObject(name, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        public GameObject CreateStaticPage(string name, Transform parent, out Transform content)
        {
            GameObject page = CreateUiObject(name, parent);
            SetStretch(page.GetComponent<RectTransform>());

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(page.transform, false);
            SetStretch(contentObject.GetComponent<RectTransform>());
            ConfigureVerticalLayout(contentObject.GetComponent<VerticalLayoutGroup>());
            content = contentObject.transform;
            return page;
        }

        public GameObject CreateScrollPage(string name, Transform parent, out Transform content)
        {
            GameObject page = CreateUiObject(name, parent);
            SetStretch(page.GetComponent<RectTransform>());

            GameObject viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            viewport.transform.SetParent(page.transform, false);
            SetStretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            ConfigureVerticalLayout(contentObject.GetComponent<VerticalLayoutGroup>());

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = page.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 22f;
            content = contentObject.transform;
            return page;
        }

        public Transform CreateCard(string name, Transform parent, Color color)
        {
            GameObject card = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = color;

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout);
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 5f;

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card.transform;
        }

        public Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color,
            UnityAction action,
            float height = DevToolsTheme.ButtonHeight)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.62f);
            button.colors = colors;

            Text text = CreateText("Text", buttonObject.transform, label, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.fontSize = DevToolsTheme.ButtonFontSize;
            SetStretch(text.GetComponent<RectTransform>());
            return button;
        }

        public InputField CreateInputField(
            string name,
            Transform parent,
            string initialValue)
        {
            GameObject fieldObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(InputField),
                typeof(LayoutElement));
            fieldObject.transform.SetParent(parent, false);
            fieldObject.GetComponent<Image>().color = Color.white;
            LayoutElement layout = fieldObject.GetComponent<LayoutElement>();
            layout.preferredHeight = DevToolsTheme.ButtonHeight;
            layout.flexibleWidth = 1f;

            Text placeholder = CreateText(
                "Placeholder",
                fieldObject.transform,
                "Amount",
                TextAnchor.MiddleLeft);
            placeholder.color = new Color(0f, 0f, 0f, 0.45f);
            Text value = CreateText(
                "Text",
                fieldObject.transform,
                initialValue,
                TextAnchor.MiddleLeft);
            ConfigureInputTextRect(placeholder.GetComponent<RectTransform>());
            ConfigureInputTextRect(value.GetComponent<RectTransform>());

            InputField input = fieldObject.GetComponent<InputField>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = initialValue;
            return input;
        }

        public Text CreateBodyText(string name, Transform parent, string text, FontStyle style = FontStyle.Normal)
        {
            Text body = CreateText(name, parent, text, TextAnchor.UpperLeft, style);
            ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            return body;
        }

        public Text CreateSectionHeading(string name, Transform parent, string text)
        {
            Text heading = CreateBodyText(name, parent, text, FontStyle.Bold);
            heading.fontSize = DevToolsTheme.HeadingFontSize;
            return heading;
        }

        public Text CreateText(
            string name,
            Transform parent,
            string text,
            TextAnchor anchor,
            FontStyle style = FontStyle.Normal)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.GetComponent<Text>();
            uiText.text = text;
            uiText.font = _font;
            uiText.fontSize = DevToolsTheme.BodyFontSize;
            uiText.fontStyle = style;
            uiText.alignment = anchor;
            uiText.color = Color.black;
            uiText.raycastTarget = false;
            return uiText;
        }

        public static void ConfigureVerticalLayout(VerticalLayoutGroup layout)
        {
            layout.padding = new RectOffset(2, 4, 2, 8);
            layout.spacing = DevToolsTheme.ContentSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        public static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(Mathf.Max(width, 1f), Mathf.Max(height, 1f));
        }

        private static void ConfigureInputTextRect(RectTransform rect)
        {
            SetStretch(rect);
            rect.offsetMin = new Vector2(10f, 4f);
            rect.offsetMax = new Vector2(-10f, -4f);
        }
    }
}
#endif
