# 交易科技图标

`technology-icons.png` 是已批准的 0.16 图标预览经 imagegen 内置工具提取的 RGBA 透明图集（1448×1086）。用户已授权接入游戏，0.17 起作为常驻模块的嵌入资源发布，不要求玩家另外复制文件。

来源预览：`docs/previews/tech-icons-0.16.0.png`，保留原件。生产图集按 4 列×3 行、从左到右从上到下排列：

| 行 | 第 1 列 | 第 2 列 | 第 3 列 | 第 4 列 |
| --- | --- | --- | --- | --- |
| 1 | 交易系统 1951 | 建筑兑换 I 7701 | 建筑兑换 II 7702 | 建筑兑换 III 7703 |
| 2 | 建筑兑换 IV 7704 | 建筑兑换 V 7705 | 交易效率 I 7801 | 交易效率 II 7802 |
| 3 | 交易效率 III 7803 | 交易效率 IV 7804 | 交易效率 V 7805 | 交易效率 VI 7806 |

`TechnologyIcons` 在 Unity 中导入时清理极低透明度残留、统一白色遮罩，测量每格的实际图形边界，添加透明留白后以面积过滤缩小为原版要求的 **80×80 独立纹理和 Sprite**。IconSet 固定从纹理 (0,0) 复制 80×80，不读取 Sprite.rect；不能保留大纹理，否则行内图标会复制到透明边角。保留罗马数字、双线性过滤、无 mipmap；每个 Sprite 都有独立纹理，不会采样相邻等级。原版绘制节点边框、底色和状态颜色。

原型 Finalize 在原版 TechProto.Preload2 之后设置 `_iconSprite`，随后 LDBTool 重建原版 IconSet。科技树、详情和队列均读取相同科技 Sprite。资源异常时记录错误并保留原生图标。

校验：`python scripts/validate-technology-icons.py` 只读取 PNG，检查透明通道、网格和 12 个图形边界。在 PowerShell 7 下运行 `scripts/test-native-tech-icon-size.ps1`，可调用实际编译的导入器并与原版 80px 图集要求比较，检查非空图形、透明和细笔画；不启动 Unity。构建后核对 DLL 内嵌资源哈希与本 PNG 一致。

生成方式：内置 imagegen，保留主体与罗马数字的提取编辑。最终清理提示词如下：

```text
Technical cleanup of this EXACT 4-column 3-row technology icon atlas. Preserve every icon's shape, exact location, scale, row/column ordering and all eleven Roman rank labels. Change ONLY rendering/alpha quality: the main glyphs must be SOLID FLAT WHITE, no gray or gold outlines, no bevels, no shadows. The background, holes, gutters, and all non-icon pixels must be COMPLETELY TRANSPARENT with alpha zero. Eliminate every speckle, mist, off-white background, semi-transparent noise and leftover edge from the previous contact sheet. This must look like a clean professional white UI stencil atlas, not an extracted photo. Draw crisp geometrically clean edges with only a narrow antialiased edge; large interior white regions opaque alpha 255. Retain a single flat ivory-white color #F5F5F5 in visible icon strokes. Keep exactly 12 sprites in exactly the current 4x3 cells on a 4:3 canvas. Transparent margin around each full icon including numeral, at least 10% of each cell, no touching neighboring cells. All corresponding building-exchange base shapes identical, all corresponding efficiency base shapes identical. Keep no text except these Roman ranks. Do not add frames, tiles, connecting lines, backgrounds, grids, checkerboards or extra symbols. Match the original approved silhouettes exactly while removing the gray embossed effect and all background residue.
```
