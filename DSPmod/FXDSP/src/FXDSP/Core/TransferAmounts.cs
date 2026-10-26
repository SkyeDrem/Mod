using System;
using System.Numerics;

namespace FXDSP.Core
{
    public static class TransferAmounts
    {
        public const int SliderSteps = 1000;
        public static int Maximum(BigInteger available) { return (int)BigInteger.Min(int.MaxValue, BigInteger.Max(0, available)); }
        public static int FromSlider(int step, int maximum)
        { return maximum <= 0 ? 0 : (int)((long)maximum * Math.Max(0, Math.Min(SliderSteps, step)) / SliderSteps); }
        public static int ToSlider(int quantity, int maximum)
        { return maximum <= 0 ? 0 : (int)Math.Min(SliderSteps, (long)Math.Max(0, quantity) * SliderSteps / maximum); }
    }
}
