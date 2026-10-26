using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FXDSP.Core
{
    // Presentation only: saved logs, protocol strings and prototype identities stay unchanged.
    public static class TextLocalization
    {
        private sealed class Node
        {
            internal readonly Dictionary<char, Node> Children = new Dictionary<char, Node>();
            internal string Value;
        }
        private static readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Node Root = new Node();
        private static bool _loaded, _english;
        public static bool English
        {
            get { return _english; }
            set { if (_english != value) { _english = value; Cache.Clear(); } }
        }
        public static string Translate(string source)
        {
            if (!_english || string.IsNullOrEmpty(source)) return source ?? "";
            Load();
            string result;
            if (Entries.TryGetValue(source, out result) || Cache.TryGetValue(source, out result)) return result;
            bool chinese = false;
            foreach (char c in source) if (c >= '\u4e00' && c <= '\u9fff') { chinese = true; break; }
            if (!chinese) return source;
            var output = new StringBuilder();
            for (int start = 0; start < source.Length;)
            {
                Node node = Root; string replacement = null; int end = start;
                for (int index = start; index < source.Length; index++)
                {
                    if (!node.Children.TryGetValue(source[index], out node)) break;
                    if (node.Value != null) { replacement = node.Value; end = index + 1; }
                }
                if (replacement == null) { output.Append(source[start]); start++; }
                else { output.Append(replacement); start = end; }
            }
            result = output.ToString();
            if (Cache.Count >= 512) Cache.Clear();
            Cache[source] = result;
            return result;
        }
        public static int EntryCount { get { Load(); return Entries.Count; } }
        public static void RegisterNativeName(string source, string translation)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation) || source == translation) return;
            Load();
            if (Entries.ContainsKey(source))
            {
                Entries[source] = translation;
                Node node = Root;
                foreach (char c in source) node = node.Children[c];
                node.Value = translation;
            }
            else Add(source, translation);
            Cache.Clear();
        }
        private static void Load()
        {
            if (_loaded) return;
            using (var stream = typeof(TextLocalization).Assembly.GetManifestResourceStream("FXDSP.Localization.en-US.tsv"))
            {
                if (stream == null) throw new InvalidOperationException("English localization resource is missing.");
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0 || line[0] == '#') continue;
                        int separator = line.IndexOf('\t');
                        if (separator < 1) throw new InvalidDataException("Invalid localization entry.");
                        Add(Decode(line.Substring(0, separator)), Decode(line.Substring(separator + 1)));
                    }
                }
            }
            _loaded = true;
        }
        private static void Add(string source, string target)
        {
            Entries.Add(source, target);
            Node node = Root;
            foreach (char c in source)
            {
                Node next;
                if (!node.Children.TryGetValue(c, out next)) node.Children[c] = next = new Node();
                node = next;
            }
            node.Value = target;
        }
        private static string Decode(string text)
        {
            var result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[++i];
                    if (next == 'n') result.Append('\n');
                    else if (next == 'r') result.Append('\r');
                    else if (next == 't') result.Append('\t');
                    else if (next == '\\') result.Append('\\');
                    else { result.Append('\\'); result.Append(next); }
                }
                else result.Append(c);
            }
            return result.ToString();
        }
    }
}
