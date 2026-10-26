using System;

// The real DeliveryService and MarketSession are linked unchanged; only the native game boundary is replaced.
namespace UnityEngine { internal struct Vector3 { public float x, y, z; } }
internal static class GameMain
{
    internal static bool isRunning = true, isPaused;
    internal static long gameTick = 100;
    internal static GameInstance instance = new GameInstance();
    internal static GameData data;
    internal static Player mainPlayer = new Player();
    internal static GameHistoryData history = new GameHistoryData();
}
internal sealed class GameInstance { internal bool isMenuDemo = false; }
internal sealed class GameDesc { internal DateTime creationTime = new DateTime(2026, 10, 26); internal int galaxySeed = 42; }
internal sealed class GameData { internal GameDesc gameDesc = new GameDesc(); internal PlanetFactory[] factories; internal GalaxyData galaxy; }
internal sealed class GalaxyData { internal PlanetData planet; internal PlanetData PlanetById(int id) { return planet != null && planet.id == id ? planet : null; } }
internal sealed class PlanetData { internal int id; internal string displayName = "Test Planet"; internal PlanetFactory factory; }
internal sealed class GameHistoryData { internal bool ItemUnlocked(int id) { return true; } internal bool RecipeUnlocked(int id) { return true; } internal void UnlockRecipe(int id) { } internal bool TechUnlocked(int id) { return true; } }
internal sealed class Player { }
internal struct EntityData { internal int id, protoId, stationId; internal UnityEngine.Vector3 pos; }
internal sealed class PlanetFactory
{
    internal int planetId = 101, entityCursor = 4;
    internal EntityData[] entityPool = new EntityData[4];
    internal PlanetTransport transport = new PlanetTransport();
}
internal sealed class PlanetTransport
{
    internal StationComponent[] stationPool = new StationComponent[4];
    internal void SetStationStorage(int stationId, int index, int itemId, int maximum, ELogisticStorage local, ELogisticStorage remote, Player player)
    {
        var station = stationPool[stationId];
        if (station.storage[index].itemId != itemId) station.storage[index].count = station.storage[index].inc = 0;
        station.storage[index].itemId = itemId; station.storage[index].max = maximum;
        station.storage[index].localLogic = local; station.storage[index].remoteLogic = remote;
    }
}
internal enum ELogisticStorage { None, Supply, Demand }
internal struct StationStore { internal int itemId, count, inc, max; internal ELogisticStorage localLogic, remoteLogic; }
internal sealed class StationComponent
{
    internal int id, entityId, planetId;
    internal StationStore[] storage;
    internal void TakeItem(ref int itemId, ref int count, out int inc)
    {
        inc = 0; int request = count; count = 0;
        for (int i = 0; i < storage.Length; i++) if (storage[i].itemId == itemId)
        { count = Math.Min(request, storage[i].count); inc = storage[i].count == 0 ? 0 : (int)((long)storage[i].inc * count / storage[i].count); storage[i].count -= count; storage[i].inc -= inc; return; }
    }
}
internal static class LDB { internal static ItemSet items = new ItemSet(); internal static RecipeSet recipes = new RecipeSet(); internal static TechSet techs = new TechSet(); }
internal sealed class ItemSet
{
    internal System.Collections.Generic.Dictionary<int, ItemProto> Entries = new System.Collections.Generic.Dictionary<int, ItemProto>();
    internal ItemProto Select(int id) { if (!Entries.ContainsKey(id)) Entries[id] = new ItemProto { name = "物品" + id }; return Entries[id]; }
}
internal sealed class RecipeSet
{
    internal System.Collections.Generic.Dictionary<int, object> Entries = new System.Collections.Generic.Dictionary<int, object>();
    internal object Select(int id) { if (!Entries.ContainsKey(id)) Entries[id] = new object(); return Entries[id]; }
}
internal sealed class TechSet
{
    internal System.Collections.Generic.Dictionary<int, TechProto> Entries = new System.Collections.Generic.Dictionary<int, TechProto>();
    internal TechProto Select(int id) { if (!Entries.ContainsKey(id)) Entries[id] = new TechProto { ID = id }; return Entries[id]; }
}
internal sealed class TechProto { internal int ID; internal string UnlockFunctionText() { return "Foreign mod text"; } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] internal sealed class HarmonyPatch : Attribute
    { internal HarmonyPatch(Type type, string method) { } }
}
internal sealed class ItemProto { internal string name; }
internal static class UIMessageBox
{
    internal static string LastTitle, LastMessage;
    internal static void Show(string title, string message, string button, int style) { LastTitle = title; LastMessage = message; }
}
