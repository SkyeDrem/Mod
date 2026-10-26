# FXDSP — DSP 交易平台

**伊卡洛斯，老实铺产线很枯燥吧！**

除了扩大生产，你现在还可以通过交易获取物资。创建 DSP 交易账户，接入主脑交易平台：选择交易品种，用手中的物资作为保证金，预测行情涨跌，把盈利提取回工厂。

你可以满仓百倍杠杆梭哈、短线擒龙，也可以在低位耐心做多，等待行情回升。技术足够好，甚至可以让市场代替产线。特殊的建筑币还能双向兑换建筑，让交易平台成为你的建筑超市。

**FX 战士伊卡洛斯，祝你交易顺利！**

## 开始第一笔交易

1. 研究 **“交易系统”**，消耗 **10 铁矿 + 10 铜矿**。
2. 按 **F8** 打开交易平台，在原料、材料、武器、消耗品或矩阵的选择页启用想交易的物品。
3. 从背包存入该物品，选择 **仓位比例** 和 **杠杆**。
4. 预测上涨就 **开多**，预测下跌就 **开空**；之后可以主动平仓，结算本次盈亏。
5. 将可用余额提取成同种物资，或者提取到绑定的交付平台，接回工厂流水线。

首次打开会显示介绍，之后也可以点击标题栏的 **“说明”** 重新阅读。新档默认只运行建筑币行情，其余品种由你按需启用；停用市场会保留余额与历史，有持仓时需要先平仓。游戏暂停时，交易与行情也会暂停。

![DSP交易平台：行情、账户、仓位与物品存取](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E4%BA%A4%E6%98%93%E5%B9%B3%E5%8F%B0%E7%95%8C%E9%9D%A2.png)

## 每种物品，独立账户

原料、材料、武器、消耗品和六种工厂矩阵都有各自的行情、余额与持仓。**存入铜矿，赚到的就是铜矿；存入宇宙矩阵，赚到的就是宇宙矩阵。** 普通物品账户之间不能互换本金或收益，不能通过铁矿账户直接提取白糖。

账户余额按物品数量计算，允许保留小数；提取实物时取整件数量。存入与提取分别有独立输入框和滑动条，可以选择背包或交付平台作为来源。

**建筑币是独立的特殊账户**，通过建筑兑换获取和使用，也有自己的行情与持仓。

## 仓位、杠杆与爆仓

**仓位比例**决定这笔交易投入多少可用资金，**杠杆**放大同一段价格变化带来的收益与亏损。支持 **1～100 倍杠杆**，开仓时会预留手续费。

K 线图显示行情、价格刻度和当前持仓的爆仓线。每个市场可以持有一笔做多或做空仓位，平仓后才能重新开仓。持有时间以经过的 K 线数量显示。

- **浮动盈亏**：这笔未平仓持仓随行情变化的盈亏；其收益率按浮动盈亏除以实际保证金计算。
- **累计盈亏**：该账户累计已实现盈亏；对应收益率按累计盈亏除以净投入计算。净投入为总存入减总提取，非正时显示“—”。
- **平仓结算**：显示本次扣除开仓费和平仓费后的净盈亏与收益率。

如果行情走错方向，亏损过大触及爆仓条件，持仓会被强制平仓，并显示爆仓提示。高杠杆下，价格的一小段反向变化也可能吃掉保证金。

![爆仓结算与成就达成提示](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E7%88%86%E4%BB%93%E6%B8%85%E7%AE%97.png)

## 边铺工厂，边看持仓

右上角的紧凑持仓面板可随时查看各品种的浮动盈亏和收益率，并直接平仓；不必一直打开完整交易界面。

点击 **“兑换”** 可直达建筑币兑换，点击 **“详情”** 可打开全部持仓页面，逐项查看、平仓，或者一键全部平仓。面板可隐藏，也可通过“持仓”按钮恢复。

![右上角持仓面板](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E6%8C%81%E4%BB%93%E5%B0%8F%E9%9D%A2%E6%9D%BF.png)

## 建筑币：炒币，也能开超市

建筑币可按固定价格双向兑换 **71 种建筑、地基、物流运输单位和战斗单位**。你可以用背包里的建筑换取建筑币，也可以把建筑币换回想要的建筑。

如果你足够厉害或幸运，或许可以靠交易实现建筑自由。即使不想投机，也可以挑选适合量产的建筑赚取建筑币，再兑换其它建筑，把它当作工厂里的建筑超市。

兑换页显示单价、背包可用量、建筑币余额、手续费，以及本次买入需要或回收可得的金额。数量支持输入与滑动条，最终按背包实际收到或移出的数量结算。

![建筑币双向兑换界面](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E5%BB%BA%E7%AD%91%E5%B8%81%E5%85%91%E6%8D%A2.png)

建筑兑换分五级，在升级科技中逐步解锁；**买入和回收都需要对应等级**。各建筑归入哪个等级，按其原版解锁科技及前置研究所需的最高等级矩阵划分；自演化研究站固定为四级。

| 兑换等级 | 对应研究消耗 |
| --- | --- |
| 一级 | 200 电磁矩阵（蓝糖） |
| 二级 | 400 能量矩阵（红糖） |
| 三级 | 1,000 结构矩阵（黄糖） |
| 四级 | 2,000 信息矩阵（紫糖） |
| 五级 | 4,000 引力矩阵（绿糖） |

## 上传算力，降低佣金

研究 **“交易效率”**，可以逐步降低交易和建筑兑换手续费，直到完全免除。

| 等级 | 研究消耗 | 交易手续费 | 建筑兑换双向手续费 |
| --- | --- | --- | --- |
| 初始 | — | 0.3% | 10% |
| 1 | 400 蓝糖 | 0.2% | 10% |
| 2 | 800 蓝、红糖 | 0.1% | 8% |
| 3 | 1,200 蓝、红、黄糖 | 0.05% | 6% |
| 4 | 1,600 蓝、红、黄、紫糖 | 0.02% | 4% |
| 5 | 2,000 蓝、红、黄、紫、绿糖 | 0.01% | 2% |
| 6 | 4,000 白糖 | 0% | 0% |

多色研究的数量是 **每种矩阵各自消耗的数量**。交易手续费在开仓、平仓时分别收取，按持仓名义价值计算；兑换手续费在买入、回收时分别收取。

![建筑兑换与交易效率升级科技](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E5%8D%87%E7%BA%A7%E7%A7%91%E6%8A%80.png)

## 让盈利物资接入物流网络

解锁原版对应物流科技后，可以在制造台制作两种交付平台：

| 建筑 | 制作配方 | 制作时间 |
| --- | --- | --- |
| 行星级交付平台 | 4 座行星物流塔 → 1 座平台 | 60 秒 |
| 星际交付平台 | 5 座星际物流运输站 → 1 座平台 | 60 秒 |

平台保留对应原版物流塔的槽位数量与物流功能，并拥有深不可测的储存容量以应对大单交割。平台不通过建筑币兑换获取。

在 **交付平台的原版物流窗口** 中设置物品，就会自动绑定该物品的交易账户。不同物品可以绑定到同一座塔或不同的塔；**每种普通物品全局只能设置一次，翘曲器除外**。重复设置会提示已有绑定的位置。

随后在交易界面的存取区切换到 **“交付平台”**，即可从平台存入物资，或把账户余额提取到平台，再由传送带、分拣器与物流运输网络送入产线。交易执行时会重新核对库存。

## 18 项成就，记录伊卡洛斯的交易生涯

平台内有独立的成就页面，达成后弹窗提示，进度随存档保存。

从 **“交易新人”**、**“第一桶金”**，到 **“短线擒龙”**、**“耐心资本”**、**“建筑自由”**，再到 **“工厂？不需要了！”**，你可以用不同风格挑战自己的交易目标。完成其余全部成就，即可成为 **“投资达人”**。

“逛超市”和“进货”计入兑换列表里的全部物品，包含地基、运输单位与战斗单位；“盈亏同源”和“龙虎榜一”要求对应账户从未提取过物品，重新存入不会恢复资格。

![交易平台成就页面](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E6%88%90%E5%B0%B1%E7%B3%BB%E7%BB%9F.png)

## 安装

推荐使用 r2modman / Thunderstore Mod Manager 安装 FXDSP，并同时安装依赖：

- [BepInEx 5.4.17](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/)
- [DSPModSave 1.2.2](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/DSPModSave/)
- [LDBTool 3.0.3](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/)

手动安装时，先安装以上依赖，再把压缩包中的 `plugins/FXDSP` 复制到游戏或配置文件夹的 `BepInEx/plugins/FXDSP`，然后完整重启游戏。目录内应同时包含 `FXDSP.dll` 和 `FXDSP.DeliveryBootstrap.dll`。

游玩不需要 ScriptEngine、ConfigurationManager、dnSpyEx 或 .NET SDK。从开发配置迁移时，先备份并移出 `BepInEx/scripts/FXDSP.dll` 和旧插件副本，避免重复加载，保留现有配置和存档。

## 存档与兼容性

交易账户、交付扩展库存和成就进度由 DSPModSave 保存。备份或传递存档时，请同时保留游戏存档和对应的 `.moddsv` 旁档；存档中存在交付平台时，应保留本 Mod 再读取。

物品/配方 ID 为 **11702、11703**，科技 ID 为 **1951、7701～7705、7801～7806**；与占用相同 ID 的 Mod 不能共存。建筑兑换使用原版固定目录，不会自动给其它 Mod 的建筑定价。多人账户与库存同步尚未实现。

## 开发与反馈

源码和构建说明：[GitHub · SkyeDrem/Mod — FXDSP](https://github.com/SkyeDrem/Mod/tree/main/DSPmod/FXDSP)。

可在 [GitHub Issues](https://github.com/SkyeDrem/Mod/issues) 反馈问题。请附游戏版本、Mod 列表、触发步骤和 `BepInEx/LogOutput.log`；涉及存取时，同时记录操作前后的物品数量。

---

# FXDSP — DSP Trading Platform

**Icarus, getting tired of laying down production lines?**

Expanding your factory is no longer the only way to obtain supplies. Open a DSP trading account, connect to the mainframe's trading platform, put your items up as collateral, predict market moves, and withdraw your profits back into the factory.

Go all-in at 100x leverage and chase a quick rally, or patiently hold a long position after buying low. With enough skill, the market can even take the place of a production line. Building Coins add another option: a two-way exchange that can turn the platform into your own building supermarket.

**Good luck, FX Warrior Icarus!**

The screenshots below show the Chinese interface. The mod itself supports Chinese and English and follows the game's language setting.

## Your first trade

1. Research **Trading System** with **10 Iron Ore + 10 Copper Ore**.
2. Press **F8** and enable an item in the raw materials, materials, weapons, consumables or matrix selection page.
3. Deposit that item from your backpack, then choose your **allocation** and **leverage**.
4. Open **long** if you expect a rise, or **short** if you expect a fall. Close the position when you want to settle its result.
5. Withdraw available funds as the same item, either into your backpack or into its bound delivery platform.

An introduction appears on your first visit; use **Help** in the title bar to read it again. Only the Building Coin market is enabled by default. Other markets are optional, retain their funds and history when disabled, and must have no open position before they can be disabled. Trading and prices pause with the game.

![Trading platform: market chart, account, positions and item transfers](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E4%BA%A4%E6%98%93%E5%B9%B3%E5%8F%B0%E7%95%8C%E9%9D%A2.png)

## Each item has its own account

Raw materials, materials, weapons, consumables and all six factory matrices have separate prices, balances and positions. **Deposit Copper Ore and earn Copper Ore; deposit Universe Matrices and earn Universe Matrices.** Ordinary accounts cannot exchange collateral or profits with each other: an Iron Ore account cannot directly pay out white science cubes.

Balances can include fractions, while physical withdrawals use whole items. Deposits and withdrawals have separate inputs and sliders, and can use your backpack or a delivery platform.

**Building Coins have their own special account**, funded and spent through building exchange, with an independent market and position.

## Allocation, leverage and liquidation

**Allocation** determines how much of your available funds goes into a trade. **Leverage** magnifies both gains and losses from a price move. Leverage ranges from **1x to 100x**, with opening fees reserved when you place the trade.

The candlestick chart shows prices and the current position's liquidation level. Each market supports one long or short position at a time; close it before opening another. Holding time is displayed as the number of elapsed candlesticks.

- **Unrealized P&L** changes with an open position. Its return is unrealized P&L divided by the actual posted margin.
- **Cumulative P&L** is the account's realized result. Its return is cumulative realized P&L divided by net deposits: total deposits minus total withdrawals. Non-positive net deposits display “—”.
- **Settlement** reports the trade's net result and return after both opening and closing fees.

A position is forcibly closed when losses reach the liquidation condition. At high leverage, even a small adverse price move can consume the margin.

![Liquidation settlement and achievement notification](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E7%88%86%E4%BB%93%E6%B8%85%E7%AE%97.png)

## Keep trading while building

The compact HUD in the top-right corner shows each position's unrealized P&L and return, with a direct close button.

Use **Exchange** to jump to building exchange or **Details** to open the portfolio page. There you can inspect and close individual positions, or close all positions with one button. Hide the HUD when you need the space and restore it with the **Positions** button.

![Compact positions HUD](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E6%8C%81%E4%BB%93%E5%B0%8F%E9%9D%A2%E6%9D%BF.png)

## Building Coins: trade them, or open a supermarket

Exchange Building Coins at fixed prices for **71 buildings, foundations, logistics units and combat units**. Sell buildings from your backpack for coins, or spend coins to obtain the buildings you need.

Skillful or lucky trading might buy you building freedom. If speculation is not your thing, mass-produce a building that suits your factory, sell it for coins, and exchange them for other buildings instead.

The exchange page shows unit prices, owned quantities, your coin balance, fees, and the total purchase cost or sale proceeds. Enter a quantity or use the slider; settlement uses the actual quantity accepted by or removed from your backpack.

![Two-way building exchange](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E5%BB%BA%E7%AD%91%E5%B8%81%E5%85%91%E6%8D%A2.png)

There are five exchange tiers, unlocked through upgrade research. **Both buying and selling require the corresponding tier.** A building's tier follows the highest matrix required by its vanilla unlock technology and prerequisites; the Self-evolution Research Lab is explicitly assigned to Tier 4.

| Tier | Research cost |
| --- | --- |
| 1 | 200 Electromagnetic Matrices (blue) |
| 2 | 400 Energy Matrices (red) |
| 3 | 1,000 Structure Matrices (yellow) |
| 4 | 2,000 Information Matrices (purple) |
| 5 | 4,000 Gravity Matrices (green) |

## Contribute computing power, pay less commission

Research **Trading Efficiency** to reduce trading and building exchange fees, eventually eliminating both.

| Level | Research cost | Trading fee | Building exchange fee, each direction |
| --- | --- | --- | --- |
| Initial | — | 0.3% | 10% |
| 1 | 400 blue matrices | 0.2% | 10% |
| 2 | 800 blue and red matrices | 0.1% | 8% |
| 3 | 1,200 blue, red and yellow matrices | 0.05% | 6% |
| 4 | 1,600 blue, red, yellow and purple matrices | 0.02% | 4% |
| 5 | 2,000 blue, red, yellow, purple and green matrices | 0.01% | 2% |
| 6 | 4,000 Universe Matrices (white) | 0% | 0% |

Multi-colour costs apply to **each listed matrix type**. Trading fees apply separately on opening and closing, based on notional value; exchange fees apply separately to purchases and sales.

![Building Exchange and Trading Efficiency research](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E5%8D%87%E7%BA%A7%E7%A7%91%E6%8A%80.png)

## Connect your profits to the logistics network

After unlocking the corresponding vanilla logistics technology, craft these platforms in an assembler:

| Building | Recipe | Crafting time |
| --- | --- | --- |
| Planetary Delivery Platform | 4 Planetary Logistics Stations → 1 platform | 60 seconds |
| Interstellar Delivery Platform | 5 Interstellar Logistics Stations → 1 platform | 60 seconds |

Platforms retain their original station's slot count and logistics functions, with expanded storage for large deliveries. They are obtained through crafting rather than Building Coin exchange.

Configure an item in the **platform's normal logistics window** to bind its trading account automatically. Different items may use the same tower or different towers. **Each ordinary item can be assigned to only one delivery slot globally; Space Warpers are exempt.** Duplicate assignments show where the existing binding is located.

Choose **Delivery Platform** in the trading window's transfer area to deposit from the tower or withdraw into it. Your belts, sorters and logistics network can then feed those items into production. Inventory is checked again when the transfer executes.

## 18 achievements for your trading career

The platform has its own achievement page and completion notifications. Progress is saved with your world.

From **Rookie Trader** and **First Pot of Gold** to **Scalping the Dragon**, **Patient Capital**, **Building Freedom** and **A Factory? Not Anymore!**, different trading styles offer different goals. Complete every other achievement to become a **Master Investor**.

Shopping Spree and Stocking Up count every item in the exchange catalog, including foundations, logistics units and combat units. Easy Come, Easy Go and Top of the Leaderboard require no prior withdrawals from that account; redepositing does not restore eligibility.

![In-platform achievements](https://raw.githubusercontent.com/SkyeDrem/Mod/main/DSPmod/FXDSP/image/readme/%E6%88%90%E5%B0%B1%E7%B3%BB%E7%BB%9F.png)

## Installation

Install FXDSP with r2modman or Thunderstore Mod Manager and include its three dependencies:

- [BepInEx 5.4.17](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/)
- [DSPModSave 1.2.2](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/DSPModSave/)
- [LDBTool 3.0.3](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/)

For manual installation, install those dependencies and copy `plugins/FXDSP` from the archive into `BepInEx/plugins/FXDSP`. Both `FXDSP.dll` and `FXDSP.DeliveryBootstrap.dll` must be present. Restart the game completely.

Playing does not require ScriptEngine, ConfigurationManager, dnSpyEx or the .NET SDK. When moving from a development installation, back up and remove the old `BepInEx/scripts/FXDSP.dll` and duplicate plugin copies. Keep your existing saves and configuration.

## Saves and compatibility

DSPModSave stores trading accounts, expanded delivery inventory and achievement progress. Keep game saves together with their matching `.moddsv` sidecars when backing up or sharing them. Keep this mod installed when loading saves containing delivery platforms.

Reserved item/recipe IDs are **11702, 11703**; technology IDs are **1951, 7701–7705, 7801–7806**. Mods using the same IDs conflict. Building exchange uses a fixed vanilla catalog and does not automatically price other mods' buildings. Multiplayer account and inventory synchronization is not implemented.

## Development and feedback

Source code and build instructions: [GitHub · SkyeDrem/Mod — FXDSP](https://github.com/SkyeDrem/Mod/tree/main/DSPmod/FXDSP).

Report issues through [GitHub Issues](https://github.com/SkyeDrem/Mod/issues). Include the game version, mod list, reproduction steps and `BepInEx/LogOutput.log`. For transfer issues, also record item quantities before and after the operation.
