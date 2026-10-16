# 贡献指南

感谢改进这个项目。

## 开始前

1. 阅读 `PROJECT_CONTEXT.md`，了解必须保持的用户行为和安全边界。
2. 阅读 `docs/ARCHITECTURE.md`，了解 C# 与 Photoshop JSX 的职责分工。
3. 不要提交个人图片、`.lnk`、`Actions Palette.psp`、测试输出或包含私人动作的 Photoshop 配置。

## 开发流程

1. Fork 仓库并创建功能分支。
2. 修改源码及相关文档。
3. 在 Windows PowerShell 中运行 `./build.ps1`。
4. 对文件处理、失败恢复或 Photoshop 行为的修改，必须用真实 Photoshop 做端到端验证。
5. 提交 Pull Request，说明 Photoshop 版本、测试文件格式、预期结果和实际结果。

## 代码约定

- 保持对 .NET Framework 4.x 编译器的兼容，不要无意引入较新的 C# 语法。
- 文件操作必须明确限定到用户选择的文件夹和本次创建的路径。
- 不应自动删除原图备份。
- 不应保存或关闭用户原本打开的 Photoshop 文档。
- 新增设置或改变默认行为时同步更新 README、安装说明和项目约定。

