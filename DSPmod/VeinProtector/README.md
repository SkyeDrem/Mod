# VeinProtector

《戴森球计划》矿脉保护 Mod，当前版本 **0.5.1**。

**玩家说明与发布文案请编辑 [release/README.md](release/README.md)。** 该文件会作为发布 ZIP 的 README，也用于展示中英文玩法、设置、安装说明和截图。

## 目录

| 路径 | 用途 |
| --- | --- |
| `release/README.md` | 玩家发布文档 |
| `release/manifest.json` | 发布名称、版本、依赖和项目网址 |
| `release/icon.png` | 发布图标 |
| `release/CHANGELOG.md` | 发布更新记录 |
| `image/` | README 截图，GitHub 图片绝对链接指向此处 |
| `src/` | Mod 源码 |
| `tests/` | 逻辑与补丁检查 |
| `scripts/` | 构建、检查、打包及本地部署脚本 |
| `dist/` | 生成的发布包，不提交 Git |

## 开发

需要 Windows、本机《戴森球计划》游戏程序集、BepInEx/Harmony 引用，以及项目使用的 .NET Framework 4.7.2 构建环境。

游戏引用放在 `src/VeinProtector/AssemblyFromGame/`，加载器引用放在 `src/VeinProtector/AssemblyFromProfile/`。这些目录不提交 Git。原版接口分析见 [docs/assembly-analysis.md](docs/assembly-analysis.md)。

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\package.ps1
```

打包输出为 `dist/VeinProtector-0.5.1.zip`。修改玩家文档后重新运行打包命令即可更新 ZIP 内的 README；`package-local.ps1` 仍可使用。

## English

Vein protection mod for Dyson Sphere Program, version **0.5.1**. Edit **[release/README.md](release/README.md)** for the player-facing Chinese/English description included in the release ZIP.

Release metadata lives in `release/`, screenshots in `image/`, source in `src/`, checks in `tests/`, and build/package scripts in `scripts/`. Generated files in `dist/` are excluded from Git.

Provide local game and BepInEx/Harmony references in the ignored `AssemblyFromGame/` and `AssemblyFromProfile/` folders under `src/VeinProtector/`, then run the build, test and package commands above.

The project retains its [MIT license](LICENSE).
