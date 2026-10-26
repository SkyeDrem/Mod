# FXDSP — DSP 交易平台

通过交易赚取物资，让市场成为工厂的一部分。支持中文和英文，随游戏语言切换。

## 玩法

- 原料、材料、武器、消耗品、六种矩阵分别拥有独立账户与行情。选择启用品种，存入对应物品，用该物品作为保证金，盈利也以同种物品提取。
- 支持做多、做空、仓位比例与1～100倍杠杆。K线显示行情、价格刻度与爆仓线；保证金不足时自动强制平仓。
- 建筑币拥有独立行情，可按固定价格双向兑换71种建筑、地基、物流运输与战斗单位。五级兑换与六级手续费升级通过科技解锁。
- 行星级交付平台、星际交付平台保留原物流塔的槽数与物流功能。设置物品即可唯一绑定账户，连接交易系统与工厂；同一普通物品只能绑定一个槽位，翘曲器除外。
- 交付平台提供扩展储存容量，支持从平台向账户存入或从账户提取到平台。配方分别消耗4座行星物流塔、5座星际物流运输站，制造台制作60秒。
- 全部持仓页面与右上角持仓面板提供查看、平仓及兑换入口。平台内提供18项独立成就，达成后弹窗提示。

## 开始使用

1. 研究“交易系统”，消耗10铁矿、10铜矿。
2. 按 **F8** 打开或关闭平台，在物品选择页启用品种。
3. 存入该物品，选择仓位比例和杠杆，开多或开空。
4. 平仓后可把账户可用余额提取为实物。建筑币通过建筑兑换页面获取和使用。

新档默认只开启建筑币行情。资源与矩阵按需启用，停用后保留余额与历史，有持仓时须先平仓。暂停游戏时暂停交易与行情。右上角面板可隐藏，并通过“持仓”按钮恢复。

## 安装

推荐使用r2modman或Thunderstore Mod Manager，安装FXDSP并同时安装依赖：

- [BepInEx 5.4.17](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/BepInEx/)
- [DSPModSave 1.2.2](https://thunderstore.io/c/dyson-sphere-program/p/CommonAPI/DSPModSave/)
- [LDBTool 3.0.3](https://thunderstore.io/c/dyson-sphere-program/p/xiaoye97/LDBTool/)

手动安装：先安装以上依赖，将压缩包中`plugins/FXDSP`目录复制到游戏或配置文件夹的`BepInEx/plugins/FXDSP`，然后完整重启游戏。目录内应有`FXDSP.dll`和`FXDSP.DeliveryBootstrap.dll`两份插件。

不需要ScriptEngine、ConfigurationManager、dnSpyEx或.NET SDK。上述工具仅供开发调试使用。

从开发配置迁移时，请先备份并移出`BepInEx/scripts/FXDSP.dll`和旧版本插件副本，再安装发布包，避免同一插件重复加载。保留现有配置和存档文件。

## 存档与兼容性

交易资产、交付扩展库存和成就进度由DSPModSave保存。备份或传递存档时，应同时保留游戏存档和对应的`.moddsv`旁档。新增建筑和科技需要完整重启后加载；存档包含交付平台时，应保留本Mod再读取。

物品/配方ID为11702、11703，科技ID为1951、7701～7705、7801～7806。与占用相同ID的Mod不能共存；检测到冲突时会停止注册。建筑兑换使用原版71项固定目录，没有自动为其它Mod新增建筑定价。

本版通过233项自动测试及原版接口、图标、文本解析与价格校验。其它大型改造Mod组合和多人联机尚未完成实测；联机账户与库存同步尚未实现。

遇到问题请保留`BepInEx/LogOutput.log`，并记录游戏版本、Mod列表、触发步骤；涉及存取时同时记录操作前后物品数量。

---

# FXDSP — DSP Trading Platform

Trade for supplies and connect your profits to your factory. The interface follows the game's Chinese or English language setting.

## Features

- Separate markets and accounts for resources, materials, weapons, consumables and all six factory matrices. Each account uses its own item as collateral and pays out the same item.
- Long and short positions, allocation controls, 1–100x leverage, candlesticks and liquidation prices. Positions are liquidated when their margin becomes insufficient.
- An independent Building Coin market with fixed-price, two-way exchange for 71 buildings, foundations, logistics units and combat units. Research unlocks five exchange tiers and six fee upgrades.
- Planetary and Interstellar Delivery Platforms retain normal logistics functionality and add expanded storage. Configure an item in a platform slot to bind it to its trading account. Ordinary items can be assigned only once globally; Space Warpers are exempt.
- Craft the platforms in an assembler: 4 Planetary Logistics Stations or 5 Interstellar Logistics Stations, respectively, taking 60 seconds.
- Portfolio management, a compact positions HUD and 18 in-platform achievements with completion notifications.

## Getting started

Research **Trading System** with 10 Iron Ore and 10 Copper Ore, then press **F8**. Enable a market, deposit its item, choose an allocation and leverage, and open a long or short position. Close the position to realize your result and withdraw available funds as items. Obtain and spend Building Coins through the exchange page.

Only the Building Coin market is enabled by default. Disabled markets retain their funds and history; close a position before disabling its market. Trading and prices pause with the game.

## Installation

Install with r2modman or Thunderstore Mod Manager and include all three dependencies: **BepInEx 5.4.17, DSPModSave 1.2.2 and LDBTool 3.0.3**.

For manual installation, install those dependencies and copy `plugins/FXDSP` from this archive into `BepInEx/plugins/FXDSP`. Both `FXDSP.dll` and `FXDSP.DeliveryBootstrap.dll` are required. Restart the game completely.

ScriptEngine, ConfigurationManager, dnSpyEx and the .NET SDK are not required. When moving from a development installation, back up and remove the old `BepInEx/scripts/FXDSP.dll` and duplicate plugin copies before installing this package. Keep existing saves and configuration files.

## Saves and compatibility

Keep each game save together with its matching `.moddsv` sidecar when backing up or sharing saves. Keep this mod installed when loading saves containing its delivery platforms.

Reserved item/recipe IDs: **11702, 11703**. Technology IDs: **1951, 7701–7705, 7801–7806**. Mods occupying the same IDs conflict; registration stops on detected conflicts. The exchange uses a fixed catalog of 71 vanilla items and does not price other mods' buildings automatically.

This build passed 233 automated tests and additional native API, icon, text-parser and pricing checks. Overhaul combinations and multiplayer have not been playtested; multiplayer account and inventory synchronization is not implemented.

For bug reports, retain `BepInEx/LogOutput.log` and include the game version, mod list and steps to reproduce.
