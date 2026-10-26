# FXDSP：科技、交付与存档代码研究

研究日期：2026-10-25。以下区分“已读代码事实”和“FXDSP 设计建议”。研究对象为作者仓库固定提交，不表示这些 Mod 已在当前 DSP / Unity 2022 环境验证兼容。本文为原创摘要；精选原文件仅保存在被 Git 忽略的 `artifacts/references/progression`，不编译进 FXDSP。

## 结论

1. **铜矿或塑料可以作为原生科技的研究材料候选，不能把原版科技系统理解成只能消耗矩阵。**已有库明确填写任意物品 ID，并区分实验室科技和非实验室科技。准确消耗 100 个仍需要配置研究点比例和游戏内验收。
2. 建议把原版科技与市场经营资格分开：科技决定“有没有能力生产/建造”，经营资格决定“是否开放该物品市场、额度和交付能力”。盈利奖励可以加速经营资格，不能绕过本体关键科技。
3. 交付先绑定原版箱/塔，验证后再克隆原型做专用建筑。直接替换底层仓储池、建立跨星共享仓的维护成本明显更高。
4. 接入 DSPModSave 适合保存账户和绑定关系，但旁档不是跨文件数据库事务。必须自行处理版本、缺档、旧档、切档、热重载注册和快照不一致。

## 已读案例与可复用机制

### 案例 A：CommonAPI 原生科技注册

- 仓库：`limoka/CommonAPI`，提交 `4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a`。
- 文件与入口：[ProtoRegistry.RegisterTech](https://github.com/limoka/CommonAPI/blob/4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a/CommonAPI/Systems/ProtoRegistrySystem/ProtoRegistry.cs#L1073)。
- 已读事实：方法把研究物品写入 `TechProto.Items`，研究点速率写入 `ItemPoints`，总研究点写入 `HashNeeded`；是否为实验室科技由研究物品是否包含 `LabComponent.matrixIds` 判定，并调用 `LDBTool.PreAddProto` 注册。
- 同一方法设置 `PreTechs`、`UnlockRecipes`、位置与说明等，说明新增交易科技可进入原生科技树，而不必另做一套仿科技树 UI。
- 代码注释给出总物品量关系 `N = HashNeeded × ItemPoints / 3600`。`ItemPoints = 100` 不能直接理解为“总消耗 100 个”。例如研究点与速率的组合可以设计成总量 100，最终以当前游戏的显示与实际扣除为准。
- FXDSP 应用：用非实验室材料科技表达“交易网络接入”“交付箱”“交付物流站”，普通市场认证也可选择此路径。科技 ID、图标、树坐标和前置关系要单独规划，不能沿用示例 Mod 的固定 ID。

### 案例 B：GenesisBook 的非矩阵研究材料与量子仓储

- 仓库：`Awbugl/ProjectGenesis`，提交 `8d6b8abb786323ca0e362f9132da230b968227a9`。
- 数据：[data/techs.json](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/data/techs.json)。已读科技 1001 的 `Items=[1202]`、`ItemPoints=[30]`、`HashNeeded=1200`、`IsLabTech=false`，是非矩阵材料科技的明确实例；1002 等也使用多种非矩阵材料。
- 当前本地 `Assembly-CSharp.dll` 的静态字段检查同样发现 `TechProto.Items`、`ItemPoints`、`HashNeeded`、`IsLabTech`、`PreTechs` 和 `UnlockRecipes`，并确认 `kPointPerItem=3600`。只确认结构与常量存在，未验证新科技运行行为。
- 仓储代码：[QuantumStorage/InternalFunctions.cs](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Patches/Hooks/QuantumStorage/InternalFunctions.cs)、[QuantumStoragePatches.cs](https://github.com/Awbugl/ProjectGenesis/blob/8d6b8abb786323ca0e362f9132da230b968227a9/src/Patches/Hooks/QuantumStorage/QuantumStoragePatches.cs)。
- 已读事实：其绑定保存星球、仓储 ID 和通道信息；新增和移除仓库时干预 `FactoryStorage.storagePool`，并自行导入导出共享 `StorageComponent` 与映射。多种拿取、清空和插入路径也需要补丁。
- 可复用的是“绑定关系必须随建筑生命周期维护”以及“共享状态和映射一并保存”的原则。首版不采用共享仓替换方案：它触及分拣器、建拆、存储池复用和跨星行为，超出了 FXDSP 自动交付的最小需求。

### 案例 C：MoreMegaStructures 的专用物资交换物流站

- 仓库：`jinxOAO/DSPmod_MoreMegaStructures`，提交 `429d8f0e61c9a1eb9f416269113c89580931db81`。
- 原型代码：[MMSProtos.cs](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/429d8f0e61c9a1eb9f416269113c89580931db81/MoreMegaStructure/MoreMegaStructure/MMSProtos.cs#L1372)。它复制原版物流站物品与 `prefabDesc`，建立专用原型、配方和建造栏入口。
- 交付代码：[StarAssembly.TakeItemForFactory / SendProductToGround](https://github.com/jinxOAO/DSPmod_MoreMegaStructures/blob/429d8f0e61c9a1eb9f416269113c89580931db81/MoreMegaStructure/MoreMegaStructure/StarAssembly.cs#L1245)。它识别专用交换站，按匹配槽位和剩余容量转移货物；未送完的数量保留在内部产品缓存，并处理 `count` 与增产点数 `inc`。
- FXDSP 应用：专用交付站可以复用原版物流系统的数据结构、槽位、运输与端口，新增市场交付行为；后续仍应为本项目独立实现适配层。案例中的全星球遍历、固定五槽、固定原型 ID 和部分增产处理不能照抄成通用方案。
- 建议维护已绑定站点索引并分批处理，而不是每个市场每帧扫描全星系物流站。

### 案例 D：DSPModSave 旁档与热重载注册

- 仓库：`limoka/DSP-Mods`，提交 `f7a0c964ccfc72a96a5d96f56938b1f71aab3c19`。
- 接口：[IModCanSave.cs](https://github.com/limoka/DSP-Mods/blob/f7a0c964ccfc72a96a5d96f56938b1f71aab3c19/Mods/DSPModSave/src/DSPModSave/IModCanSave.cs)。提供 `Export`、`Import`、`IntoOtherSave`。
- 实现：[DSPModSavePlugin.cs](https://github.com/limoka/DSP-Mods/blob/f7a0c964ccfc72a96a5d96f56938b1f71aab3c19/Mods/DSPModSave/src/DSPModSave/DSPModSavePlugin.cs)、[GameSave_Patch.cs](https://github.com/limoka/DSP-Mods/blob/f7a0c964ccfc72a96a5d96f56938b1f71aab3c19/Mods/DSPModSave/src/DSPModSave/Patches/GameSave_Patch.cs)。
- 已读事实：保存入口在原版保存成功后处理旁档；`.moddsv` 以插件 GUID 定位数据块，调用各插件自己的读写逻辑；新游戏/没有该插件数据时调用初始化接口。实现还有 `AddModSaveManually` 和 `RemoveModSaveManually`，可供动态生命周期集成。
- FXDSP 应用：账户、委托、成交流水、市场认证、交付绑定和待交付数量一起保存。导入时先读到临时状态，校验完整后再替换运行状态；不要在读到一半时修改现有账户。
- 改名后的插件 GUID 必须稳定。ScriptEngine 重新创建实例时，需明确移交状态、解绑旧保存处理器和注册新实例，不能只靠静态字段留下资金。

### 补充：CommonAPI 自定义组件生命周期

[FactoryComponent.cs](https://github.com/limoka/CommonAPI/blob/4a5d7c21c0e2a5f91b639e4d710fa5bb5dcb928a/CommonAPI/Systems/ComponentSystem/FactoryComponent.cs) 展示 `OnAdded`、`OnRemoved`、`InternalUpdate`、`Import`、`Export` 等入口。专用交付建筑将来可以接入该体系；验证原版箱绑定时不必先引入整个自定义组件框架。依赖启用哪些子模块，应以实际需要为准。

## 科技与市场解锁建议

### 两层门槛

| 层 | 作用 | 推荐保存/判断位置 |
|---|---|---|
| 原生科技与配方门槛 | 是否已到达塑料、星际物流等本体阶段 | `GameHistoryData` 等当前游戏原生状态，通过适配层查询 |
| 市场认证 | 是否提交材料、达到经营指标，开放何种额度 | FXDSP 的版本化旁档 |

“100 铜矿解锁系统及铜矿市场”“100 塑料解锁塑料市场”都可表达：可选择原生材料科技，也可在交易面板设一次性认证交付。若要尽量贴合本体，优先试验原生材料科技；如果以后要经常改经营条件，独立认证更灵活。初期不同时为同一门槛维护两份互相独立的解锁真值。

建议条件为：`本体生产门槛满足 AND 市场认证满足`。开启新物品市场不赠送免费结算额度；玩家仍须有该物品的种子库存。具体首次取得与认证门槛待定，不要求玩家此后持续维持该产线。

用户已确认同类交易收益可替代产线。科技和交付的扩展用于提供能力与操作选择，不默认以 NPC 库存、补货或净收益配额限制玩家合法获利。

盈利提前解锁适合作为以下任一种奖励：预先显示下一个市场、降低认证材料费、提前开放小额度，或解锁下一个已达到本体生产阶段的市场。不要直接设置为“铜市场赚钱即可无视塑料科技并获得塑料”。

经营指标应只计扣除费用后的已实现盈亏，不把充值、提现、材料认证或浮盈算盈利。不同物品市场的币值不可直接相加比较；使用各自市场的指标，或明确规定归一化方法。首版无需为这一机制添加复杂的隐藏财务分数。

## 三种交付建筑路线

| 路线 | 优点 | 成本与适用阶段 |
|---|---|---|
| 绑定玩家现有箱/塔 | 不新增原型，先验证对流水线的实际价值 | 仍需绑定 UI、拆除校验、槽位与容量处理；推荐首个交付原型 |
| 克隆箱/塔原型形成专用终端 | 可建造、可科技解锁、容易让玩家理解 | 新增物品/配方 ID、图标、建造栏、原型初始化与兼容检查；推荐正式形态 |
| 全新独立建筑/共享跨星仓 | 最大设计自由 | 模型、端口、组件、建造/蓝图、存档与运行更新全要维护；首版不采用 |

建议先做“交付箱”，再做“交付物流站”。箱子可直接由分拣器接入生产线；塔在物流科技以后接入无人机/运输船。售后增加专用模型不必与市场核心同时开展。

## 交付必须满足的约束

1. **身份检查。**绑定至少记录星球、实体、组件/槽位、预期原型和本 Mod 生成的绑定标识。每次访问检查实体是否仍存在、组件是否仍属于该实体；数字 ID 会被回收复用，不能只保存一个 storageId/stationId。
2. **按实际插入量扣账。**在游戏允许的逻辑线程/同步点处理，先检查目标、物品槽、容量、过滤和配额，用当前版本合适的插入 API 接收数量，再按真正插入量减少待交付数量。同一逻辑事务中不允许保存或其它修改穿插。首版不要在后台任务中写 Unity/工厂数组。
3. **箱满与部分交付。**未接收的部分留在市场待交付库存，不落地消失、不退款后继续保留原物资、不阻塞其它目的地。幂等请求 ID 和交付游标防止重试同一批订单多次发货。
4. **拆除与改槽。**解绑后保留未交付货物；不把已进入原版仓储的货物再返还到账户。更换物品槽时暂停不匹配的请求，不静默覆盖槽位。
5. **增产点数。**新购货默认 `inc=0` 最清楚；如允许保留喷涂货物，账本必须同时记数量与增产点数，部分转移按明确规则守恒。不要从已喷涂物品充值后免费提现生成额外喷涂物，亦不要在原型阶段自动赠送最高增产。
6. **跨星球。**全局账户任意地点提货会绕过运输船与翘曲器，即使各物品币互不兑换也如此。初期交付限定绑定星球，后续跨星球由原版物流完成；若将远程交付作为玩法，必须单独规划科技、能耗、吞吐和运输成本。
7. **并发、统计与回收。**使用当前游戏容量规则和写入同步方式；不要只仿旧源码直接改 `count`。市场进货/提货应与原版生产统计的含义区分，避免把转移算制造并重复触发奖励。
8. **存档一致性。**同一快照保存余额、物品库存、已实现盈亏、已处理请求和交付游标。原版存档和旁档是两个文件，订单幂等不能单独阻止混用不同快照导致重复发货；需要匹配快照信息、备份和缺档/不匹配时停止自动交付的恢复策略。
9. **热重载。**UI、事件和补丁可重建，账本与已交付游标不能被重置。热重载时暂停交付、移交状态后恢复；验证“重复 F6 后每笔物资仍只交付一次”。新增原型和科技注册一般安排完整重启验证。

## 推荐实现顺序与验收

1. 注册一个不影响本体配方的材料科技，验证树位置、中文、前置关系、精确消耗、旧档、新档与重启加载。
2. 市场先支持仓库内提货/放回背包，建立版本化持久化；用数量守恒测试建立交易与交付边界。
3. 加入一个原版储物箱绑定，验证箱满、半满、过滤、拆箱、重建、读档和重复 F6。
4. 验证完成后克隆为专用交付箱，挂接原生科技与建造配方。
5. 再接原版物流站、跨星运输、科技升级；不与第一版市场模拟同时实现。

优先自动测试：净入库/出库守恒、部分接收、同请求重试、拆除后 ID 复用、`inc` 守恒、异常导入不覆盖现有状态、原版/旁档不匹配时停发。UI 和实际物流连接另做游戏内验收。

## 许可与保存方式

- CommonAPI：仓库 GPL-3.0；研究摘要不复制其实现。若以后直接复用代码或发布链接依赖，需要按具体使用方式检查许可。
- ProjectGenesis：固定提交的 LICENSE 明确源码和数据为 GPL-3.0-only，美术资源另见 `assets/LICENSE.assets`。本文没有复制其图标或模型。
- DSPModSave：子目录 LICENSE 为 Apache-2.0；不能因为上层 DSP-Mods 仓库显示 MIT 就覆盖模块自己的许可。
- MoreMegaStructures：本次元数据和所读目录未检出明确许可证。保留阅读链接和原创分析，不据此认为可复制发布其源码或素材。

完整来源清单见 `sources-progression.json`。本地原文件用于私有学习和版本对照，不作为可自由再分发素材库；正式实现时优先根据原生 API 和规则独立编写适配层。
