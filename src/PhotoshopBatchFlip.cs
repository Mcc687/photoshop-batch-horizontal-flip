using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("PS 图片批量水平翻转")]
[assembly: AssemblyDescription("使用 Photoshop 动作批量水平翻转、备份并自然排序重命名图片")]
[assembly: AssemblyProduct("PS Batch Horizontal Flip")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

namespace PhotoshopBatchFlip
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\PS图片批量水平翻转", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("工具已经在运行。", "PS 图片批量水平翻转",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private const string ActionSetName = "组 1";
        private const string ActionName = "水平翻转";

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(
            new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".psd" },
            StringComparer.OrdinalIgnoreCase);

        private readonly Button startButton;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;

        public MainForm(string initialFolder)
        {
            Text = "PS 图片批量水平翻转";
            ClientSize = new Size(560, 270);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            AllowDrop = true;

            var title = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Location = new Point(28, 24),
                Text = "PS 图片批量水平翻转"
            };

            var description = new Label
            {
                AutoSize = false,
                Location = new Point(30, 68),
                Size = new Size(500, 70),
                Text = "选择图片文件夹后，将按文件名自然排序并重命名为 1～N，\r\n然后调用 Photoshop 原生批处理执行“组 1 → 水平翻转”。\r\n结果直接保存在所选文件夹；原图会移入带日期的备份子文件夹。"
            };

            startButton = new Button
            {
                Location = new Point(30, 150),
                Size = new Size(500, 46),
                Text = "选择图片文件夹并开始",
                UseVisualStyleBackColor = true
            };
            startButton.Click += StartButtonClick;

            progressBar = new ProgressBar
            {
                Location = new Point(30, 207),
                Size = new Size(500, 8),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            statusLabel = new Label
            {
                AutoEllipsis = true,
                Location = new Point(30, 224),
                Size = new Size(500, 28),
                Text = "支持 JPG、JPEG、PNG、BMP、TIF、TIFF、PSD；忽略子文件夹。"
            };

            Controls.Add(title);
            Controls.Add(description);
            Controls.Add(startButton);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);

            DragEnter += MainFormDragEnter;
            DragDrop += MainFormDragDrop;
            if (!string.IsNullOrWhiteSpace(initialFolder))
                Shown += (sender, args) => BeginProcessingFolder(initialFolder);
        }

        private void StartButtonClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择需要处理的图片文件夹（只处理当前文件夹）";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                BeginProcessingFolder(dialog.SelectedPath);
            }
        }

        private void MainFormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length == 1 && Directory.Exists(paths[0]))
                    e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainFormDragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length == 1)
                BeginProcessingFolder(paths[0]);
        }

        private void BeginProcessingFolder(string sourceFolder)
        {
            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show(this, "文件夹不存在：\r\n" + sourceFolder,
                    "文件夹不存在", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string[] imageFiles;
            try
            {
                imageFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                    .ToArray();
                Array.Sort(imageFiles, NaturalPathComparer.Instance);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "无法读取该文件夹：\r\n" + error.Message,
                    "读取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (imageFiles.Length == 0)
            {
                MessageBox.Show(this, "当前文件夹中没有受支持的图片。",
                    "没有图片", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "正在备份、重命名并处理 " + imageFiles.Length + " 张图片……");
            StartWorker(sourceFolder, imageFiles);
        }

        private void StartWorker(string sourceFolder, string[] imageFiles)
        {
            var worker = new Thread(() =>
            {
                BatchResult result;
                try
                {
                    result = ProcessBatch(sourceFolder, imageFiles);
                }
                catch (Exception error)
                {
                    result = new BatchResult
                    {
                        Success = false,
                        Message = error.GetBaseException().Message
                    };
                }

                BeginInvoke(new Action(() => FinishWorker(result)));
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private BatchResult ProcessBatch(string sourceFolder, string[] imageFiles)
        {
            string outputFolder = sourceFolder;
            string backupFolder = CreateUniqueBackupFolder(sourceFolder);
            var copiedFiles = new List<string>();
            var movedFiles = new List<MovedFile>();

            for (int index = 0; index < imageFiles.Length; index++)
            {
                string extension = Path.GetExtension(imageFiles[index]);
                string destination = Path.Combine(outputFolder, (index + 1) + extension);
                if (Directory.Exists(destination))
                    throw new IOException("无法生成结果文件，因为同名文件夹已存在：" + destination);
            }

            Directory.CreateDirectory(backupFolder);
            try
            {
                foreach (string source in imageFiles)
                {
                    string backup = Path.Combine(backupFolder, Path.GetFileName(source));
                    File.Move(source, backup);
                    movedFiles.Add(new MovedFile { SourcePath = source, BackupPath = backup });
                }

                for (int index = 0; index < movedFiles.Count; index++)
                {
                    string extension = Path.GetExtension(movedFiles[index].BackupPath);
                    string destination = Path.Combine(outputFolder, (index + 1) + extension);
                    File.Copy(movedFiles[index].BackupPath, destination, false);
                    copiedFiles.Add(destination);
                }
            }
            catch
            {
                RollBackStaging(copiedFiles, movedFiles, backupFolder);
                throw;
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "PS图片批量水平翻转");
            Directory.CreateDirectory(tempRoot);
            string jobId = Guid.NewGuid().ToString("N");
            string jobPath = Path.Combine(tempRoot, "job_" + jobId + ".txt");
            string resultPath = Path.Combine(tempRoot, "result_" + jobId + ".txt");
            string pointerPath = Path.Combine(tempRoot, "current_job.txt");
            string errorPath = Path.Combine(outputFolder, "Photoshop批处理错误.txt");

            var jobLines = new List<string>
            {
                "PS_FLIP_JOB_V1",
                ActionSetName,
                ActionName,
                resultPath,
                errorPath,
                copiedFiles.Count.ToString()
            };
            jobLines.AddRange(copiedFiles);
            bool success;
            string detail;
            try
            {
                File.WriteAllLines(jobPath, jobLines, new UTF8Encoding(true));
                File.WriteAllText(pointerPath, jobPath, new UTF8Encoding(true));

                InvokePhotoshopScript();

                if (!File.Exists(resultPath))
                    throw new InvalidOperationException("Photoshop 未返回处理结果。");

                string[] resultLines = File.ReadAllLines(resultPath, Encoding.UTF8);
                success = resultLines.Any(line => line == "STATUS\tOK");
                detail = string.Join(Environment.NewLine,
                    resultLines.Where(line => line.StartsWith("ERROR\t")));
            }
            catch (Exception error)
            {
                return new BatchResult
                {
                    Success = false,
                    OutputFolder = outputFolder,
                    BackupFolder = backupFolder,
                    Message = error.GetBaseException().Message
                };
            }
            finally
            {
                TryDelete(jobPath);
                TryDelete(resultPath);
                TryDelete(pointerPath);
            }

            if (!success)
            {
                return new BatchResult
                {
                    Success = false,
                    OutputFolder = outputFolder,
                    BackupFolder = backupFolder,
                    Message = string.IsNullOrWhiteSpace(detail)
                        ? "Photoshop 处理失败；原图备份和当前结果已保留。"
                        : detail.Replace("ERROR\t", "")
                };
            }

            bool hasBatchErrors = HasRealBatchErrors(errorPath);
            return new BatchResult
            {
                Success = true,
                HasWarnings = hasBatchErrors,
                OutputFolder = outputFolder,
                BackupFolder = backupFolder,
                ProcessedCount = copiedFiles.Count,
                Message = hasBatchErrors ? "处理完成，但 Photoshop 记录了部分警告。" : "处理完成。"
            };
        }

        private static void InvokePhotoshopScript()
        {
            string scriptFolder = Path.Combine(Path.GetTempPath(), "PS图片批量水平翻转");
            Directory.CreateDirectory(scriptFolder);
            string scriptPath = Path.Combine(scriptFolder, "photoshop_batch_flip.jsx");
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "PhotoshopBatchFlip.photoshop_batch_flip.jsx"))
            {
                if (resource == null)
                    throw new InvalidOperationException("程序中缺少内置 Photoshop 批处理脚本。");
                using (var target = new FileStream(scriptPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    resource.CopyTo(target);
            }

            object photoshop = null;
            Type photoshopType = null;
            try
            {
                photoshop = CreatePhotoshopApplication(out photoshopType);
                photoshopType.InvokeMember(
                    "DoJavaScriptFile",
                    BindingFlags.InvokeMethod,
                    null,
                    photoshop,
                    new object[] { scriptPath, new object[0], 1 });
            }
            finally
            {
                if (photoshop != null && Marshal.IsComObject(photoshop))
                    Marshal.FinalReleaseComObject(photoshop);
            }
        }

        private static object CreatePhotoshopApplication(out Type photoshopType)
        {
            // 优先使用具体版本号，避免 Windows 中残留的通用 Photoshop.Application
            // 注册项指向已经卸载的旧版本。
            for (int version = 300; version >= 10; version -= 10)
            {
                try
                {
                    Type installedVersion = Type.GetTypeFromProgID(
                        "Photoshop.Application." + version, false);
                    if (installedVersion != null)
                    {
                        object application = Activator.CreateInstance(installedVersion);
                        photoshopType = installedVersion;
                        return application;
                    }
                }
                catch { }
            }

            try
            {
                Type currentVersion = Type.GetTypeFromProgID("Photoshop.Application", false);
                if (currentVersion != null)
                {
                    object application = Activator.CreateInstance(currentVersion);
                    photoshopType = currentVersion;
                    return application;
                }
            }
            catch { }

            photoshopType = null;
            throw new InvalidOperationException(
                "未检测到可自动调用的 Adobe Photoshop。请先安装并启动一次 Photoshop，然后重试。");
        }

        private static string CreateUniqueBackupFolder(string sourceFolder)
        {
            string baseName = "_原图备份_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            string candidate = Path.Combine(sourceFolder, baseName);
            int suffix = 2;
            while (Directory.Exists(candidate))
                candidate = Path.Combine(sourceFolder, baseName + "_" + suffix++);
            return candidate;
        }

        private static void RollBackStaging(
            IList<string> copiedFiles, IList<MovedFile> movedFiles, string backupFolder)
        {
            foreach (string copied in copiedFiles.Reverse())
                TryDelete(copied);

            foreach (MovedFile moved in movedFiles.Reverse())
            {
                try
                {
                    if (File.Exists(moved.BackupPath) && !File.Exists(moved.SourcePath))
                        File.Move(moved.BackupPath, moved.SourcePath);
                }
                catch { }
            }

            try
            {
                if (Directory.Exists(backupFolder) &&
                    !Directory.EnumerateFileSystemEntries(backupFolder).Any())
                    Directory.Delete(backupFolder);
            }
            catch { }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static bool HasRealBatchErrors(string errorPath)
        {
            if (!File.Exists(errorPath))
                return false;
            try
            {
                string content = File.ReadAllText(errorPath)
                    .Replace("开始批处理", "")
                    .Replace("结束批处理", "")
                    .Trim();
                if (content.Length == 0)
                {
                    File.Delete(errorPath);
                    return false;
                }
                return true;
            }
            catch
            {
                return new FileInfo(errorPath).Length > 0;
            }
        }

        private void FinishWorker(BatchResult result)
        {
            SetBusy(false, result.Message);

            if (result.Success)
            {
                Close();
                return;
            }

            string message = result.Message;
            if (!string.IsNullOrEmpty(result.OutputFolder))
                message += "\r\n\r\n当前结果保留在：\r\n" + result.OutputFolder;
            if (!string.IsNullOrEmpty(result.BackupFolder))
                message += "\r\n\r\n原图备份保留在：\r\n" + result.BackupFolder;
            MessageBox.Show(this, message, "处理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
            Close();
        }

        private void SetBusy(bool busy, string status)
        {
            startButton.Enabled = !busy;
            progressBar.Visible = busy;
            statusLabel.Text = status;
            UseWaitCursor = busy;
        }

        private sealed class BatchResult
        {
            public bool Success { get; set; }
            public bool HasWarnings { get; set; }
            public int ProcessedCount { get; set; }
            public string OutputFolder { get; set; }
            public string BackupFolder { get; set; }
            public string Message { get; set; }
        }

        private sealed class MovedFile
        {
            public string SourcePath { get; set; }
            public string BackupPath { get; set; }
        }

        private sealed class NaturalPathComparer : IComparer<string>
        {
            public static readonly NaturalPathComparer Instance = new NaturalPathComparer();

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int StrCmpLogicalW(string left, string right);

            public int Compare(string left, string right)
            {
                return StrCmpLogicalW(Path.GetFileName(left), Path.GetFileName(right));
            }
        }
    }
}
