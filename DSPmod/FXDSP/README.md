# FXDSP

《戴森球计划》游戏内物品交易 Mod，项目名 FXDSP（原名 DSPFX）。

当前 **1.1.0**提供原料、材料、武器、消耗品、矩阵、建筑币六个同级入口。新档仅运行建筑币行情，其余品种由玩家选择启用。金额显示两位小数，行情价保留四位。各自独立行情、余额和逐仓持仓，支持仓位比例、1～100×杠杆、K 线价格刻度和爆仓线。显示名称为 DSP 交易平台。研究“交易系统”（10 铁矿及 10 铜矿）后按 F8 打开，首次进入原料选择页并显示介绍卡片。黑雾掉落统一在材料中，矩阵仅六种工厂矩阵；沙土归消耗品并直接对接游戏沙土总量。初始开平仓手续费为名义价值的 0.3%，建筑兑换双向手续费为 10%，可通过六级科技逐步降至零。右上角持仓面板提供平仓、兑换及详情入口。累计盈亏下显示收益率，按累计已实现盈亏除以净投入计算，净投入非正显示“—”。

建筑币可以固定比价双向兑换原版可建造物、地基、三类物流运输单位和五类战斗单位。兑换按建筑解锁科技及其前置研究所需的最高矩阵分为蓝、红、黄、紫、绿五级，入口位于建筑币左栏；初始建筑归一级，自演化研究站归四级。五级兑换分别需要研究升级页的对应科技（蓝 200、红 400、黄 1000、紫 2000、绿 4000），买入和回收均校验。交易系统单独放在战场分析基站左侧，交易系统、五级兑换和六级效率已接入白色科技图标，保留罗马数字等级；图集随常驻模块内嵌发布。交付平台必须通过制造台生产，不加入建筑币兑换。资源与矩阵账户仍隔离。兑换价格由本机配方逐级加工成本计算，覆盖 71 项建筑及单位；完整配方、材料价值和计算过程见 [建筑币定价文档](docs/building-values-0.14.0.md)，界面和目录步骤见 [0.18.0 检查与验收](docs/audit-0.18.0.md)。

仅保留行星级交付平台、星际交付平台两种物流塔，复用原版模型并保留原版槽数与运输功能。原生制作面板有独立条目和介绍：4 座行星物流塔合成一座行星级交付平台，5 座星际物流运输站合成一座星际交付平台，配方均为 60 秒，仅制造台制作。塔内选择物品即自动绑定对应市场，不需要管理面板：不同物品可分属不同塔；每种普通物品全局仅一个交付槽，翘曲器除外，重复设置弹窗显示占用位置。存取各有独立数量输入与滑条，扩展库存不设玩法容量上限，每次交易重新核对实物，只按实际移动量记账。

停用资源保留余额与历史，重新启用不补算停用期间行情；有持仓时需先平仓才能停用。

1.1.0 起，新建市场由系统随机源生成种子；存档校验通过后只重置各行情的随机种子，保留当前报价、趋势动量、历史和账户。SL 后未来行情重新分支，F6 热重载、暂停和品种切换继续已有随机序列。实现和本轮检查见 [1.1.0 随机数更新](docs/acceptance-1.1.0.md)。

首次安装或更新常驻建筑/科技模块需要完整重启。发布包包含两个1.1.0模块，采用常规BepInEx插件安装；不依赖ScriptEngine、ConfigurationManager、dnSpyEx或.NET SDK。后续工作不使用子智能体。

欢迎文稿使用原版支持的颜色强调，已移除会露出的加粗标签；标题栏“说明”可随时重新阅读。价格采用 1.5+材料种类数/2，普通传送带 7、极速带 2,409、星际塔 586,845；0.20.2 按用户指令完成233项完整测试及Release构建，并核对原版接口、图标、文本与价格；详细结果见 [发布前检查](docs/audit-0.20.2.md)。

界面、提示、说明、操作记录、建筑和科技已支持跟随游戏语言切换中英文，翻译随 DLL 内嵌，无新增依赖。验收见 [0.19.0 中英文说明](docs/acceptance-0.19.0.md)。

账户盈亏金额不带物品名，浮动百分比明确标注收益率；全部持仓分列显示收益率与持有时间。结算和新平仓记录显示净收益率，右上面板保持原样。见 [0.19.2 验收](docs/acceptance-0.19.2.md)。

## 发布包与成就

发布包位于 `dist/FXDSP-1.1.0.zip`，附带SHA256和逐文件清单。面向玩家的中英文说明见 [release/README.md](release/README.md)。必需依赖为BepInEx 5.4.17、DSPModSave 1.2.2、LDBTool 3.0.3；不打包游戏或第三方依赖DLL。

交易平台内提供18项独立成就，支持中英文及达成提示，进度随存档和F6保存。盈亏同源、龙虎榜一要求该品种从未提取过物品；建筑币兑换物品计作提取。A9、建筑自由描述使用千位逗号。其余成就条件见 [成就说明](docs/acceptance-0.20.1.md)，最新文案位于源码与发布版内。

重新构建与打包：

```powershell
.\scripts\build.ps1 -Configuration Release
.\scripts\test.ps1
.\scripts\package.ps1
```

内置可关闭的本地异常检测兼容功能，用户已反馈此前版本的游戏内测试没有问题。实现与验收记录见 [异常检测兼容验收](docs/acceptance-abnormality-bypass.md)；最近一次完整检查见 [1.1.0发布检查报告](docs/audit-1.1.0.md)，历史记录见 [1.0.0发布检查报告](docs/audit-1.0.0.md)。备份统一存放在项目内 `backups/`，不提交 Git。

## 环境与初始化

Windows x64，BepInEx 5、DSPModSave 1.2.2、LDBTool 3.0.3，目标框架 .NET Standard 2.1。当前本地游戏程序集依赖 netstandard 2.1，目标选择依据见 [BepInEx 开发文档](https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/2_plugin_start.html)。

在项目根目录运行：

```powershell
# SDK 10.0.402，仅用来编译；项目本地安装，不修改系统 PATH。
.\scripts\setup-sdk.ps1

# 准备经过固定校验的存档库编译引用；不安装到游戏 Profile。
.\scripts\setup-modsave.ps1
.\scripts\setup-delivery.ps1
```

指定自己的完整游戏 Managed 目录与开发 Profile：

```powershell
.\scripts\setup-local.ps1 `
  -GameManagedDir 'C:\Games\Dyson Sphere Program\DSPGAME_Data\Managed' `
  -BepInExCoreDir 'C:\ModProfiles\FXDSP\BepInEx\core'
```

本机路径写入被 Git 忽略的 `build.local.json`，示例见 `build.local.example.json`。游戏和加载器 DLL 不纳入 Git，不随 Mod 发布。

0.2.0 的 UI 需要 InputLegacy、UI、TextRendering 等模块，应指向完整游戏 Managed 目录。旧版 VeinProtector 的精简引用目录可能不满足要求。

`setup-sdk.ps1` 从 Microsoft 下载固定版本 SDK，校验官方 SHA512 后解压到 `artifacts/tools/dotnet`；更新 `global.json` 版本时须同步更新脚本中的校验值。游戏运行时不需要安装该 SDK。已有匹配的系统 SDK 时可跳过这一步。

## 构建

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Configuration Release
```

输出为 `src/FXDSP/bin/Debug/FXDSP.dll` 或 `src/FXDSP/bin/Release/FXDSP.dll`，并生成 portable PDB。构建脚本优先使用本地 SDK，首次运行状态与缓存也保存在 `artifacts`。首次构建恢复 .NET Standard 2.1 引用包，`setup-modsave.ps1` 准备独立的存档库引用；准备好后无新依赖时可离线构建。

运行交易、存档会话和背包适配器自检：

```powershell
.\scripts\test.ps1
```

这不替代游戏内 UI、原版背包和真实存读档验收。

IDE 可打开 `FXDSP.sln`；需让 IDE 找到匹配 SDK，并为 MSBuild 设置 `DspManagedDir`、`BepInExCoreDir`。本地 JSON 由构建脚本读取，不由 IDE 自动读取。

## 部署

首次安装或升级到本版交付建筑，请使用包含原型模块与 LDBTool 的部署脚本，并重启：

```powershell
.\scripts\deploy-delivery.ps1 -BepInExDirectory 'C:\ModProfiles\FXDSP\BepInEx'
```

后续仅修改交易逻辑时，可继续使用下方 scripts 模式部署。

使用已有 BepInEx 和一份 DSPModSave 1.2.2 的独立测试 Profile。下面路径请替换成实际位置。使用 ScriptEngine 时将其 LoadOnStart 设为 true，让 FXDSP 在读档前加载；首次安装或依赖变化后完整重启游戏。

```powershell
# 只预览，不复制。
.\scripts\deploy.ps1 -BepInExDirectory 'C:\ModProfiles\FXDSP\BepInEx' -WhatIf

# 常规模式，部署后重启游戏。
.\scripts\deploy.ps1 -BepInExDirectory 'C:\ModProfiles\FXDSP\BepInEx'

# 热重载模式，需另外安装并验证 ScriptEngine，默认 F6 重载。
.\scripts\deploy.ps1 -BepInExDirectory 'C:\ModProfiles\FXDSP\BepInEx' -Mode Scripts
```

常规模式写入 `plugins/FXDSP/FXDSP.dll`；热重载模式写入 `scripts/FXDSP.dll`。两种模式只保留一份插件；脚本会同时检测旧名 `DSPFX.dll` 和新名 `FXDSP.dll`，避免重复加载。迁移既有安装时先备份旧 DLL / PDB 并移出插件搜索目录。配置 GUID 保留为 `dspfx.mod`，配置文件继续使用该名称。

部署脚本不自动删除文件、不启动游戏、不安装 ScriptEngine。

## 游戏内验收

1. 重启游戏并进入测试存档，本次可沿用 0.17.x 存档。日志应出现 `FXDSP 1.1.0 loaded` 及两种交付物流塔的原型注册日志。
2. 研究“交易系统”后 F8 打开窗口，选择物品 → 存入 → 选择仓位比例与杠杆 → 开多/开空 → 平仓 → 提取，分别核对物品余额与盈亏。
3. 保存再读档，检查资产；F6 后再 F8，检查账户没有重置或重复执行。
4. 新配方、行星站绑定、科技与费率、滑条和默认市场步骤见 [验收文档](docs/acceptance-0.14.0.md)。

编译成功不代表完成游戏内验证。加载、热重载及 Mod 兼容性需要实测。

## 开发边界

最新五种纯加工倍数（×2、1+k、1+k/2、1.5+k/2、2+k/2）的完整结果见 [五套价格对比](docs/building-price-five-schemes.md)，包含全部 71 项兑换价格及配方。用户已选定 1.5+k/2，0.18.4 已应用；其余列保留对照；复算使用 `python scripts/compare-five-building-values.py --check`。

材料种类倍数、加工时间倍数及两者结合的四套定价结果见 [离线价格对比](docs/building-price-scenarios.md)。包含全部 71 项兑换价格及上游材料，原方案一曾于 0.18.2 采用，0.18.3 曾采用 1+材料种类数/2，0.18.4 已采用 1.5+材料种类数/2，四套旧值保留作历史对照；复算使用 `python scripts/compare-building-values.py --check`。

当前采用每物品独立的物品本位保证金账户；交付平台独立美术与更多自动化选项留待后续。此前现货研究保留作参考，当前玩法以 [0.18.0 检查与验收](docs/audit-0.18.0.md) 为准。研究资料见 [研究索引](docs/research/README.md)，工程边界见 [开发说明](docs/development.md)。玩家说明与发布元数据统一维护于 `release/`，打包结果位于 `dist/`。

已确认的玩法目标：禁止不同物品市场之间兑换，鼓励通过交易赚取更多同类物资；玩家可以靠交易技巧替代该物品产线，不以保护产线为由默认设置净收益或供给硬上限。








