# FXDSP 原版风格 UI 研究

研究日期：2026-10-25。结论来自作者仓库的关键实现代码及当前本地参考程序集检查；尚未在游戏里创建或验证新窗口。

## 结论

FXDSP 适合采用原生 UGUI：复用游戏已有窗口外壳、字体、物品图标、按钮与滚动列表，再编写自己的交易面板与图表。应优先建立一层小型控件工厂和窗口生命周期适配，不依赖整个 UXAssist 或 LSTM 插件，也不把最终交易界面做成独立风格的 IMGUI 窗口。

以下是实际阅读后可继续定位的案例。来源均固定到提交，文件清单和缓存 SHA256 见 [sources-ui.json](sources-ui.json)。

## 案例一：原版窗口外壳和生命周期

UXAssist 的 `MyWindow.InitBaseObject/Create` 克隆原版分拣器窗口，仅保留背景和阴影，替换原窗口控制组件，再绑定关闭按钮与标题。其 `MyWindowManager` 将自建窗口接入创建、初始化、更新、关闭与销毁流程；这比仅显示一个 GameObject 更接近原版行为。[源码：MyWindow.cs](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/MyWindow.cs#L26-L92)

可采用：建立 FXDSP 自己的 `NativeWindowFactory` 和 `MarketWindow`，复用窗口外观，单独管理所创建对象。不要直接复制原 Mod 的整个全局窗口管理器。特别是其暂停时跳过窗口更新的策略，未必符合交易窗口需求；应让 UI 交互和市场逻辑时钟分离。[生命周期参考](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/MyWindow.cs#L526-L650)

热重载时必须销毁模板副本、所有 FXDSP 窗口、图表资源，并解绑回调；跨存档时不能继续持有旧 `GameData`、物流站或仓库对象引用。

## 案例二：按钮、文字、本地化与布局

UXAssist 的 `LayoutHelper` 从原版制造台状态文字和性能面板按钮创建控件，沿用字体、材质与交互组件，更新 `Localizer` 和文本。`MyFlatButton` 则复用戴森球编辑器的扁平按钮。`Util` 集中处理锚点、坐标、按钮状态复位及文字尺寸刷新。[LayoutHelper.cs](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/LayoutHelper.cs#L19-L88)、[MyFlatButton.cs](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/MyFlatButton.cs#L16-L86)、[Util.cs](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/Util.cs#L9-L100)

可采用：把模板获取、文字、按钮、提示和标准间距封装为自己的工厂。模板不存在时记录明确错误，停止创建该窗口，不随意退回可能修改原版窗口的路径。中文和英文均使用稳定翻译键；复制控件后必须重新设置对应 `Localizer`，避免原版文本在刷新时覆盖交易文案。

细节：`RemoveAllListeners()` 只移除运行时注册的监听，不会清除 Inspector 序列化的持久监听。对 FXDSP 自己克隆且完全接管的按钮，可重建其 UnityEvent 并检查 `UIButton` 自有回调；不得对原版共享实例这样操作。[Unity 官方语义](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Events.UnityEventBase.RemoveAllListeners.html)

## 案例三：物品和交付端点的原生列表行

LSTM 的 `UIBalanceListEntry.CreatePreｆab` 克隆物流站的 `UIStationStorage`，保留图标、数量栏、进度条和配色，移除原组件并改作自己的行组件。`SetUpValues` 通过物品原型取得原版图标。[条目构造](https://github.com/hetima/DSP_LSTM/blob/06c7de35bd7d7a54e1eedc04534f71394f87ed34/LSTM/UIBalanceListEntry.cs#L113-L175)、[物品显示](https://github.com/hetima/DSP_LSTM/blob/06c7de35bd7d7a54e1eedc04534f71394f87ed34/LSTM/UIBalanceListEntry.cs#L309-L357)

可采用：市场列表和交付目的地列表沿用原版物品图标、物品提示、容量条及站点定位按钮。UI 持有端点 ID 和只读快照；实际数量修改由独立交付服务完成。不要让复制的原版储物控件保留取物、分拣、筛选等业务回调，否则界面点击可能误操作原版状态。

## 案例四：列表创建、事件与刷新性能

LSTM 的 `UIBalanceWindow` 复用教程窗口的 `UIListView`，换成自定义条目资源；成对注册/解绑按钮和滚动事件。列表增量添加，显示刷新按帧错峰，并区分可见条目与新进入视野的条目。[事件与刷新](https://github.com/hetima/DSP_LSTM/blob/06c7de35bd7d7a54e1eedc04534f71394f87ed34/LSTM/UIBalanceWindow.cs#L172-L270)、[列表实现](https://github.com/hetima/DSP_LSTM/blob/06c7de35bd7d7a54e1eedc04534f71394f87ed34/LSTM/UIBalanceWindow.cs#L742-L804)、[列表模板](https://github.com/hetima/DSP_LSTM/blob/06c7de35bd7d7a54e1eedc04534f71394f87ed34/LSTM/UIBalanceWindow.cs#L895-L958)

可采用：只刷新已变化的数据和当前可见行；关闭窗口后停止图表和列表绘制。少量市场无需一开始实现复杂虚拟化。LSTM 的每侧每帧增加 5 行、30 帧错峰刷新是该项目的取舍，FXDSP 不应照搬数值，更不能用渲染帧数决定行情或交付速度；应以逻辑时间推进业务，再通知 UI。

## 输入与关闭规则

`MyConfigWindow._OnUpdate` 在玩家未输入文字时消费 ESC；LSTM 进一步优先关闭内部菜单/配置窗，再关闭主窗，防止一次按键触发多层行为。[UXAssist 输入案例](https://github.com/soarqin/DSP_Mods/blob/473c3c8fecb45ccb4df4cf158f8bcae3be007d58/UXAssist/UI/MyConfigWindow.cs#L44-L57)

FXDSP 建议：输入数量时屏蔽交易快捷键和建造热键；ESC 优先取消输入或关闭确认框，再关闭市场窗；鼠标滚轮只有在图表区域才缩放图表。使用原版 UI 更新后的输入状态，不仅检查 `Input.GetKeyDown`。提交订单需由业务服务再校验，不能依赖按钮是否灰显。

## FXDSP 界面草案（新设计，非原 Mod 已实现功能）

- 顶部：FXDSP 名称、市场状态与关闭按钮。
- 左侧：少量已解锁物品；显示物品图标、名称、报价变化，未解锁项说明条件。
- 中间：该物品价格曲线、时间范围、可成交报价和深度/剩余额度。
- 右侧：该物品独立的结算余额、托管实物、买入/卖出数量、费用与预计到账。首版不用通用净资产数字掩盖物品隔离。
- 下方页签：成交记录、交付队列、市场解锁。

图表可使用原生 UGUI 的自定义 `Graphic` 网格，沿用原版颜色和字体；先做折线图，不引入浏览器或大型金融图表框架。仅在行情快照变化、缩放或尺寸改变时重绘。[Unity Graphic API](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Graphic.html)

窗口应挂接到原版 UI 层级并沿用其缩放。不得在不知道原版策略时自行新建并改写全局 CanvasScaler；按分辨率、宽高比和游戏 UI 缩放分别验收。[CanvasScaler 语义](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/script-CanvasScaler.html)

## 已验证边界与下一次实测

通过 Mono.Cecil 检查当前本地 `Assembly-CSharp.dll`，16 个关键类型/成员均存在，包括窗口模板入口、`UIStationStorage` 图标和数量字段、`ManualBehaviour` 生命周期、`VFInput.UseEscape`、`UIListView` 入口。参考程序集 SHA256：`C43A484F6ADF8A9E4B956156047070891B46860D5B5C707BA1377B6A2AF25732`。

这仅确认代码接口存在，不证明层级路径、模板激活状态、拖动缩放或输入拦截已在当前版本工作。新窗口第一次接入时需要验证：入档后可打开/关闭；ESC 和输入不穿透；不同 UI 缩放不越界；切档和重复 F6 后无重叠窗口/回调；暂停时不产生隐性成交；大存档下关闭窗口不继续扫描全星系。

## 来源保存与使用

作者原仓库固定版本：UXAssist/DSP_Mods `473c3c8fecb45ccb4df4cf158f8bcae3be007d58`（MIT），LSTM `06c7de35bd7d7a54e1eedc04534f71394f87ed34`（Unlicense）。已在 `artifacts/references/ui` 保存 10 个选定源码文件、两份许可证和下载校验清单，没有复制整个仓库。

这些缓存是研究资料，不参与编译，也不作为 FXDSP 自有代码发布。本文件主要是原创分析。若以后实际移植代码，应保留对应版权/许可声明并在项目致谢中记录；当前建议学习模式后编写小型适配层。
