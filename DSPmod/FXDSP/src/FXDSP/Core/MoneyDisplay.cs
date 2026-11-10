using System;
using System.Globalization;

namespace FXDSP.Core
{
    public static class MoneyDisplay
    {
        public static string Signed(decimal value)
        { return decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("+#,0.00;-#,0.00;0.00", CultureInfo.InvariantCulture); }
        public static string Percent(decimal? ratio)
        { return ratio.HasValue ? ratio.Value.ToString("+#,0.00%;-#,0.00%;0.00%", CultureInfo.InvariantCulture) : "—"; }
        public static string Leverage(decimal ratio)
        { return ratio < .01m ? "＜0.01×" : ratio.ToString("0.##", CultureInfo.InvariantCulture) + "×"; }
    }
}
