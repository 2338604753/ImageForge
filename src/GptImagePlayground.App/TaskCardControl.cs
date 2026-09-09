using GptImagePlayground.Core.Models;
using GptImagePlayground.Core.Services;
using Sunny.UI;
using System.Drawing;
using System.Windows.Forms;
using TaskStatus = GptImagePlayground.Core.Models.TaskStatus;

namespace GptImagePlayground.App;

public class TaskCardControl : UserControl
{
    private PictureBox _thumb = new();
    private UISymbolLabel _title = new();
    private UISymbolLabel _status = new();
    private UISymbolLabel _time = new();
    private UISymbolButton? _retryBtn;
    private TaskRecord _task;
    private bool _hover;

    public event EventHandler? OpenRequested;
    public event EventHandler? RetryRequested;

    public TaskCardControl(TaskRecord task)
    {
        _task = task;
        Build();
        RefreshTask(task);
    }

    private void Build()
    {
        this.Size = new Size(212, 262);
        this.BackColor = Color.White;
        this.Margin = new Padding(10);
        this.Cursor = Cursors.Hand;

        _thumb.Dock = DockStyle.Top;
        _thumb.Height = 176;
        _thumb.SizeMode = PictureBoxSizeMode.Zoom;
        _thumb.BackColor = Color.FromArgb(248, 250, 252);
        this.Controls.Add(_thumb);

        _title.Left = 10; _title.Top = 184; _title.Width = 192; _title.Height = 30;
        _title.Style = UIStyle.Custom;
        _title.Font = new Font("微软雅黑", 9F);
        _title.ForeColor = Color.FromArgb(51, 65, 85);
        _title.Symbol = 61555;
        this.Controls.Add(_title);

        _status.Left = 10; _status.Top = 214; _status.Width = 130; _status.Height = 26;
        _status.Style = UIStyle.Custom;
        _status.Font = new Font("微软雅黑", 8.5F);
        this.Controls.Add(_status);

        _time.Left = 10; _time.Top = 238; _time.Width = 192; _time.Height = 18;
        _time.Style = UIStyle.Custom;
        _time.Font = new Font("微软雅黑", 8F);
        _time.ForeColor = Color.FromArgb(148, 163, 184);
        _time.Symbol = 0;
        _thumb.Text = "";
        this.Controls.Add(_time);

        this.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _thumb.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _title.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);

        this.MouseEnter += (s, e) => { _hover = true; this.BackColor = Color.FromArgb(250, 252, 255); Invalidate(); };
        this.MouseLeave += (s, e) => { _hover = false; this.BackColor = Color.White; Invalidate(); };
    }

    public TaskRecord Task => _task;

    public void RefreshTask(TaskRecord task)
    {
        _task = task;
        LoadThumb(task);
        _title.Text = Truncate(task.Prompt, 26);

        switch (task.Status)
        {
            case TaskStatus.Running:
                _status.Symbol = 61698;
                _status.Text = "生成中…";
                _status.ForeColor = Color.FromArgb(59, 130, 246);
                break;
            case TaskStatus.Done:
                _status.Symbol = 61586;
                _status.Text = $"{task.OutputImages.Count} 张";
                _status.ForeColor = Color.FromArgb(22, 163, 74);
                break;
            case TaskStatus.Error:
                _status.Symbol = 61550;
                _status.Text = "失败";
                _status.ForeColor = Color.FromArgb(220, 38, 38);
                break;
        }

        _time.Text = task.FinishedAt is { } f
            ? $"{f.ToLocalTime():MM-dd HH:mm}  {FormatElapsed(task.Elapsed)}"
            : task.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm");

        if (task.Status == TaskStatus.Error && _retryBtn == null)
        {
            _retryBtn = new UISymbolButton
            {
                Text = "重试",
                Symbol = 61516,
                Left = 148, Top = 212, Width = 56, Height = 26,
                Font = new Font("微软雅黑", 8F),
                Style = UIStyle.Custom
            };
            _retryBtn.FillColor = Color.FromArgb(241, 245, 249);
            _retryBtn.FillHoverColor = Color.FromArgb(226, 232, 240);
            _retryBtn.FillPressColor = Color.FromArgb(203, 213, 225);
            _retryBtn.RectColor = Color.FromArgb(226, 232, 240);
            _retryBtn.ForeColor = Color.FromArgb(51, 65, 85);
            _retryBtn.SymbolColor = Color.FromArgb(51, 65, 85);
            _retryBtn.Radius = 13;
            _retryBtn.RadiusSides = UICornerRadiusSides.All;
            _retryBtn.Click += (s, e) => RetryRequested?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_retryBtn);
            _retryBtn.BringToFront();
        }
        else if (task.Status != TaskStatus.Error && _retryBtn != null)
        {
            this.Controls.Remove(_retryBtn);
            _retryBtn = null;
        }
    }

    private void LoadThumb(TaskRecord task)
    {
        string? id = task.OutputImages.FirstOrDefault();
        string? thumbPath = null;
        if (!string.IsNullOrEmpty(id)) thumbPath = AppServices.Images.GetThumbnailPath(id);

        if (string.IsNullOrEmpty(thumbPath))
        {
            _thumb.Image = null;
            _thumb.BackColor = Color.FromArgb(245, 247, 251);
            return;
        }

        try
        {
            using var fs = new FileStream(thumbPath, FileMode.Open, FileAccess.Read);
            _thumb.Image = Image.FromStream(fs);
            _thumb.BackColor = Color.White;
        }
        catch
        {
            _thumb.Image = null;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(无提示词)";
        s = s.Replace('\r', ' ').Replace('\n', ' ');
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static string FormatElapsed(long? ms)
    {
        if (ms == null) return "";
        var s = ms.Value / 1000.0;
        return s < 60 ? $"{s:0.0}s" : $"{s / 60:0.0}m";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect(rect, 12);
        // 外描边（默认可见浅灰，悬停变蓝色加粗）
        var border = _hover ? Color.FromArgb(59, 130, 246) : Color.FromArgb(203, 213, 225);
        using var pen = new Pen(border, _hover ? 2f : 1.4f);
        g.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRoundedRegion(12);
    }

    private void ApplyRoundedRegion(int radius)
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundRect(new Rectangle(0, 0, Width, Height), radius);
        Region = new Region(path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
