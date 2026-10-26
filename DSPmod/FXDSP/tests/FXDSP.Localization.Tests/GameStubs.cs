using System.Collections.Generic;
namespace UnityEngine { internal sealed class Sprite { } }

internal class Proto { public int ID; public string Name, name; }
internal sealed class TechProto : Proto { public string description, conclusion; }
internal sealed class ItemProto : Proto { public string description; }
internal sealed class RecipeProto : Proto { public string description; }
internal sealed class ProtoSet<T> where T : Proto
{
    public readonly Dictionary<int, T> Entries = new Dictionary<int, T>();
    public T Select(int id) { T proto; return Entries.TryGetValue(id, out proto) ? proto : null; }
}
internal static class LDB
{
    public static ProtoSet<TechProto> techs = new ProtoSet<TechProto>();
    public static ProtoSet<ItemProto> items = new ProtoSet<ItemProto>();
    public static ProtoSet<RecipeProto> recipes = new ProtoSet<RecipeProto>();
}
internal static class Localization { public static bool Loaded = true, isZHCN; }
internal static class Configs { public static Builtin builtin = new Builtin(); }
internal sealed class Builtin { public Mapping generalIconMapping = new Mapping(); }
internal sealed class Mapping { public Icon[] iconMappings; }
internal sealed class Icon { public string tag, name; public int UvMarker; }
