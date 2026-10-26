using System;
using System.Collections.Generic;

namespace FXDSP.Core
{
    public sealed class DeliveryRecipeDefinition
    {
        public readonly int ItemId, BaseItemId;
        public readonly string Name, Description;
        public int InputCount { get { return ItemId == 11702 ? 4 : 5; } }
        public int TimeSpend { get { return 3600; } }
        public bool Handcraft { get { return false; } }
        public DeliveryRecipeDefinition(int itemId, int baseItemId, string name, string description)
        { ItemId = itemId; BaseItemId = baseItemId; Name = name; Description = description; }
    }
    public static class DeliveryRecipes
    {
        public static readonly IReadOnlyList<DeliveryRecipeDefinition> Definitions = Array.AsReadOnly(new[] {
            new DeliveryRecipeDefinition(11702, 2103, "行星级交付平台",
                "在行星物流塔基础上改造而成的现货交付平台，具备原行星物流塔的全部功能，并拥有深不可测的储存容量以应对大单交割。平台槽位设置物品后即唯一绑定交易系统，可在交易面板中从平台中存入或提取物资，该物品不能再被第二个交付平台选中。"),
            new DeliveryRecipeDefinition(11703, 2104, "星际交付平台",
                "在星际物流塔基础上改造而成的现货交付平台，具备原星际物流塔的全部功能，并拥有深不可测的储存容量以应对大单交割。平台槽位设置物品后即唯一绑定交易系统，可在交易面板中从平台存入或提取物资，该物品不能再被第二个交付平台选中。")
        });
        public static int AllocateGrid(ISet<int> occupied)
        {
            // Current native building page: 8 rows, 14 columns. Keep the two entries together at the bottom where possible.
            for (int row = 8; row >= 1; row--)
                for (int column = 1; column <= 14; column++)
                {
                    int grid = 2000 + row * 100 + column;
                    if (occupied.Add(grid)) return grid;
                }
            throw new InvalidOperationException("制作面板的建筑分页已满，无法放置交付平台配方。");
        }
    }
}
