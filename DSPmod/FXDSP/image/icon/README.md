# FXDSP 图标制作归档

正式发布图标为 [release/icon.png](../../release/icon.png)，由 [08-icon-final.png](08-icon-final.png) 缩小为256×256。运行 `python scripts/render-package-icon.py` 可重新导出，不会再用旧SVG覆盖新图标。

| 文件 | 阶段 |
| --- | --- |
| 03-headswap-classic-yellow.png | 按正面、侧面参考修正的经典黄色头部换头图 |
| 08-icon-final.png | 去掉多余水平连接线的最终高清图 |

旧阶段稿已清理；当前保留人物底图、最终高清图和原始参考素材。

`reference/` 保留原PNG/SVG图标、久留美官方封面、宇宙矩阵图标及用户提供的头部参考。

绘制使用内置 image_gen，尺寸导出使用Pillow。完整提示词见[制作记录](../../docs/previews/icon-icarus-v2/README.md)。

封面参考来自[《FX战士久留美》官方音乐页](https://fxkurumi-info.com/music/)，[原图](https://fxkurumi-info.com/assets/img/music/jk.jpg)为1000×1000。宇宙矩阵参考来自[DSP Wiki物品图标](https://media.dsp-wiki.com/f/fc/Icon_Universe_Matrix.png)。头部正面、侧面近景由用户提供。
