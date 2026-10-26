using System.Collections.Generic;
using FXDSP.Core;
using UnityEngine.UI;

namespace FXDSP.UI
{
    // Restrict translation to text created by our UI factory, never other mods' labels.
    internal sealed class LocalizedText : Text
    {
        private static readonly HashSet<LocalizedText> ActiveTexts = new HashSet<LocalizedText>();
        private string _source = "";
        public override string text
        {
            get { return base.text; }
            set { _source = value ?? ""; baseText(); }
        }
        protected override void OnEnable() { base.OnEnable(); ActiveTexts.Add(this); baseText(); }
        protected override void OnDisable() { ActiveTexts.Remove(this); base.OnDisable(); }
        protected override void OnDestroy() { ActiveTexts.Remove(this); base.OnDestroy(); }
        internal static void RefreshLanguage()
        {
            foreach (var label in ActiveTexts) if (label != null) label.baseText();
        }
        private void baseText() { base.text = TextLocalization.Translate(_source); }
    }
}
