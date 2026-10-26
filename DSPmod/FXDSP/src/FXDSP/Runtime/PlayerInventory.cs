using System;
using System.Numerics;
using FXDSP.Core;

namespace FXDSP.Runtime
{
    internal static class PlayerInventory
    {
        public static int Count(int itemId)
        {
            var player = GameMain.mainPlayer;
            if (itemId == 1099) return player == null ? 0 : (int)Math.Min(int.MaxValue, Math.Max(0, player.sandCount));
            return player == null || player.package == null ? 0 : player.package.GetItemCount(itemId);
        }
        public static BigInteger Available(int itemId)
        { return itemId == 1099 ? new BigInteger(GameMain.mainPlayer == null ? 0 : Math.Max(0, GameMain.mainPlayer.sandCount)) : Count(itemId); }

        public static string Deposit(MarginMarket market, int requested)
        {
            if (market.State.ItemId == MarketCatalog.BuildingCoin) throw new InvalidOperationException("请在建筑兑换中获取建筑币。");
            if (market.State.ItemId == 1099) return TransferSand(market, requested, true);
            ValidateQuantity(requested);
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null)
                throw new InvalidOperationException("背包尚未准备好。");

            int inc;
            var available = player.package.GetItemCount(market.State.ItemId, out inc);
            if (available < requested)
                throw new InvalidOperationException("背包物品不足。");
            if (inc > 0)
                throw new InvalidOperationException("请先移出背包中已喷涂的同类物品。");

            // All predictable ledger failures are checked before changing game items.
            checked
            {
                var quantity = market.State.Balance + requested;
                var deposits = market.State.TotalDeposited + requested;
                if (quantity < 0 || deposits < 0) throw new OverflowException();
            }

            int removedInc;
            var removed = player.package.TakeItem(market.State.ItemId, requested, out removedInc);
            if (removed <= 0)
                throw new InvalidOperationException("背包未移出物品，入库取消。");
            market.Deposit(removed);
            return "已存入 " + removed + " " + market.Unit + "。";
        }

        public static string Withdraw(MarginMarket market, int requested)
        {
            if (market.State.ItemId == MarketCatalog.BuildingCoin) throw new InvalidOperationException("请在建筑兑换中使用建筑币。");
            if (market.State.ItemId == 1099) return TransferSand(market, requested, false);
            ValidateQuantity(requested);
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null)
                throw new InvalidOperationException("背包尚未准备好。");
            if (market.State.Balance < requested)
                throw new InvalidOperationException("可用余额不足。");
            checked
            {
                var withdrawals = market.State.TotalWithdrawn + requested;
                if (withdrawals < 0) throw new OverflowException();
            }

            // AddItemStacked returns the quantity actually accepted; excess stays
            // in custody instead of being dropped, lost, or charged a second time.
            int remainInc;
            var inserted = player.package.AddItemStacked(market.State.ItemId, requested, 0, out remainInc);
            if (inserted <= 0)
                throw new InvalidOperationException("背包已满。");
            market.Withdraw(inserted);
            player.NotifyPackageAddItem(market.State.ItemId, inserted, 0);
            return inserted == requested
                ? "已提取 " + inserted + " " + market.Unit + "。"
                : "背包容量不足，已提取 " + inserted + " " + market.Unit + "。";
        }

        public static string BuyBuilding(MarginMarket wallet, int itemId, int quantity, long unitValue, decimal feeRate = BuildingExchangeRules.FeeRate)
        {
            RequireCurrency(wallet);
            long cost = BuildingExchangeRules.Cost(unitValue, quantity);
            wallet.ValidateExchange(cost, BuildingExchangeRules.Fee(cost, feeRate), true);
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null) throw new InvalidOperationException("背包尚未准备好。");
            int remainder;
            int received = player.package.AddItemStacked(itemId, quantity, 0, out remainder);
            if (received <= 0) throw new InvalidOperationException("背包已满。");
            long paid = BuildingExchangeRules.Cost(unitValue, received);
            decimal fee = BuildingExchangeRules.Fee(paid, feeRate);
            wallet.SettleExchange(paid, fee, true);
            wallet.NotifyBuildingPurchase(itemId, received);
            player.NotifyPackageAddItem(itemId, received, 0);
            return "已兑换 " + received + " 件物品，使用 " + (paid + fee).ToString("0.00") + " 建筑币。";
        }

        public static string SellBuilding(MarginMarket wallet, int itemId, int quantity, long unitValue, decimal feeRate = BuildingExchangeRules.FeeRate)
        {
            RequireCurrency(wallet);
            long proceeds = BuildingExchangeRules.Cost(unitValue, quantity);
            wallet.ValidateExchange(proceeds, BuildingExchangeRules.Fee(proceeds, feeRate), false);
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null) throw new InvalidOperationException("背包尚未准备好。");
            int inc;
            if (player.package.GetItemCount(itemId, out inc) < quantity) throw new InvalidOperationException("背包物品不足。");
            if (inc > 0) throw new InvalidOperationException("请先移出背包中已喷涂的同类物品。");
            int removed = player.package.TakeItem(itemId, quantity, out inc);
            if (removed <= 0) throw new InvalidOperationException("背包未移出物品。");
            long credited = BuildingExchangeRules.Cost(unitValue, removed);
            decimal fee = BuildingExchangeRules.Fee(credited, feeRate);
            wallet.SettleExchange(credited, fee, false);
            return "已换得 " + (credited - fee).ToString("0.00") + " 建筑币。";
        }

        private static string TransferSand(MarginMarket market, int requested, bool deposit)
        {
            ValidateQuantity(requested);
            var player = GameMain.mainPlayer;
            if (player == null) throw new InvalidOperationException("沙土尚未准备好。");
            long before = player.sandCount;
            if (before < 0) throw new InvalidOperationException("沙土数量无效。");
            if (deposit && before < requested) throw new InvalidOperationException("沙土不足。");
            if (!deposit && market.State.Balance < requested) throw new InvalidOperationException("可用余额不足。");
            int moved = deposit ? requested : (int)Math.Min(requested, long.MaxValue - before);
            if (moved <= 0) throw new InvalidOperationException("沙土数量已达上限。");
            checked
            {
                // Reject predictable ledger overflow before changing native sand.
                decimal balance = deposit ? market.State.Balance + moved : market.State.Balance - moved;
                long total = deposit ? market.State.TotalDeposited + moved : market.State.TotalWithdrawn + moved;
                if (balance < 0 || total < 0) throw new OverflowException();
            }
            player.SetSandCount(deposit ? before - moved : before + moved, ESandSource.Other);
            if (deposit) market.Deposit(moved); else market.Withdraw(moved);
            return "已" + (deposit ? "存入 " : "提取 ") + moved.ToString("N0") + " 沙土。";
        }

        private static void RequireCurrency(MarginMarket wallet)
        {
            if (wallet == null || wallet.State.ItemId != MarketCatalog.BuildingCoin)
                throw new InvalidOperationException("兑换须使用建筑币账户。");
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity <= 0) throw new InvalidOperationException("数量必须是正整数。");
        }
    }
}
