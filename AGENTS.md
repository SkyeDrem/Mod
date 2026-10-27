# Mod 仓库工作约定

- 两个 DSP 项目位于仓库根目录下的 DSPmod/FXDSP 与 DSPmod/VeinProtector。
- 用户要求不使用子智能体。项目修改、检查与整合均由当前主智能体完成。
- 保留各子项目的独立版本、构建方式与许可证；FXDSP 的详细工作约定见其 AGENTS.md。
- GitHub 只提交源码、测试、文档、图片和发布元数据。不要提交本地 SDK、缓存、游戏或依赖 DLL、个人配置、令牌、存档及 ZIP。
- 用户最新要求暂不推送 GitHub。平时只做本地修改、检查和打包；等用户明确说明最终发布并要求推送时，再同步 GitHub，不能沿用先前的上传授权自动推送后续改动。
- 两个项目统一使用 release/ 存放发布文档与元数据。玩家发布文档：FXDSP/release/README.md 与 VeinProtector/release/README.md，路径均相对于 DSPmod；项目根 README 用于开发入口。
- 调整仓库目录或名称后同步更新 README 的 GitHub 图片绝对链接及两个发布 manifest 的 website_url；重新打包时保持用户指定的版本号。
