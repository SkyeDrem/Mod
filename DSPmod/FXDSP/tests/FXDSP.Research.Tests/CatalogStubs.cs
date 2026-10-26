using System.Linq;

// Native item facts are loaded from the locally extracted prototype table by Program.
public enum EItemType { Resource = 1, Material = 2, Component = 3, Product = 4 }
public sealed class ItemProto
{
    public const int kWarperId = 1210, kRocketId = 1503;
    public int ID, StackSize, CatalystType, FuelType;
    public string name;
    public object iconSprite;
    public bool CanBuild, isAmmo, isBomb;
    public EItemType Type;
}
public sealed class ItemSet
{
    public ItemProto[] dataArray;
    public ItemProto Select(int id) { return dataArray.FirstOrDefault(item => item.ID == id); }
}
public static class LDB { public static ItemSet items; }
namespace FXDSP.Runtime
{
    internal sealed class MarketOption { public int ItemId; public string Name; public object Icon; }
}
