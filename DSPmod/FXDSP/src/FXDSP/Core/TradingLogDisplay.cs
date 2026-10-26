using System.Globalization;
using System.Text.RegularExpressions;

namespace FXDSP.Core
{
    public static class TradingLogDisplay
    {
        private static readonly Regex Opening = new Regex(@"^(开多|开空)[：:\s]*([0-9,]+(?:\.[0-9]+)?)\s*.*?保证金");
        private static readonly Regex DecimalNumber = new Regex(@"(?<![\d.,])[+-]?\d+(?:,\d{3})*\.\d+(?![\d.])");
        public static string Format(string message)
        {
            message = message ?? "";
            var opening = Opening.Match(message);
            decimal amount;
            if (opening.Success && decimal.TryParse(opening.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
                return TextLocalization.English
                    ? TextLocalization.Translate(opening.Groups[1].Value).Trim() + " " + amount.ToString("#,0.00", CultureInfo.InvariantCulture) + " " + TextLocalization.Translate("保证金").ToLowerInvariant()
                    : opening.Groups[1].Value + amount.ToString("#,0.00", CultureInfo.InvariantCulture) + "保证金";
            return TextLocalization.Translate(DecimalNumber.Replace(message, match => {
                decimal value;
                if (!decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return match.Value;
                return (match.Value.StartsWith("+") ? "+" : "") + value.ToString("#,0.00", CultureInfo.InvariantCulture);
            }));
        }
    }
}
