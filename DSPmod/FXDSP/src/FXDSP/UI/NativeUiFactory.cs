using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FXDSP.UI
{
    // Copies only visual resources. No original window/controller is instantiated.
    internal sealed class NativeUiFactory
    {
        internal readonly Transform Parent;
        private readonly Font _font;
        private readonly Image _panelStyle;
        private readonly Image _shadowStyle;
        private readonly Image _buttonStyle;

        internal NativeUiFactory()
        {
            if (UIRoot.instance == null || UIRoot.instance.uiGame == null)
                throw new InvalidOperationException("请先进入游戏，再打开 FXDSP。");
            var game = UIRoot.instance.uiGame;
            if (game.inserterWindow == null || game.assemblerWindow == null || game.assemblerWindow.stateText == null)
                throw new InvalidOperationException("原版窗口模板尚未就绪。");

            Parent = game.inserterWindow.transform.parent;
            var sourceText = game.assemblerWindow.stateText;
            _font = sourceText.font;
            if (_font == null) throw new InvalidOperationException("无法取得原版字体。");

            _panelStyle = FindImage(game.inserterWindow.transform, "panel-bg");
            _shadowStyle = FindImage(game.inserterWindow.transform, "shadow");
            if (game.statWindow != null && game.statWindow.performancePanelUI != null
                && game.statWindow.performancePanelUI.cpuActiveButton != null)
                _buttonStyle = game.statWindow.performancePanelUI.cpuActiveButton.GetComponent<Image>();
        }

        private static Image FindImage(Transform parent, string path)
        {
            var child = parent.Find(path);
            return child == null ? null : child.GetComponent<Image>();
        }

        internal RectTransform Root(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(Parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
            return rect;
        }

        internal Image WindowBackground(Transform parent, bool shadow, float x, float y, float width, float height)
        {
            if (!shadow)
            {
                var fill = Image("solid-background", parent, x, y, width, height, new Color(0.025f, 0.04f, 0.055f, 0.98f));
                fill.raycastTarget = true;
            }
            var image = Image(shadow ? "shadow" : "panel-bg", parent, x, y, width, height,
                shadow ? new Color(0, 0, 0, 0.45f) : new Color(0.055f, 0.075f, 0.09f, 0.98f));
            CopyImageStyle(shadow ? _shadowStyle : _panelStyle, image);
            image.raycastTarget = !shadow;
            return image;
        }

        internal Image Image(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Place((RectTransform)go.transform, parent, x, y, width, height);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal RectTransform Container(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place((RectTransform)go.transform, parent, x, y, width, height);
            return (RectTransform)go.transform;
        }

        internal Text Text(string name, Transform parent, float x, float y, float width, float height,
            string value, int size = 14, Color? color = null, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LocalizedText));
            Place((RectTransform)go.transform, parent, x, y, width, height);
            var text = go.GetComponent<Text>();
            text.font = _font;
            // The assembler label uses a game-specific emissive font material.
            // Reusing it overexposes independent labels and even the close glyph.
            // Null uses UGUI's regular alpha-blended font rendering instead.
            text.material = null;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            var tint = color ?? new Color(0.78f, 0.84f, 0.89f, 1f);
            text.color = new Color(Mathf.Min(0.85f, tint.r), Mathf.Min(0.85f, tint.g), Mathf.Min(0.85f, tint.b), tint.a);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.text = value;
            return text;
        }

        internal static string DirectionText(string direction)
        {
            if (direction != "做多" && direction != "做空") return direction;
            var color = direction == "做多" ? MarketCandleGraphic.Rising : MarketCandleGraphic.Falling;
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + direction + "</color>";
        }

        internal Button Button(string name, Transform parent, float x, float y, float width, float height,
            string label, UnityAction action, Color? accent = null)
        {
            var background = Image(name, parent, x, y, width, height, new Color(0.14f, 0.22f, 0.27f, 1f));
            CopyImageStyle(_buttonStyle, background);
            background.color = accent ?? new Color(0.32f, 0.48f, 0.55f, 0.9f);
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.83f, 0.87f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            var caption = Text("label", background.transform, 5, 1, width - 10, height - 2, label, 14,
                null, TextAnchor.MiddleCenter);
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 10; caption.resizeTextMaxSize = 14;
            return button;
        }

        internal InputField QuantityInput(Transform parent, float x, float y, float width, float height)
        {
            var background = Image("quantity-input", parent, x, y, width, height, new Color(0.015f, 0.03f, 0.04f, 0.95f));
            background.raycastTarget = true;
            var text = Text("text", background.transform, 9, 1, width - 18, height - 2, "", 18,
                null, TextAnchor.MiddleLeft);
            var field = background.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 10;
            field.caretColor = new Color(0.5f, 0.9f, 1f, 1f);
            field.customCaretColor = true;
            field.selectionColor = new Color(0.25f, 0.6f, 0.7f, 0.5f);
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            return field;
        }

        internal Slider Slider(string name, Transform parent, float x, float y, float width, float height,
            int minimum, int maximum, int value, UnityAction<float> changed)
        {
            var track = Image(name, parent, x, y, width, height, new Color(0, 0, 0, 0));
            track.raycastTarget = true;
            Image("track", track.transform, 7, height / 2 - 2, width - 14, 4, new Color(.17f, .27f, .32f, 1));
            var area = Image("sliding-area", track.transform, 7, 0, width - 14, height, Color.clear);
            var fill = Image("fill", area.transform, 0, 0, 0, 4 - height, new Color(.31f, .61f, .7f, 1));
            var handle = Image("handle", area.transform, 0, 0, 14, -6, new Color(.65f, .78f, .82f, 1));
            ((RectTransform)fill.transform).pivot = new Vector2(.5f, .5f);
            ((RectTransform)handle.transform).pivot = new Vector2(.5f, .5f);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = true;
            slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        internal static void Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void CopyImageStyle(Image source, Image target)
        {
            if (source == null) return;
            target.sprite = source.sprite;
            target.type = source.type;
            target.material = null;
            target.color = source.color;
            target.fillCenter = source.fillCenter;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }
    }
}
