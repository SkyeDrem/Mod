# FXDSP 研究索引

研究日期：2026-10-25。由三个子智能体分别研究，主智能体复核并整合。学习现有实现的目的是提炼可独立实现的模式；本文档不表示这些第三方 Mod 已在当前游戏环境验证兼容。

后续用户已要求停止使用子智能体，并将交易方向改为铜本位保证金、多空与杠杆。本文保留原研究过程；当前实现与验收以 [0.3.0 说明](../acceptance-0.3.0.md) 为准，不继续把现货结算额度当作当前界面要求。

## 先读什么

1. [本轮方案评估与推荐](../design/proposal-2026-10-25.md)：用户设想、推荐账本、平衡漏洞、科技和交付路线、待决定事项。
2. [原版风格 UI](dsp-native-ui.md)：UXAssist、LSTM 的实际窗口、控件、物品行、事件和刷新实现；有固定提交与源码行链接。
3. [科技、交付与存档](dsp-progression-delivery.md)：CommonAPI、GenesisBook、MoreMegaStructures、DSPModSave 的实际案例。
4. [现货市场模型](spot-market-model.md)：真实现货概念、分物品额度与库存、报价/深度、数值案例、经济约束和解锁指标。

## 来源与可复用案例

| 主题 | 优先定位的实现 | 来源清单 |
| --- | --- | --- |
| 原版窗口外壳、控件、本地化 | UXAssist `MyWindow`、`LayoutHelper`、`MyFlatButton` | [sources-ui.json](sources-ui.json) |
| 物流站式物品行、滚动和列表更新 | LSTM `UIBalanceListEntry`、`UIBalanceWindow` | 同上 |
| 非矩阵材料科技 | CommonAPI `ProtoRegistry.RegisterTech`、Genesis `data/techs.json` | [sources-progression.json](sources-progression.json) |
| 克隆专用物流站、部分交付 | MoreMegaStructures `MMSProtos`、`StarAssembly` | 同上 |
| 旁档、保存生命周期与动态注册 | DSPModSave `IModCanSave`、保存补丁 | 同上 |
| 现货、订单、可用与冻结资产 | CFTC、Coinbase、Kraken 一手说明 | [sources-market.json](sources-market.json) |

## 后续工作如何使用

- 开始某一模块前，先读相应研究文档，再按固定提交链接定位代码；不要直接假定 master 分支和研究版本相同。
- 原文件缓存和许可证放在 `artifacts/references/ui`、`artifacts/references/progression`，由 Git 忽略。缓存用于本机学习，不参与编译。
- 研究摘要与来源清单保留在 Git 可管理的 docs 中。缓存丢失时，可根据 sources 清单重新取得所需文件。
- 研究中提到的 `NativeWindowFactory`、市场服务等是建议的 FXDSP 结构，不是已经存在的类。
- 移植第三方代码或素材前重新检查对应文件许可；不同仓库、子模块、源码和美术许可可能不同。优先学习后独立实现，不批量搬运。
- 已对部分本地游戏接口作静态检查；运行时层级、材料消耗、容器插入及新内容热重载仍要游戏实测。

## 当前推荐边界

每物品独立现货账户、实物托管库存与结算额度分离；交易引擎复用；基础能力走原生科技、经营条件走市场认证；先验证一个箱子的交付，再增加专用箱/塔。物品名单、数值、盈利提前解锁的奖励力度暂未定案。

**用户明确要求：阻止跨物品兑换，鼓励通过交易获取更多同类物品，允许玩家用市场收益替代该产线。**有限 NPC 库存、补货和净供给上限不作为保护产线的默认要求。研究中的真实市场概念用于借鉴，不自动成为游戏限制。
