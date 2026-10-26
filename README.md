# Mod

SkyeDrem 的游戏 Mod 源码与发布文档。目前包含《戴森球计划》的两个独立 Mod。

| 项目 | 版本 | 功能 | 玩家说明 |
| --- | --- | --- | --- |
| [FXDSP](DSPmod/FXDSP) | 0.20.2 | 物品本位杠杆交易、建筑币兑换、物流交付平台与成就 | [中文 / English](DSPmod/FXDSP/release/README.md) |
| [VeinProtector](DSPmod/VeinProtector) | 1.0.0 | 保留有限固体矿点最后一个矿物，并提供保护状态产能详情 | [中文 / English](DSPmod/VeinProtector/release/README.md) |

## 目录

```text
DSPmod/
├── FXDSP/           # DSP 交易平台
└── VeinProtector/   # 矿脉保护
```

每个项目独立构建与打包，具体步骤见对应目录中的 README 和 scripts。仓库只保存源码、测试、文档、图片及发布元数据；本地 SDK、缓存、游戏/依赖 DLL、个人配置和 ZIP 不上传。

VeinProtector 原独立仓库已合并到本仓库，提交历史和原有发布记录保留。各项目的版本号未因目录调整而变更。

## English

Source code and release documentation for SkyeDrem's game mods. The repository currently contains two independent Dyson Sphere Program mods: **FXDSP 0.20.2** and **VeinProtector 1.0.0**.

Each project has its own build and packaging scripts. See the player documentation linked above and each project's README for development instructions. Local SDKs, caches, game/dependency binaries, personal configuration, saves and ZIP packages are excluded from Git.

The former standalone VeinProtector repository has been consolidated here with its commit history and existing releases preserved. Project versions remain unchanged.
