using GptImagePlayground.Core.Models;
using GptImagePlayground.Core.Services;
using Sunny.UI;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TaskStatus = GptImagePlayground.Core.Models.TaskStatus;

namespace GptImagePlayground.App;

public class TaskDetailForm : UIForm
{
    private readonly TaskRecord _task;
    private readonly ImageStore _images;

    private FlowLayoutPanel _imageFlow = new();
    private UILabel _infoLabel = new();
    private UISymbolButton _btnDownload = new();
    private UISymbolButton _btnSaveAs = new();
    private UISymbolButton _btnDelete = new();
    private UISymbolButton _btnClose = new();

    public TaskDetailForm(TaskRecord task, ImageStore images)
    {
        _task = task;
        _images = images;
        BuildLayout();
        LoadImages();
        LoadInfo();
    }

    private void BuildLayout()
    {
        this.Text = "任务详情";
        UiStyle.ApplyForm(this, new Size(880, 700));
        this.ShowTitle = true;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = this.Size;
        this.ShowInTaskbar = false;

        var titleLabel = new UISymbolLabel
        {
            Text = _task.Status == TaskStatus.Error ? "生成失败" : "生成结果",
            Symbol = _task.Status == TaskStatus.Error ? 61550 : 61586,
            Left = 18, Top = 16, Width = 160, Height = 26,
            Style = UIStyle.Custom,
            Font = new Font("微软雅黑", 10.5F, FontStyle.Bold),
            ForeColor = _task.Status == TaskStatus.Error ? UiStyle.Danger : UiStyle.Success
        };
        this.Controls.Add(titleLabel);

        _imageFlow.Left = 18; _imageFlow.Top = 52; _imageFlow.Width = 844; _imageFlow.Height = 360;
        _imageFlow.AutoScroll = true;
        _imageFlow.WrapContents = false;
        _imageFlow.BackColor = UiStyle.Bg;
        this.Controls.Add(_imageFlow);

        _infoLabel.Left = 18; _infoLabel.Top = 420; _infoLabel.Width = 844; _infoLabel.Height = 220;
        _infoLabel.Style = UIStyle.Custom;
        _infoLabel.Text = "";
        _infoLabel.AutoSize = false;
        _infoLabel.Font = new Font("微软雅黑", 9.5F);
        _infoLabel.ForeColor = UiStyle.TextSec;
        this.Controls.Add(_infoLabel);

        var btnY = 648;
        _btnDelete.Text = "删除任务"; _btnDelete.Symbol = 61571;
        _btnDelete.Left = 18; _btnDelete.Top = btnY; _btnDelete.Width = 120; _btnDelete.Height = 36;
        UiStyle.StyleGhostButton(_btnDelete);
        _btnDelete.ForeColor = UiStyle.Danger;
        _btnDelete.SymbolColor = UiStyle.Danger;
        _btnDelete.Click += (s, e) => DeleteTask();
        this.Controls.Add(_btnDelete);

        _btnSaveAs.Text = "另存为…"; _btnSaveAs.Symbol = 61601;
        _btnSaveAs.Left = 152; _btnSaveAs.Top = btnY; _btnSaveAs.Width = 120; _btnSaveAs.Height = 36;
        UiStyle.StyleGhostButton(_btnSaveAs);
        _btnSaveAs.Click += (s, e) => SaveSelected();
        this.Controls.Add(_btnSaveAs);

        _btnDownload.Text = "下载全部"; _btnDownload.Symbol = 61530;
        _btnDownload.Left = 286; _btnDownload.Top = btnY; _btnDownload.Width = 120; _btnDownload.Height = 36;
        UiStyle.StylePrimaryButton(_btnDownload);
        _btnDownload.Click += (s, e) => DownloadAll();
        this.Controls.Add(_btnDownload);

        _btnClose.Text = "关闭"; _btnClose.Symbol = 61546;
        _btnClose.Left = 730; _btnClose.Top = btnY; _btnClose.Width = 132; _btnClose.Height = 36;
        UiStyle.StyleGhostButton(_btnClose);
        _btnClose.Click += (s, e) => DialogResult = DialogResult.OK;
        this.Controls.Add(_btnClose);
    }

    private void LoadImages()
    {
        _imageFlow.Controls.Clear();
        foreach (var id in _task.OutputImages)
        {
            var img = TryLoadImage(id);
            if (img == null) continue;
            var pb = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = img,
                Width = 340, Height = 340,
                Margin = new Padding(6),
                BackColor = Color.White
            };
            _imageFlow.Controls.Add(pb);
        }
        if (_task.OutputImages.Count == 0 && _task.Status == TaskStatus.Error)
        {
            _imageFlow.Controls.Add(new UILabel
            {
                Text = "生成失败，未返回图片。\n\n" + _task.Error,
                Width = _imageFlow.Width - 6,
                Height = _imageFlow.Height - 6,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0),
                Style = UIStyle.Custom,
                ForeColor = UiStyle.Danger,
                BackColor = UiStyle.Bg,
                Font = new Font("微软雅黑", 10F)
            });
        }
    }

    private Image? TryLoadImage(string id)
    {
        var path = _images.GetThumbnailPath(id);
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Image.FromStream(fs);
        }
        catch { return null; }
    }

    private void LoadInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("提示词：" + _task.Prompt);
        sb.AppendLine();
        sb.AppendLine($"模型：{_task.ApiModel}   模式：{_task.ApiMode}   配置：{_task.ApiProfileName}");
        sb.AppendLine($"尺寸：{_task.Params.Size}   质量：{_task.Params.Quality}   格式：{_task.Params.OutputFormat}   张数：{_task.Params.N}");
        if (_task.HasActualParams && _task.ActualParams != null)
        {
            sb.AppendLine($"实际生效：{_task.ActualParams.Size} / {_task.ActualParams.Quality} / {_task.ActualParams.OutputFormat}");
        }
        if (_task.Elapsed != null) sb.AppendLine($"耗时：{_task.Elapsed.Value / 1000.0:0.0} 秒");
        sb.AppendLine($"创建：{_task.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        if (_task.Status == TaskStatus.Error) sb.AppendLine($"错误：{_task.Error}");
        if (_task.TransparentOutput) sb.AppendLine("透明背景：已启用");
        if (_task.RawImageUrls.Count > 0) sb.AppendLine($"原始链接：{string.Join(" , ", _task.RawImageUrls)}");
        _infoLabel.Text = sb.ToString();
    }

    private void DeleteTask()
    {
        if (MessageBox.Show("确定删除该任务记录？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        AppServices.Tasks.DeleteTask(_task.Id);
        DialogResult = DialogResult.OK;
    }

    private void SaveSelected()
    {
        var files = Directory.GetFiles(AppPaths.ImagesDirectory(AppServices.Settings.DataDirectory), _task.OutputImages.FirstOrDefault()?.ToString() + ".*");
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG 图片|*.png",
            FileName = $"{_task.Prompt[..Math.Min(20, _task.Prompt.Length)].Trim()}.png"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (files.Length > 0) File.Copy(files[0], dlg.FileName, true);
    }

    private void DownloadAll()
    {
        using var dlg = new FolderBrowserDialog { Description = "选择保存目录" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var dir = Path.Combine(dlg.SelectedPath, $"gptimg_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(dir);
        var imgsDir = AppPaths.ImagesDirectory(AppServices.Settings.DataDirectory);
        var count = 0;
        for (var i = 0; i < _task.OutputImages.Count; i++)
        {
            var id = _task.OutputImages[i];
            var files = Directory.GetFiles(imgsDir, id + ".*");
            if (files.Length == 0) continue;
            var ext = Path.GetExtension(files[0]);
            File.Copy(files[0], Path.Combine(dir, $"image_{i + 1}{ext}"), true);
            count++;
        }
        MessageBox.Show($"已导出 {count} 张图片到：\n{dir}");
    }
}
