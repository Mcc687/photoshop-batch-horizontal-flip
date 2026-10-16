# 架构说明

## 处理流程

```text
选择文件夹
  ↓
筛选当前目录图片 + Windows 自然排序
  ↓
原图移动到带时间戳的备份子目录
  ↓
从备份复制出 1～N 到原目录
  ↓
写入临时任务文件
  ↓
通过 Photoshop COM 执行内嵌 JSX
  ↓
JSX 校验动作 → app.batch 原生批处理
  ↓
只保存并关闭本批文件 → 写回结果文件
  ↓
成功静默退出 / 失败显示诊断
```

## C# 主程序

`src/PhotoshopBatchFlip.cs` 是单文件 WinForms 应用，负责：

- 文件夹选择和拖放入口。
- 支持格式筛选及 `StrCmpLogicalW` 自然排序。
- 原图备份、结果命名以及复制阶段回滚。
- 生成 UTF-8 临时任务文件。
- 自动尝试已注册的 Photoshop COM 版本。
- 把 JSX 资源释放到临时目录并调用 `DoJavaScriptFile`。
- 读取 JSX 结果并决定静默退出或显示错误。

编译时必须把 JSX 以内嵌资源名 `PhotoshopBatchFlip.photoshop_batch_flip.jsx` 加入 EXE。

## Photoshop JSX

`src/photoshop_batch_flip.jsx` 负责：

- 从 `%TEMP%/PS图片批量水平翻转/current_job.txt` 定位本次任务。
- 校验动作存在、只有一步，并且步骤名包含中文“翻转”或英文 `Flip`。
- 通过 `app.batch` 一次提交全部文件，目标设为 `NODESTINATION`。
- 按绝对路径找到本批打开的文档，保存并关闭。
- 恢复 Photoshop 原有的对话框设置。
- 把成功数量或错误写回结果文件。

## 临时协议

任务文件当前版本为 `PS_FLIP_JOB_V1`，每行依次为：

1. 协议标识。
2. 动作组名。
3. 动作名。
4. 结果文件路径。
5. Photoshop 批处理错误日志路径。
6. 文件数量。
7. 后续 N 行为待处理文件绝对路径。

扩展协议时应新增版本标识并保留清晰的兼容或失败策略。

## 失败边界

- Photoshop 启动前：备份或复制失败会尝试完整回滚。
- Photoshop 启动后：不自动覆盖或回滚已有输出，原图备份始终保留。
- Photoshop 的错误日志若只包含“开始批处理/结束批处理”头尾，会被视为无真实错误并删除。

