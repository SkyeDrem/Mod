using System;
using System.Collections.Generic;
using UnityEngine;
using FXDSP.Core;

namespace FXDSP.Runtime
{
    internal enum MarketCommand { Deposit, Withdraw, OpenLong, OpenShort, ClosePosition, ChangeLeverage, SelectMarket,
        SelectExchangeItem, BuyBuilding, SellBuilding, CloseMarketPosition, CloseAllPositions,
        EnableResource, DisableResource, CloseHudPosition, SelectExchangeTier, UseBackpack, UseDelivery }

    internal sealed class PositionSnapshot
    {
        public int ItemId;
        public string Name, Direction;
        public Sprite Icon;
        public int Leverage;
        public decimal Margin, EntryPrice, CurrentPrice, LiquidationPrice, UnrealizedPnL;
        public decimal? UnrealizedReturnRatio { get { return AccountPerformance.UnrealizedReturnRatio(UnrealizedPnL, Margin); } }
        public long OpenTick, CurrentTick;
        public long HeldCandles { get { return AccountPerformance.HeldCandles(OpenTick, CurrentTick); } }
    }

    internal sealed class PortfolioSnapshot
    {
        public bool Ready;
        public IReadOnlyList<PositionSnapshot> Positions = Array.Empty<PositionSnapshot>();
    }

    internal sealed class MarketOption
    {
        public int ItemId;
        public string Name;
        public Sprite Icon;
        public bool Enabled;
        public bool HasPosition;
    }

    internal sealed class ExchangeOffer
    {
        public int ItemId;
        public int Tier;
        public string Name;
        public Sprite Icon;
        public long UnitValue;
    }

    internal sealed class MarketSnapshot
    {
        public bool Ready;
        public string Status = "";
        public int ItemId = 1002;
        public string ItemName = "铜矿";
        public Sprite ItemIcon;
        public int BackpackQuantity;
        public System.Numerics.BigInteger TransferAvailable;
        public bool UseDelivery, DeliveryBound, CanUseDelivery = true;
        public string DeliveryName;
        public decimal AvailableBalance;
        public decimal Equity;
        public decimal UsedMargin;
        public decimal UnrealizedPnL;
        public decimal RealizedPnL;
        public decimal NetPnL;
        public decimal? ReturnRatio;
        public decimal Bid;
        public decimal Ask;
        public decimal FeeRate;
        public decimal OpenCost;
        public decimal OpenFee;
        public decimal OrderMargin;
        public int SelectedLeverage = 5;
        public bool HasPosition;
        public string PositionDirection = "";
        public int PositionLeverage;
        public decimal EntryPrice;
        public decimal PositionMargin;
        public long PositionOpenTick, CurrentTick;
        public long? HeldCandles { get { return HasPosition ? (long?)AccountPerformance.HeldCandles(PositionOpenTick, CurrentTick) : null; } }
        public decimal? UnrealizedReturnRatio
        { get { return AccountPerformance.UnrealizedReturnRatio(UnrealizedPnL, HasPosition ? PositionMargin : 0m); } }
        public decimal LiquidationPrice;
        public long Deposited;
        public long Withdrawn;
        public IReadOnlyList<MarketCandle> Candles = Array.Empty<MarketCandle>();
        public string[] RecentEvents = Array.Empty<string>();
        public string PersistenceStatus = "";
        public IReadOnlyList<MarketOption> Markets = Array.Empty<MarketOption>();
        public IReadOnlyList<ExchangeOffer> ExchangeOffers = Array.Empty<ExchangeOffer>();
        public readonly Dictionary<int, int> ExchangeInventory = new Dictionary<int, int>();
        public int ExchangeItemId;
        public int ExchangeTier;
        public bool ExchangeUnlocked;
        public decimal ExchangeFeeRate;
        public int ExchangeBackpackQuantity;
        public decimal BuildingCoinBalance;
        public bool MarketEnabled;
        public bool ResourcesEditable;
        public IReadOnlyList<MarketOption> ResourceOptions = Array.Empty<MarketOption>();
    }
}
