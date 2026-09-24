using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EXFIL.UI
{
    /// <summary>Helpers that build uGUI widgets at runtime (no prefabs required).</summary>
    public static class UIFactory
    {
        public static Canvas Canvas(string name)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        public static RectTransform Window(Transform parent, string title, float width = 900f, float height = 640f)
        {
            GameObject go = new GameObject(title, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Image background = go.GetComponent<Image>();
            background.color = new Color(0.08f, 0.09f, 0.10f, 0.96f);

            Text titleText = Text(rect, title, 26, TextAnchor.UpperLeft, Color.white);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(20f, -46f);
            titleText.rectTransform.offsetMax = new Vector2(-20f, -8f);
            titleText.fontStyle = FontStyle.Bold;

            return rect;
        }

        public static Text Text(Transform parent, string content, int fontSize = 16,
            TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color ?? new Color(0.92f, 0.92f, 0.90f);
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Button(Transform parent, string label, System.Action onClick, Color? color = null)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color ?? new Color(0.20f, 0.24f, 0.28f, 1f);

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text text = Text(go.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        public static RectTransform VerticalList(Transform parent, float spacing = 6f, float padding = 12f)
        {
            GameObject go = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.GetComponent<RectTransform>();
        }

        public static ScrollRect ScrollArea(Transform parent, out RectTransform content)
        {
            GameObject go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.15f, 0.9f);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Mask mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform list = VerticalList(viewport.transform);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;

            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;

            content = list;
            return go.GetComponent<ScrollRect>();
        }

        public static RectTransform Row(Transform parent, float height, Color? color = null)
        {
            GameObject go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color ?? new Color(0.16f, 0.17f, 0.19f, 0.95f);

            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(8, 8, 4, 4);

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return go.GetComponent<RectTransform>();
        }

        public static void FitText(Text text, float width, float height)
        {
            text.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}
