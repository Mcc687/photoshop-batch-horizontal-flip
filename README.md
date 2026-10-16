# PS 图片批量水平翻转

一个面向 Windows 的便携小工具：选择一个图片文件夹后，调用 Adobe Photoshop 的原生批处理，一次性执行已录制的“水平翻转”动作；同时按自然顺序把结果重命名为 `1～N`，并自动备份原图。

## 下载

普通用户请从仓库的 [Releases](../../releases/latest) 下载最新版 ZIP，解压后运行 `PS图片批量水平翻转.exe`。不需要安装本工具。

> 本工具依赖桌面版 Adobe Photoshop，以及 Photoshop 动作面板中的 `组 1 → 水平翻转` 动作。动作不会被打包在 EXE 中，首次使用请按[安装与动作设置](docs/INSTALLATION.md)完成配置。

## 功能

- 使用 Photoshop 原生批处理，而不是逐张启动脚本。
- 只处理所选文件夹根目录，忽略子文件夹。
- 按 Windows 文件名自然排序，例如 `图片1、图片2、图片10`。
- 支持任意图片数量，结果命名为 `1～N`。
- 支持 JPG、JPEG、PNG、BMP、TIF、TIFF、PSD，并保持原扩展名和格式。
- 结果直接写入所选文件夹根目录。
- 原图移入 `_原图备份_yyyy-MM-dd_HHmmss` 子文件夹，便于恢复。
- 成功后不弹结果窗口、不打开文件夹，工具自动退出。
- 失败时显示错误，同时保留原图备份和当前结果供检查。
- 不保存或关闭 Photoshop 中原本已经打开的其他文档。
- 自动查找实际可启动的 Photoshop 版本，安装位置不必固定。

## 快速使用

1. 安装并至少启动过一次 Adobe Photoshop。
2. 在 Photoshop 中准备动作组 `组 1`，其中包含仅有一个翻转步骤的动作 `水平翻转`。
3. 解压 Release ZIP。
4. 双击 `PS图片批量水平翻转.exe`，选择图片文件夹；也可以把文件夹拖到 EXE 上。
5. 等待程序和 Photoshop 完成处理并自动退出。

处理前：

```text
商品图/
├─ 商品1.png
├─ 商品2.png
└─ 商品10.jpg
```

处理后：

```text
商品图/
├─ 1.png
├─ 2.png
├─ 3.jpg
└─ _原图备份_2026-10-16_180000/
   ├─ 商品1.png
   ├─ 商品2.png
   └─ 商品10.jpg
```

## 重要提醒

- 水平翻转会同时镜像文字、商标和包装文字。请只处理适合镜像的图片，并在上架前人工检查。
- 不要直接删除 `_原图备份_*`。确认处理结果无误后再自行决定是否保留。
- 动作中不能包含“保存”“另存为”或“关闭”；这些操作由工具统一处理。
- 本项目不会上传图片，所有处理均在本机文件夹和本机 Photoshop 中完成。

## 从源码构建

要求：Windows 10/11、Windows 自带的 .NET Framework 4.x C# 编译器。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist/`。GitHub Actions 也会在每次推送和 Pull Request 时编译便携包；推送 `v*` 标签会创建 GitHub Release。

## 项目结构

```text
src/                         WinForms 主程序与内嵌 Photoshop JSX
docs/INSTALLATION.md         安装、动作迁移和故障排查
docs/ARCHITECTURE.md         工作原理和安全边界
PROJECT_CONTEXT.md           当前需求、设计决策和二次开发约束
build.ps1                    本地/CI 构建和打包脚本
.github/workflows/build.yml  自动构建与 Release
```

开发前建议先阅读 [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) 和[架构说明](docs/ARCHITECTURE.md)。问题和改进欢迎提交 Issue 或 Pull Request，具体见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 兼容性

- 已验证：Windows 10、Adobe Photoshop 2021（22.5.9）。
- 程序会尝试自动识别其他已注册的桌面版 Photoshop。
- Photoshop 的 COM/ExtendScript 能力在不同版本可能存在差异，欢迎在 Issue 中反馈版本信息和错误日志。

## 许可与声明

代码采用 [MIT License](LICENSE)。Adobe、Photoshop 是其各自权利人的商标；本项目为独立工具，与 Adobe 无隶属或背书关系。

