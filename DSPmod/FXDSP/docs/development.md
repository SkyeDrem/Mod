# 开发说明

## 0.18.0 当前实现

- 兑换标签整段着色，图标与名称居中。原型所有权以 AppDomain 中原生对象引用共享，Bootstrap 与脚本模块分别编译同一纯托管检查，不把脚本对象或委托保存到共享槽；F6 不清除常驻原型标记。
- 科技注册/Finalize、文案替换、科技解锁检查与交付服务均增加归属边界。固定 ID 仍要求稳定，不声称支持自动重映射或大型整合包。
- 拆除 Prefix 暂存库存，Postfix 仅在同一实体、坐标、原型及组件仍存在时恢复被取消的拆除。真正拆除仍保留库存；Tick 清理空的已拆除记录，防止无资产元数据长期增长。
- 179 项模型及原生边界替身测试通过，另验证原版图标解析器、实际编译导入器及价格生成。完整报告与游戏验收见 [0.18 检查报告](audit-0.18.0.md)。

## 0.17.2 实现记录

- 对本机 IconSet.Create 的科技分支检查发现其 Graphics.CopyTexture 参数固定源左下角及尺寸 80×80，未使用 Sprite.rect。0.17 的独立大图形虽能在节点中显示，但行内图集截取透明边角，0.17.1 注册了名称后仍不显示行内图标。
- 导入流程维持原有去底、归一化和留白，随后 ResizeMask 对 alpha 做面积平均，输出完全覆盖原版采样区域的 80×80 纹理及 Sprite，避免最近邻缩小时丢失细等级笔画。保留六字符行内标签，保证文字和图像两条路径都适配。
- test-native-tech-icon-size.ps1 使用 PowerShell 7 调用实际编译 ResizeMask，和实际游戏 IconSet.kIconSize 对比；仅执行托管像素代码。回归重现旧大纹理角落空白并验证新图形、透明区域和细笔画。图集资产与金融逻辑不改，未重跑无关交易测试。
- 记录保留已核对：MarginMarket 日志上限 10，UI 取最近 6；SpotMarket.HistoryLimit=120，MarginMarket 将周期覆盖为 90 tick，MarketCandles 每报价点一根。固定窗口覆盖约三分钟游戏时间，不持续追加无界历史；累计资产字段独立保存。见 [0.17.2 验收](acceptance-0.17.2.md)。

## 0.17.1 实现记录

- 修复原型 IconTag 留空导致的行内引用错误。原版 TechProto.Preload 拼接 iconTagString / iconNameTagString，前置悬浮提示与底部条件使用这两者，和节点 `_iconSprite` 无关；IconSet.Create 为非空标签建立 UV 与友好名称配置。
- InlineIconTag 使用 fx+四位科技 ID。检查本机 UnityEngine.UI.Youthcat.TextIconMapping.StringToKey 确认仅 1～6 字符有效，过长返回零；12 个六字符键唯一。字段在 Preload 前设定，原版图标集随后采集现有自定义 Sprite，无需额外 UI 字符串补丁或新美术。
- Native 测试直接加载本机 UnityEngine.UI.dll 的托管字符串解析代码；绕过 ScriptableObject 构造，只创建托管映射配置，不调用 Unity 原生函数。24 个纯图标/含名称标记、23 个科技目录检查通过，Debug/Release 构建零警告零错误；不重复无关交易测试。
- 标签属于启动时缓存，部署后需完整重启。科技 ID、研究状态、费用、图像和账户格式均不改变；见 [验收步骤](acceptance-0.17.1.md)。

## 0.17.0 实现记录

- 用户已确认图标接入，保留 0.16 概念板。imagegen 内置编辑生成 4×3 RGBA 生产图集，嵌入 FXDSP.DeliveryBootstrap.dll，逻辑资源名为 `FXDSP.DeliveryBootstrap.Assets.technology-icons.png`；不需要 AssetBundle 或玩家手工复制 PNG。
- TechnologyIcons 在原型注册前解码图集，按 alpha 和亮度清理提取残留、统一中性白色，测量各格边界。裁取图形与等级字符后添加约 8% 透明留白，建立独立方形 RGBA32 纹理与 FullRect Sprite，双线性、无 mipmap，保留可读性供原生图标缓存使用。原始解码纹理释放，12 个 Sprite 由常驻静态缓存保留，不逐帧处理。
- 精确顺序为交易系统、兑换 I～V、效率 I～VI，直接由 TradeTechnology 的家族与等级映射。Apply 在 TechProto.Preload2 之后更新私有 `_iconSprite`；本机 getter 直接返回该字段。LDBTool 后续 IconSet.Create 将接收自定义图标，节点、详情和队列均使用同源图片。读取失败记录日志并保留原版路径/图标。
- 图像模块引用为游戏自带 UnityEngine.ImageConversionModule，无额外玩家依赖。透明通道与 12 格边界只读检查通过；Debug/Release 均零警告零错误，两个模块 DLL 中的嵌入 PNG 哈希均与源文件一致。本次纯美术资源接入没有重复运行未修改的交易模型检查，Unity 实际显示待 [0.17 游戏验收](acceptance-0.17.0.md)。

## 0.16.0 实现记录

- 主页仅交易系统 1951，参照原生战场分析基站 1826 的坐标左移 4 个网格单位，保持同一行；兑换改为 7701～7705（本机原型表验证无占用），与效率 7801～7806 都在升级页，分别成行。两个家族都用 Level=MaxLevel 提供原生等级；首级以隐式前置避免给独立交易系统画线。
- TradeTechnology 新增 ExchangeTier、UnlockText，RegistrationName 同时使用家族名称与等级，防止两个升级家族在 LDBTool 中同名绑定冲突。兑换蓝 200、红 400，后三级与效率成本不变；不迁移旧 1952～1956 研究标志。
- 本机 UITechNode.UpdateInfoComplete 先调用 TechProto.UnlockFunctionText 并测量效果文本，再移动成本区；底部独立读取 TechProto.description。TradeTechnologyTextPatch 在原方法执行前返回本 Mod 效果文字，保持多行测量和布局正常，不虚构 UnlockFunctions 触发原版副作用。Desc 用于用户的底部描述，Conclusion 用于原生研究完成提示。
- AccountPerformance 从已实现盈亏和现金流累计计数读取收益率，净投入非正返回 nullable 空值；MoneyDisplay.Percent 以格式化器缩放百分比，避免极端 decimal 金额先乘 100 导致溢出。快照和 UI 不新增存档字段，不改变账户账本。
- 累计盈亏下加入收益率，账户上方金额的标签补齐全角空白，压缩账户行间距以保留原下单控件边界。只有文档预览目录增加图像，无游戏美术资源接入；预览来源与完整提示词见 previews/tech-icons-0.16.0.md。
- 171 项检查通过（14 核心、55 保证金、27 会话、22 背包、15 交付核心、16 交付接口、22 科技/目录/显示），并完成 Debug / Release 编译。真实科技位置、两处文案测量和收益率排版仍待 [游戏验收](acceptance-0.16.0.md)。

## 0.15.0 实现记录

- 科技线起点从 x=5 改为 x=9，节点间距 4 不变。所有黑雾专属掉落归材料；矩阵仅工厂六色。ResourceMarketCatalog 现在包含沙土，104 个行情品种与 71 项兑换覆盖 175 个原生物品。
- 原版 Player.sandCount 为 long，SetSandCount(long, ESandSource) 负责历史特性和数量通知；沙土单次 int 操作前校验余额、总计数与 long 上限，再调用原生 Other 来源。可用量返回 BigInteger 保持完整 long 显示，Count 接口兼容性限幅 int。沙土 UI 禁用交付塔来源，实际命令也独立路由至原生沙土计数。
- MarketBook 格式 v4 在原市场列表后追加已介绍标记，v3 读取路径不带该字段；外层快照 v2 与交付块不变。MarketSession 在世界归属核对后确认首次进入，标记随账本写入磁盘及 F6 交接。MarketWindow 首次进入强制原料选择页，并创建独立的留白遮罩卡片，暂时屏蔽 HUD 与穿透。
- TransferSelection 分别记录存入/提取的比例和数量；品种/来源变化时按同一步数换算新数量，库存为零不抹去比例，未变更的最大量不再从取整数量反算比例。ExchangeAmounts 计算含费可买与可卖数量作为独立兑换滑条的范围，并修正微小手续费向上取整导致的边界超支。
- MoneyDisplay 统一盈亏千位分隔；TradingLogDisplay 同时兼容带逗号旧展示并防止重复格式破坏数字。操作记录用 TextGenerator 测量实际宽度后截断，仍只显示六行。方向和等级色字局部启用富文本，不更改普通文字材质。
- 169 项检查通过（14 核心、55 保证金、27 会话、22 背包/沙土、15 交付核心、16 交付接口、20 科技/目录/数量/显示），并复算所有现有价格。实际沙土通知、介绍遮罩、滑条与字号等待 [游戏验收](acceptance-0.15.0.md)。

## 0.14.0 实现记录

- 从本机 `resources.assets` 提取 Item / Recipe 原型，以本机中文本地化名称整理最少字段的事实表 `docs/data/production-recipes.json`。提取用 UnityPy 与 TypeTreeGeneratorAPI，仅安装在忽略的 artifacts/tools 目录，不改变游戏依赖或系统 Python 包。
- `generate-building-values.py` 用 Fraction 精确递归计算 175 种物品、162 条配方。直接采集物与黑雾专属掉落以 1 为基准；加工乘 2 并除以总产量，多路线取最小配方 ID 的普通路线；氢、光子、满蓄电器与副产物约定详见生成文档。71 项整数价格与材料/建筑计算表同次生成，`--check` 检查输入与所有输出一致；回收同价，科技手续费另计。
- 目录实际运行代码使用提取事实做接口替身检查：175 项全部分入 103 行情、71 兑换和独立沙土。六种 DarkFog 类型的物品原先不在 EItemType.Resource～Product 范围，现显式允许 5201～5206；五种归材料、能量碎片归矩阵。沙土虽然原型 StackSize 为正，实际不走背包库存，禁止列入行情。未选品种仍不创建新运行账户。
- 原版 TechProto.page 以 ID 分页，preTechArray/postTechArray 只取 PreTechs。效率一级与主分页交易入口跨页，将它改为 PreTechsImplicit 保留门槛、取消跨页线；节点 x=5+4n 与原生列对齐。交易和兑换 Level=MaxLevel=0 消除多余括号；升级 Level=MaxLevel=1～6，显示名统一“交易效率”。
- LDBTool.PostAddProto 内部同步调用 IdBind，按 Proto.Name 绑定和查重，不能用六个重复显示名直接注册。先使用独立 RegistrationName 绑定固定 ID，再将 Proto.Name 改为显示名；之后 Preload 与后续语言刷新使用统一显示名。
- 原版 UIItemTip 对负 recipeId 只有在 RecipeProto.Explicit=false 时才读取 Results[0] 对应的 ItemProto 与 DescFields。交付配方现为普通成品配方，Handcraft=false 不变；显式复制原版塔 DescFields/HpMax，显示耐久度和参数，保留独立制作格位与配方。
- 兑换格增加独立内背景，选择高亮优先，其次是已研究且背包持有，余者最暗。背包持有量随快照刷新，不在每帧先禁用按钮再启用。账户与持仓调整字体，左栏交易标题居中；本次没有制作新科技图标。
- 验证包含 160 项检查（14 核心、55 保证金、26 会话、18 背包、15 交付核心、16 原版交付适配、16 科技/定价/目录）以及完整价格复算。真实科技连线、制造参数和 UI 排版待 [0.14.0 游戏验收](acceptance-0.14.0.md)。

## 0.13.0 实现记录

- `TradeResearch` 同时链接至常驻原型模块和热重载模块，集中定义科技 ID、成本、前置与费率。交易接入 1951 和五级兑换 1952～1956 位于主科技页；六级效率 7801～7806 位于升级页。原版按 ID < 2000 / >= 2000 划分页，当前游戏科技 ID 上限 12000；注册时检查冲突，不静默覆盖。
- `TechnologyRegistration` 通过 LDBTool PostAddProto 添加 TechProto，位置设在两个原生分页现有节点下方。显式初始化所需空数组，Preload / Preload2 填充名称、图标、研究成本和前后置缓存。原生消耗按 HashNeeded × ItemPoints / 3600 计算；本版每种 ItemPoints=100，HashNeeded=成本×36，确保每种消耗恰为设计值。多色升级各色等量消耗，第六级仅白矩阵。
- 原型常驻模块还对 1604/1605 两个物流科技去重 UnlockRecipes 并重建 unlockRecipeArray。解锁缓存与制作条目可用性已修复，原生科技节点第四图标的排版仍需要游戏实测，没有扩展原版 UI 图标数组。
- 行星物流站没有经过 GalacticTransport.AddStationComponent，原生 planetId 通常为 0；星际站才登记此字段。交付 Matches 保留工厂、实体、stationId、原型和坐标校验，行星站接受 planetId=0 或匹配星球，星际站严格匹配登记星球。接口替身现在按原版行为创建行星站，并检查外国星球 ID 被拒绝。
- 配方调整为行星级消耗 4 座行星物流塔，星际级消耗 5 座星际物流塔，TimeSpend=3600（60 秒），仍仅制造台生产。新档 MarketBook 默认只开启建筑币；其它默认账户不推进报价、时间和 RNG，选择状态继续使用账本 v3。
- `ResearchProgression` 读取 GameHistoryData.TechUnlocked，交易命令与建筑双向兑换在执行时重新校验，界面禁用仅为辅助。费率更新应用至运行、冻结及后续新账户；冻结账户重建保存字节。最高升级允许精确零费率。
- MarginMarket 内层格式 v2 增加持仓实际 OpeningFee。研究期间不重算已付费用，平仓采用最新费率；费率变化使缓存爆仓价失效。各账户底层精度和金额两位显示规则不变。
- `TransferAmounts` 使用整数运算将 1000 步滑条映射到可用量，BigInteger 总量到单次 int 接口安全限幅。存取滑条与输入分别同步，执行时继续读取真实库存；最大输入长度为 10 位，允许 int.MaxValue。
- Debug / Release 均零警告、零错误；156 项检查通过（核心 14、保证金/多账户 55、会话 26、背包/兑换 18、交付核心 15、交付原版接口替身 16、研究与费率 12）。已备份并部署到 FXDSP Profile，交易与常驻模块 DLL/PDB 哈希及 ScriptEngine 式 Cecil 读写检查通过。实际科技树、第四图标和滑条排版待 [0.13.0 游戏验收](acceptance-0.13.0.md)。

## 0.12.0 交付实现记录

- 仅注册物品/配方 11702、11703 两种物流塔；11701 小型箱、交易平台交付管理面板和改造功能均删除。不处理旧交付存档迁移，按新档验收。
- `DeliveryRecipes` 是原型模块与核心测试共用的配方定义：两座对应原版塔生产一座交付塔，Assemble 类型、Handcraft=false、Explicit=true。原生建筑分页为 8 行 14 列，扫描空位放置两个独立条目；注册后显式 Recipe.Preload，初始化原生图标、介绍和索引。
- `DeliveryState` 的交付块为版本 2，绑定索引不写档。按物品建立槽位索引，由原版塔设置在每次读档/F6 后重建；普通物品全局唯一，翘曲器多槽例外。每个市场的存取根据物品寻找目标，不再使用全局 BoundToken。
- `UIStationStorage.OnItemPickerReturn` 在交付塔中改走校验后的原生 SetStationStorage，从而支持翘曲器例外；后者也拦截复制/蓝图等其它设置路径，Postfix 立即刷新绑定。重复设置使用原版 UIMessageBox 显示占用星球、塔编号和槽位。普通原版塔走原逻辑。
- 保留安全原版运输缓存与 BigInteger 扩展库存、真实数量再检查、物品/增产点数守恒。槽位仍有扩展库存时不允许更换；拆塔残量留存，重新配置对应交付物品后自动恢复，不再需要管理面板。
- 当前自检共 143 项：核心 14、保证金/多账户 55、会话 26、背包/兑换 18、交付核心/配方 15、交付原版边界适配 15。实际游戏制作 UI、制造台与物流待 [0.12.0 验收](acceptance-0.12.0.md)。

## 0.11.0 历史交付实现（已由 0.12.0 替换）

- `src/FXDSP.DeliveryBootstrap` 是正常 BepInEx 启动插件，依赖 [LDBTool 3.0.3](https://github.com/xiaoye97/DSP_LDBTool)，在原型加载阶段登记物品与配方 11701～11703，并复用小型储物仓、行星塔、星际塔的模型与图标。只浅克隆原型和描述对象，自有列表独立创建，不改原版模型描述。
- `Core/DeliveryState.cs` 保存唯一绑定、星球/实体/原型/坐标身份、每种物品的 BigInteger 扩展数量和增产点。`Runtime/DeliveryService.cs` 接入实体创建/移除、存取、缓存整理和拆除回收。物理工作缓存上限 1,000,000、目标 250,000，只作为原版整数运输接口的边界，扩展库存无玩法上限。
- 小型平台固定一个格子并使用原版 Filtered 类型。首次输入可确定品种；所有 AddItem/AddCargo 入口都检查物品，缓存暂空但扩展库存非空时仍拒绝第二品种。原版过滤设置可在取空后改品种；交易提取遵循已配置的过滤器。
- 物流塔保留本机原版槽位数及供需/无人机/运输船结构，不全局修改普通箱子或塔的堆叠。仓储读写使用原版 `factory.entityMutexs[entityId]`，物流站读写使用 `station.storage` 锁。普通箱子补丁先做字典快速检查，避免每次普通入货都查询 AppDomain 所有权。
- 绑定必须验证组件 id/entityId/planetId 与实体身份一致；拆除将扩展与剩余物理库存归并到可回收记录。实体 ID 回收或坐标不匹配不允许自动接替绑定。
- 快照外层 v2 在 MarketBook 后保存 DeliveryState，兼容 v1；旧 F6 对象数组仍为九项，市场字节后增加交付块，旧桥无该块时建立空交付状态。原版世界保存前可整理缓存，旁档 Export 内只序列化、不再搬动物资，避免双文件快照出现重复。
- 验证：136 项（14 + 55 + 26 + 18 + 13 + 10）通过；新适配测试直接编译原 DeliveryService 源文件，替换游戏边界，覆盖真实读取、库存守恒、过滤器、原版槽数、唯一绑定、F6 与保存边界。游戏程序集中的 Harmony 目标和私有字段名已经 Cecil 核对。Debug/Release 两模块均零警告、零错误；实际 Unity 建造、物流和 UI 待验收。
- 首次安装运行 `setup-delivery.ps1`、`build.ps1`、`deploy-delivery.ps1` 后重启。该安装脚本不关闭或启动游戏；后续普通逻辑可继续 F6。LDBTool 压缩包校验固定在准备脚本，原型模块不放入 scripts，以免 F6 重复注册。

## 当前边界

当前为 0.12.0 按物品自动绑定的两种交付物流塔，规则见本文开头。资源、矩阵启停和独立账户沿用。原型由启动模块 + LDBTool 注册，交易和工厂适配在 scripts/FXDSP 中；本次原型/配方变化需重启并以新档验证，之后普通逻辑可 F6。

用户已要求后续停止使用子智能体，此约定保存在根目录 AGENTS.md。普通代码/UI 更新优先支持游戏保持打开后 F6。

插件元数据集中在 `PluginInfo.cs`，程序集版本也引用同一组常量。项目已从 DSPFX 更名为 FXDSP；保留原插件 GUID `dspfx.mod`，兼容既有配置。项目名、程序集名、命名空间和脚本产物名称均使用 FXDSP。

## 目录与后续模块

```text
FXDSP.sln
src/FXDSP/
  Plugin.cs                   插件生命周期与模块装配
  PluginInfo.cs               GUID、名称、版本
  Configuration/ModSettings.cs
  Core/SpotMarket.cs           纯逻辑现货、报价、费用与二进制状态
  Core/MarketBook.cs           物品目录、独立账户容器与单账户兼容
  Core/MarginMarket.cs         物品本位保证金、强平阈值与旧状态迁移
  Core/BuildingExchangeRules.cs 固定比价与兑换范围规则
  Runtime/                    快照、原版背包适配及建筑兑换目录
  Persistence/MarketSession.cs 游戏快照绑定与 F6 交接
  Core/MarketCandles.cs        报价实体与独立确定性随机影线
  UI/                         原生 UGUI 窗口、滑条、K 线和结算弹窗
  Properties/AssemblyInfo.cs
scripts/
  setup-local.ps1             校验并配置本地依赖
  setup-sdk.ps1               准备项目本地的固定版本 SDK
  setup-modsave.ps1           准备 DSPModSave 1.2.2 编译依赖
  common.ps1                 脚本共用路径与校验
  build.ps1                  Debug / Release 构建
  deploy.ps1                 常规 / ScriptEngine 部署
  test.ps1                   核心、会话和背包适配器自检
docs/development.md
```

已接入 DSPModSave 1.2.2，当前未添加 CommonAPI、新物品或新建筑原型。各模块继续在单一 DLL 内保持边界，具体市场名单留待后续规划。

首版保持单一插件 DLL，便于构建和 ScriptEngine 迭代；行情与结算逻辑仍应通过代码边界与 Unity 分离，方便后续独立测试。

## 热重载约定

- 每个插件实例使用独立 Harmony owner，避免旧实例延迟销毁时撤销新实例的补丁。
- `OnDestroy` 撤销自身补丁、取消配置事件订阅。后续创建的 UI、协程、定时器、其他事件和任务也必须加入清理流程。
- 账户以 byte[] 和原生 GameData 标识在同一会话中交接，不把重载程序集的业务对象留在 AppDomain。owner token 防旧实例延迟清理覆盖新缓存或移除新存档处理器。
- 交易后和报价更新时立即交接，平时按 30 tick 刷新；保存通过 DSPModSave，并核对世界和保存 tick。未知账户会暂挂保存，损坏或不匹配账户锁交易并保留原 payload。
- ScriptEngine 模式不是完全卸载程序集。涉及静态状态、游戏对象和注册表的更改，需要单独判断能否重载。
- 新物品注册、初始化顺序、依赖和存档结构发生变化时，使用完整重启验证。
- 不自动扫描并修改玩家现有 Profile；部署脚本要求明确指定 BepInEx 目录。

## 验证策略

当前共 143 项检查。会话和适配测试使用最小游戏边界替身，不能代替 Unity 中的建造、物流、原版 UI 和兼容性验收；见 [0.12.0 验收说明](acceptance-0.12.0.md)。

## 建议里程碑

1. **框架验收**：游戏识别插件；诊断配置即时生效；验证 ScriptEngine 兼容性与重复重载。
2. **现货原型**：选一个示例物品，打通实体入库、隔离结算账户、买卖和提现；名单和参数待定。
3. **状态与科技**：原版科技前置、材料认证、交易记录、独立存档与异常恢复。
4. **工厂连接**：先绑定原版箱子验证交付，再评估专用箱子和物流塔原型；保留原版跨星物流的价值。

2026-10-25 的研究快照见 [研究索引](research/README.md) 和 [方案评估](design/proposal-2026-10-25.md)。当前已实现一个铜矿市场，其它研究建议尚未全部实现或固定。

用户已确认核心目标：禁止跨物品兑换，允许并鼓励同类物品交易获利和本金复投，收益可替代该物品产线。不得为了保护产线默认加入 NPC 库存、补货预算或净收益硬上限。验证应覆盖合法大额收益与增长，以及跨市场串账、复制、重复交付等真正的实现错误。

## 本地依赖

`build.local.json` 保存当前机器路径，已被 Git 忽略。`setup-local.ps1 -ReuseVeinProtectorReferences` 只引用相邻工程的本地程序集，不继承或修改其代码、配置或 Git 历史。升级游戏后，应同步更新本机引用并重新验证。

0.2.0 已将本机配置指向完整游戏 Managed 和 test2 的 BepInEx/core；旧精简引用未必含 InputLegacy。DSPModSave 构建引用放在 artifacts/dependencies，部署 Profile 也必须安装该库。

当前游戏依赖 `netstandard, Version=2.1.0.0`，因此采用 SDK 风格的 `netstandard2.1` 工程。SDK 固定为 10.0.402，仅作为构建工具，不改变游戏的 Unity / Mono 运行时。SDK 与缓存保存在本项目 `artifacts` 中。

## Git

项目使用独立 Git 仓库和 `main` 分支，与 VeinProtector 分开维护。忽略游戏 DLL、构建产物、符号、日志、本机路径、Profile 与存档。首次提交、远程地址和公开发布在需要时处理。

## 本机验证记录（2026-10-24）

- Debug / Release 编译均通过，0 警告、0 错误。
- 编译产物的插件 GUID、版本与 .NET Standard 2.1 目标元数据一致。
- 输出目录只包含本插件 DLL 和相关构建产物，没有复制游戏或加载器 DLL。
- 在本项目 `artifacts/deployment-checks` 临时目录验证了 Plugins / Scripts 两种部署、WhatIf 不写入、复制后 SHA256 校验、重复副本拒绝。
- 全部 PowerShell 脚本语法检查通过；本机配置、SDK 和构建产物由 Git 忽略。
- 当时以旧名 DSPFX 在 r2modman 的 FX 测试 Profile 安装官方 ScriptEngine r11.2，并将旧名 Debug DLL 和 PDB 部署到 `BepInEx/scripts`。官方安装包 SHA256 与发布校验值一致，安装文件逐一校验，BepInEx / Harmony / Cecil 引用已作静态解析检查。
- 用户已启动当时的 FX Profile 并按 F6 验证。旧名 DSPFX 成功加载 6 次、旧实例卸载 5 次、重载完成 6 次，未见 Error 或 Exception；旧名框架加载与重复热重载验收通过。此验证不包含后续玩法，也不代替改名后新 DLL 的游戏内验证。
- 日志有一条版本警告：ScriptEngine 编译目标 BepInEx 5.4.19，Profile 安装版本为 5.4.17；本次框架加载与热重载未出现相关失败。配置管理器中的即时配置切换尚待单独验证。
- ScriptEngine 保留默认设置，首次需要手动按 F6 加载。
- 本机安装路径和文件哈希保存在被 Git 忽略的 `artifacts/fx-profile-setup.json`。

## 项目更名记录（2026-10-25）

- 根目录、解决方案、工程、程序集、命名空间、插件显示名、构建和部署脚本已统一为 FXDSP；沿用现有 Git 仓库及 main 分支。
- Debug / Release 重新构建通过，0 警告、0 错误；程序集内部身份仍为 `dspfx.mod`。
- 部署脚本同时检测 `DSPFX.dll` 和 `FXDSP.dll`，避免同一 GUID 被旧新文件同时加载。
- 本次发现原 FX Profile 已不在磁盘，唯一旧名测试副本位于 test2。工程更名未自动修改 test2；后续部署需对该测试副本作明确迁移。

## 0.2.0 原型构建与部署（2026-10-25）

- 已实现铜矿现货闭环、原生 UGUI 窗口与图表、背包适配、DSPModSave 账户及 F6 交接。
- Debug / Release 均 0 警告、0 错误；核心 14、会话 17、背包适配器 9，共 40 组自检通过。
- 已针对实际游戏和 test2 依赖解析 195 个非框架 API 引用；Cecil Immediate + ReadSymbols 及 ScriptEngine 式程序集改写通过。
- 已备份 test2 的旧 DSPFX.dll / PDB 与 ScriptEngine 配置，部署 FXDSP.dll / PDB，安装官方 DSPModSave 1.2.2，并启用 ScriptEngine.LoadOnStart=true。逐文件哈希核对通过。
- 本机路径、哈希与备份位置见被 Git 忽略的 artifacts/prototype-deployment.json；旧框架验证记录继续保留供追溯。
- 尚未启动游戏验证新 UI、原版背包、实际存读档和 F6。下一步由用户按 [验收说明](acceptance-0.2.0.md) 进游戏测试；科技和专用交付建筑仍待后续实现。

## 0.3.0 修订

- 后续工作停止使用子智能体，根目录 AGENTS.md 已记录用户要求。
- 针对用户截图移除字体/按钮的发光材质与过亮高亮，增加深色背景；实际清晰度待下一轮截图验收。
- 新增铜余额、逐仓保证金、多空、杠杆、平仓、强平；旧现货资产和行情兼容迁移，含旧版 F6 字节交接检查。
- 57 组自检通过，Debug / Release 0 警告、0 错误。已将两个调试文件热部署到用户改名后的 FXDSP Profile，备份位置与哈希见 artifacts/prototype-deployment.json。
- 本次未改依赖、ScriptEngine 配置或游戏存档，用户可保持游戏打开，在部署完成后 F6 再 F8 验收。

## 0.4.0 修订

- 红涨绿跌 K 线、比例开仓、1～100×整数杠杆滑条、独立存入/提取输入、突出背包与可提取量，以及平仓/爆仓结算弹窗。
- 用户进一步明确 K 线只需美术展示；每 1.5 游戏秒跳价并新增一根，影线采用独立、确定性的随机数，不消耗行情 RNG、不参与结算。
- 增加旧 5 秒节奏转 1.5 秒的时间锚兼容，保留账户、持仓及行情，不追溯生成已经过去的价格。
- 共 67 组自检通过，Debug / Release 构建零警告、零错误。已备份并部署 DLL/PDB 到 FXDSP Profile，位置和哈希仍记录在 artifacts/prototype-deployment.json。
- 不新增依赖，本次仍可 F6 后 F8 验收；实际滑条、弹窗和 UI 显示待用户进游戏验证。

## 0.5.0 修订

- 背包/可提取数量改为独立数字区域，显式避免纵向裁切；移除刷新周期、配色解释和模拟说明，界面信息原则写入 AGENTS.md。
- 铜矿、塑料、电磁矩阵账户完全独立，三个市场同时推进；多市场结算弹窗排队展示并标注对应物品。
- 爆仓线按实际执行价、价差、费用取整、维持保证金规则求根，越出图表时显示边缘方向标记。
- 新 MarketBook 容器兼容旧单账户，原快照外层和 F6 框架类型缓存保留；未知/重复物品、损坏和截断数据拒绝后保留原资产或原始旁档。
- 77 组检查通过：核心 14、保证金/图表/多账户 32、会话 21、背包适配 10。Debug / Release 构建均零警告、零错误。
- 已备份并热部署到 FXDSP Profile，DLL/PDB 哈希与 ScriptEngine 式程序集改写检查通过。实际数量显示、市场切换与爆仓线仍待 [游戏验收](acceptance-0.5.0.md)。

## 0.6.0 修订

- 重排资源/矩阵/建筑币分类和左侧图标品种列表，完整覆盖六种矩阵。
- 新建筑币账户与固定比例兑换页支持可建造物、地基、三类物流运输单位和五类战斗单位。原版 `CanBuild` 字段、运输/地基常量从本机游戏程序集核对。
- 以内部 ID 99000001 管理建筑币，避免原型注册；不向背包添加此 ID。资金来源是回收物品，兑换计入入金/出金，不混入合约盈亏。
- 保证金预览移至比例滑条区域；杠杆增加 2/5/10/50×快捷按钮。普通平仓与爆仓各有独立视觉提示，并允许文本纵向显示以避免原生字体行高裁切。
- MarketBook v2 为九账户格式，兼容 v1 三账户及更早单账户，原快照外层和 AppDomain 字节缓存保持不变。
- 86 项检查通过（核心 14、保证金/多账户 35、会话 21、背包/兑换 16），Debug / Release 零警告、零错误。已备份热部署 DLL/PDB，校验哈希及 ScriptEngine 式改写通过；实际显示与原版物品目录待 [游戏验收](acceptance-0.6.0.md)。

## 0.7.0 修订

- 兑换按钮使用最终状态更新，移除每次刷新先禁用再启用的中间态；修正同类单市场平仓按钮状态切换。
- 双向兑换费率为 1%，按实际收发数量收费，原价保留入金/出金语义，费用记录到已有费用与已结算盈亏字段，不修改存档格式。针对极大费用累计增加先验容量检查，避免 decimal 舍入吞掉费用。
- 新增 HoldingsView 全持仓页、PositionHud 右上角快捷面板及持仓操作目标锁定；支持逐项和预检后的全平仓，隐藏模式可恢复并保存到配置。
- 累计盈亏等文案、DSP 标题、无黑框底色、左侧爆仓价、无建筑币物品图标均已更新。
- 93 项检查通过（核心 14、保证金/多账户 40、会话 21、背包/兑换 18），Debug / Release 零警告、零错误。已备份并部署 0.7.0 DLL/PDB，哈希和 ScriptEngine 式改写检查通过。实际布局、亮度、HUD 位置及交互待 [游戏验收](acceptance-0.7.0.md)。

## 0.7.1 修订

- HUD 标题改为“持仓”，名称换原版图标，入口改为“详情”；展开采用宽 248 的单行布局，紧凑宽 212，建筑币用“币”文字。
- 本次只变更 UI，行情与交易逻辑未改动。Debug / Release 构建零警告、零错误，已备份部署并检查 DLL/PDB 哈希及 ScriptEngine 式改写；没有为此视觉改动新增或重复运行核心测试。
- 运行独立 .NET 10 压力测量，覆盖 9/50/100/200/500 市场及 100 市场的不同持仓数；记录实际公式、缓存开销、历史分配与保存容量边界。见 [行情性能评估](market-performance-0.7.1.md)，结果不等于 Unity 游戏内 FPS。

## 0.8.0 修订

- 从本机 ItemProto 筛选原版资源/材料/零部件/产品，目录浏览不创建未选账户；左侧只列已启用资源并提供“选择资源”及分页。
- MarketBook v3 将历史账户与 RunningMarkets 分开。停用账户保持现金、行情和 RNG，导出直接复制冻结字节；恢复只重接时间锚，不追溯模拟。持仓禁停用由 UI、后端与导入共同检查。
- 原始选择随游戏快照和 F6 交接保存。兼容旧 v1/v2 及单账户，读入容量提高到有界 4 MiB，账户数上限 256。
- 新增持仓分页和 HUD 六项上限/更多入口，以支持可选资源数量增长；矩阵与建筑币维持原运行方式。
- 102 项检查通过（核心 14、保证金/多账户 47、会话 23、背包/兑换 18），Debug / Release 零警告、零错误。已备份并热部署 0.8.0，DLL/PDB 哈希与 ScriptEngine 式改写检查通过；实际资源目录、图标和分页待 [游戏验收](acceptance-0.8.0.md)。

## 0.9.0 修订

- HUD 去除展开状态和方向杠杆，默认宽 128、行距 26，只以图标、两位浮动盈亏和平仓显示每行。隐藏/恢复和详情保留；平仓标签缩小并禁止换行，修复截图中的文字竖排与点击区域不一致。
- 金额显示两位，报价/入场/爆仓价四位，数量类控件整数；计算与保存精度没有更改。
- 顶层分类改为原料、材料、武器、消耗品、矩阵、建筑币。运行时依据本机原型元数据判定，空分类仍可打开独立选择页；余额、持仓、启用状态和 MarketBook v3 不变。
- 104 项检查通过（核心 14、保证金/多账户 49、会话 23、背包/兑换 18），Debug / Release 零警告、零错误。已备份热部署 0.9.0，DLL/PDB 哈希与 ScriptEngine 式改写检查通过；实际紧凑排版、点击和分类目录待 [游戏验收](acceptance-0.9.0.md)。

## 0.10.0 修订

- 资源目录包含六种矩阵，选择页和已启用列表统一按分类筛选；切换分类保留选择模式。MarketBook v3 的启用标记现允许空仓矩阵停用，建筑币仍拒绝停用，旧账户默认状态不变。
- 水、氢、重氢、硫酸、有机晶体与增产剂使用 ID 分类覆盖，避免原型弹药标记误判。分类不写入账本。
- `TradingLogDisplay` 只整理展示文本，旧记录不重写；开仓日志简写，最多显示六行。结算遮罩独立覆盖窗口父容器，点击卡片内外均冒泡至关闭处理，隐藏 HUD 避免弹窗期间点击穿透。
- HUD 与主平台同时显示，并维持正确层级；HUD 使用独立平仓指令，仅将该次手动结算标为已展示，不更改其他市场的爆仓通知。
- 图表四个刻度与水平网格对应，爆仓标签宽度根据字体首选宽度更新，展示不参与成交和风险计算。
- 建筑目录调用原版 `ItemProto.FindPreTech()` 获取解锁科技，`TechnologyTier` 遍历显式/隐式前置，按最高矩阵确定五级兑换目录。此规则不看建筑配方原料，初始建筑一级，宇宙矩阵要求并入第五级，不添加科技解锁限制。
- 111 项检查通过（核心 14、保证金/多账户 55、会话 24、背包/兑换 18），含矩阵停用、恢复、存档和 F6 交接、旧日志展示及科技层级。Debug / Release 均零警告、零错误。
- 已备份并部署 FXDSP Profile 的 scripts DLL/PDB，校验哈希、BepInPlugin 版本和 ScriptEngine 式 Cecil 读写均通过；备份路径见忽略文件 `artifacts/prototype-deployment.json`。尚待 [实际游戏验收](acceptance-0.10.0.md)，未宣称已验证 Unity 排版。
