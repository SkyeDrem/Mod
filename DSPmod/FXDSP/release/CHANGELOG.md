# 1.3.1

- **追加保证金**可把同品种的可用余额免费转入已有仓位，降低实际杠杆并拉远爆仓线。收益率按追加后的总保证金计算。
- **同方向加仓**沿用原开仓杠杆扩大仓位，按成交数量重算入场均价和爆仓线。手续费按新增成交金额计算。
- 持仓面板可用同一个比例滑条预览两种操作的金额和手续费，并显示当前仓位金额。游戏内「说明」提供中英文操作介绍。
- 新版支持读取旧存档。用本版重新保存后，请用 1.3.0 或更高版本读取交易数据。

## English

- **Add margin** from the market's available balance for free to lower effective leverage and move the liquidation threshold farther away. Returns use the total margin after the top-up.
- **Increase a position** in the same direction at its original order leverage. The added fill updates the quantity-weighted average entry and the liquidation threshold. The trading fee is based on the added fill.
- The position panel uses one allocation slider to preview both actions and their fees. It also shows current position notional. The in-game guide explains both actions in Chinese and English.
- This release can read older saves. After saving with this release, use version 1.3.0 or later to read the trading data.

# 1.1.0

- 新建市场与读取存档使用新的随机种子，SL 后未来行情不再固定重演。
- 保留现有涨跌规则、存档价格、账户资产、持仓和历史；F6 热重载继续原行情随机序列。

## English

- New markets and save loads use fresh random seeds, so reloading a save no longer replays a fixed future price sequence.
- Existing price dynamics, saved prices, account assets, positions, and history are retained. F6 hot reload continues the current random stream.

# 1.0.0

- FXDSP 正式发布：物品本位杠杆交易、建筑币兑换、科技升级、物流交付平台与独立成就系统。
- 提供中文和英文界面及使用说明。

## English

- Initial stable release of FXDSP, featuring item-backed leveraged trading, Building Coin exchange, research upgrades, logistics delivery platforms, and in-platform achievements.
- Includes Chinese and English interfaces and documentation.
