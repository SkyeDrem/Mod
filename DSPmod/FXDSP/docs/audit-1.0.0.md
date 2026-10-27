# FXDSP 1.0.0 发布检查

日期：2026-10-28。依据用户要求执行最后一次完整检查并生成本地正式发布包；未上传 Thunderstore 或 GitHub。

## 发布内容

- 核心插件、交付原型插件的注册版本、程序集版本与 manifest 统一为 1.0.0。
- 玩家 README 保留用户最新中文修改及对应英文 B 站反馈说明。
- CHANGELOG 仅保留 1.0.0 中英文正式发布说明，删除旧开发版本记录。
- 本轮未改变交易、行情、费率、交付库存、成就判定或保存格式。

## 自动检查

| 范围 | 通过数 |
| --- | ---: |
| 核心行情与边界 | 14 |
| 保证金、杠杆、强平、账户隔离、兑换和市场选择 | 55 |
| 成就判定、通知及进度 | 29 |
| 异常检测兼容开关及补丁范围 | 5 |
| 保存、世界匹配、锁定保护及 F6 交接 | 29 |
| 背包、沙土、兑换数量与余额结算 | 23 |
| 交付库存、绑定和容量 | 15 |
| 交付运行时、原版物流、拆除与恢复 | 23 |
| 科技、费率和原型归属 | 27 |
| 中英文文本、收益率和持有时间 | 18 |
| 合计 | **238** |

完整检查发现两处测试夹具仍依赖原先 90 tick 的行情周期：报价边界 F6 交接、持有两根 K 线的快照。现改为根据 `MarginMarket.QuoteIntervalTicks` 计算时间；报价边界案例还验证实际生成了新报价，避免空测。当前行情周期仍为原有的 60 tick / 1 游戏秒，未修改生产逻辑来迁就测试。

两个 Release 构建均为 0 警告、0 错误。程序集核验解析核心 329、交付模块 118 个运行时接口引用，总计 447；核对核心 11、交付模块 3 个 Harmony 目标，总计 14。英文资源、科技图集内嵌内容与源文件哈希一致，程序集重写检查通过，未引入 ScriptEngine、ConfigurationManager 或 dnSpy 运行依赖。

原版科技前置图标标签中英文各 24 项、实际编译图标导入器、原版介绍文本中英文各 12 个颜色标记检查通过。175 种物品、162 条配方、71 项兑换价及两份离线方案对照均通过只读复算。

## 网站与 ZIP 检查

依据 [Thunderstore 基本包规范](https://wiki.thunderstore.io/mods/creating-a-package) 和 [BepInEx 安装目录规则](https://wiki.thunderstore.io/mods/packaging-your-mods)：

- ZIP 根目录有 UTF-8 `manifest.json`、`README.md`、`CHANGELOG.md` 和 256×256 PNG `icon.png`；没有额外包裹目录。
- manifest 名称为 FXDSP，版本为 1.0.0，描述 241 字符，未超出 250 字符限制。
- 三个声明版本均通过 Thunderstore 公开 API 确认可用：xiaoye97-BepInEx-5.4.17、CommonAPI-DSPModSave-1.2.2、xiaoye97-LDBTool-3.0.3。
- 14 张 README 中英文截图均对应本地文件，远程图片地址全部返回 HTTP 200。
- DLL 位于 `plugins/FXDSP/`。包内仅六个白名单文件；不含游戏/第三方依赖 DLL、调试工具、个人配置、存档或本地 SDK。
- ZIP 条目数、路径大小写、CRC、逐文件内容和最终 SHA256 均已核对；包内 README 与最新发布源完全一致。

## 保留的兼容边界

- 固定物品/配方 ID 11702、11703；科技 ID 1951、7701～7705、7801～7806。占用相同 ID 的 Mod 无法共存。
- 建筑币兑换是原版固定目录，不自动给其它 Mod 建筑定价；未实现多人账户与库存同步。
- 本地异常检测兼容开关默认开启，其全局影响和关闭方法已在中英文 README 明确。此前用户已反馈该功能游戏内测试没有问题。
- 本轮未启动或重启游戏，自动检查不代表所有 Mod 组合、游戏内 UI/物流或银河系服务器上传均经实机验证。

## 成品和证据

成品：`dist/FXDSP-1.0.0.zip`，同目录附 SHA256 和逐文件清单。

主要日志：`artifacts/release-1.0.0-tests.log`、`release-1.0.0-build.log`、`release-1.0.0-assemblies.log`、`release-1.0.0-native.log`、`release-1.0.0-pricing.log`、`release-1.0.0-readme-links.json`、`release-1.0.0-package.log`。
