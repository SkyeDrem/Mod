# 本地异常检测兼容验收

2026-10-27 用户反馈“测试没有问题”，按正常版本迭代为 0.20.3 并打包。本次仅本地修改与打包。

## 修改前备份

`backups/20261027-210241-before-abnormality-bypass/`（相对于 FXDSP 项目根目录）。原备份已迁入此处，文件内容与 SHA256 保持不变。

- `FXDSP-source-0.20.2.zip`：修改前的 187 个项目源码、文档、图片与元数据文件。
- `FXDSP-0.20.2-before-abnormality.zip`：修改前的原始发布包。
- `backup.json`：原提交 c79acf0c7eb1a3efb6903aff5c793433ad2619ae 及两份 ZIP 的 SHA256。

回退游戏安装时，用备份发布包中的两个自有 DLL 替换当前版本并重启。

## 实现范围

核心 FXDSP.dll 新增三项 Harmony Prefix，不增加依赖：

1. `ABN.GameAbnormalityData_0925.NothingAbnormal()`：开关开启时返回 true，兼容已存在异常标记的存档。
2. `TriggerAbnormality(int,int,long[])`：开关开启时跳过，阻止新异常证据写入。
3. `AbnormalityLogic.GameTick(long)`：开关开启时跳过，避免继续运行不需要的周期性检测。

配置 `BepInEx/config/dspfx.mod.cfg`：

```ini
[Compatibility]
BypassAbnormalityChecks = true
```

这是全局本地异常检测覆盖，不限于 FXDSP 引起的原型差异。旧异常证据仍在原存档中，未调用 ClearAbnormality、改写签名或修改保存格式。关闭开关后重新启用检测，原有异常标记会再次生效。

原生 `AchievementLogic.active` 仍由原版计算：异常状态查询之外，achievementEnable、本人正式游戏、非沙盒条件全部保留。未直接强制成就 active，也未修改元数据限制、账号归属或上传报文。

补丁跟随核心模块实例的 Harmony owner 注册和释放，启用条件同时验证当前 AppDomain owner。兼容 F6 更新；常规插件安装应替换 DLL 后完整重启。开关改变即时生效。

## 本轮检查

- 5 项针对生产 Prefix 的独立检查：无实例、关闭开关、启用、即时恢复、目标范围。
- Release 双模块构建与原版 API/补丁目标验证。
- 发布包清单及字节校验。

上述自动检查不等同于 Unity 游戏内补丁执行或服务器上传验收。

## 游戏内验收

1. 安装发布包，启动日志应出现 `Local abnormality bypass: True`；无新增 Harmony 错误。
2. 读取原来异常的普通存档，检查原生成就界面是否恢复。原始异常详情可能仍保存，不以旧证据是否消失作为成功条件。
3. 存档、退出、读档后重复检查，再尝试银河系上传并观察游戏反馈。
4. 测试普通新档与正常交易、兑换、交付平台；FXDSP 内置成就继续正常。
5. 将开关设为 false 并重启，确认原生异常状态恢复；沙盒档仍按原版条件禁用原生成就。

用户已反馈测试没有问题，具体测试反馈以本次对话为准。本次不自动读取、改写或备份用户存档。
