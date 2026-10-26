using System.Collections.Generic;
using FXDSP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class MarketCandleGraphic : MaskableGraphic
    {
        internal static readonly Color Rising = new Color(.83f, .35f, .33f, 1);
        internal static readonly Color Falling = new Color(.32f, .73f, .5f, 1);
        private readonly List<MarketCandle> _candles = new List<MarketCandle>();
        internal float Minimum { get; private set; }
        internal float Maximum { get; private set; }
        internal float LiquidationPrice { get; private set; }
        internal float LiquidationRatio
        { get { return Maximum <= Minimum ? 0 : (LiquidationPrice - Minimum) / (Maximum - Minimum); } }

        internal void SetCandles(IReadOnlyList<MarketCandle> candles, decimal liquidationPrice)
        {
            int count = candles == null ? 0 : Mathf.Min(60, candles.Count);
            int offset = candles == null ? 0 : candles.Count - count;
            bool changed = count != _candles.Count || LiquidationPrice != (float)liquidationPrice;
            if (!changed)
                for (int i = 0; i < count; i++)
                    if (candles[offset + i].StartTick != _candles[i].StartTick || candles[offset + i].Open != _candles[i].Open
                        || candles[offset + i].Close != _candles[i].Close || candles[offset + i].DisplayHigh != _candles[i].DisplayHigh
                        || candles[offset + i].DisplayLow != _candles[i].DisplayLow) { changed = true; break; }
            if (!changed) return;
            LiquidationPrice = (float)liquidationPrice;
            _candles.Clear();
            Minimum = float.MaxValue;
            Maximum = float.MinValue;
            if (candles != null)
                for (int i = 0; i < count; i++)
                {
                    var candle = candles[offset + i];
                    _candles.Add(candle);
                    Minimum = Mathf.Min(Minimum, (float)candle.DisplayLow);
                    Maximum = Mathf.Max(Maximum, (float)candle.DisplayHigh);
                }
            if (count == 0) { Minimum = 0; Maximum = 1; }
            float padding = Mathf.Max((Maximum - Minimum) * .08f, Mathf.Abs(Minimum) * .001f);
            Minimum -= padding;
            Maximum += padding;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var area = rectTransform.rect;
            var grid = new Color(.35f, .55f, .65f, .17f);
            for (int i = 0; i <= 3; i++)
                Box(mesh, area.xMin, area.yMin + 8 + (area.height - 16) * i / 3f, area.width, .8f, grid);
            for (int i = 0; i <= 6; i++)
                Box(mesh, area.xMin + area.width * i / 6f, area.yMin, .8f, area.height, grid);
            if (_candles.Count == 0 || Maximum <= Minimum) return;
            var plot = new Rect(area.xMin + 6, area.yMin + 8, area.width - 12, area.height - 16);
            // Keep a readable, stable body width as the first bars arrive.
            float slot = plot.width / Mathf.Max(24, _candles.Count);
            float body = Mathf.Max(3, slot * .62f);
            for (int i = 0; i < _candles.Count; i++)
            {
                var c = _candles[i];
                float x = plot.xMin + slot * (i + .5f);
                float open = Y(c.Open, plot), close = Y(c.Close, plot);
                float low = Y(c.DisplayLow, plot), high = Y(c.DisplayHigh, plot);
                Color tint = c.Close > c.Open ? Rising : c.Close < c.Open ? Falling : new Color(.63f, .69f, .73f, 1);
                Box(mesh, x - .7f, low, 1.4f, Mathf.Max(1, high - low), tint);
                Box(mesh, x - body / 2, Mathf.Min(open, close) - (Mathf.Abs(close - open) < 1.5f ? .75f : 0),
                    body, Mathf.Max(1.5f, Mathf.Abs(close - open)), tint);
            }
            if (LiquidationPrice > 0)
            {
                float y = plot.yMin + plot.height * Mathf.Clamp01(LiquidationRatio);
                var tint = new Color(.85f, .66f, .32f, 1);
                for (float x = plot.xMin; x < plot.xMax; x += 12)
                    Box(mesh, x, y, Mathf.Min(7, plot.xMax - x), 1.3f, tint);
            }
        }

        private float Y(decimal value, Rect plot)
        { return plot.yMin + plot.height * ((float)value - Minimum) / (Maximum - Minimum); }

        private static void Box(VertexHelper mesh, float x, float y, float width, float height, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
