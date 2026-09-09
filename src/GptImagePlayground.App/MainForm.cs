using GptImagePlayground.Core.Models;
using GptImagePlayground.Core.Services;
using Sunny.UI;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using TaskStatus = GptImagePlayground.Core.Models.TaskStatus;

namespace GptImagePlayground.App;

public class MainForm : UIForm
{
    // 顶部
    private UIComboBox _profileCombo = new();
    private UISymbolLabel _profileLabel = new();
    private UITextBox _searchBox = new();
    private UISymbolButton _searchBtn = new();
    private UISymbolButton _settingsBtn = new();
    private UISymbolButton _clearBtn = new();
    private UISymbolButton _deleteBtn = new();

    // 输入区
    private UIPanel _inputPanel = new();
    private UIFlowLayoutPanel _refPanel = new();
    private UISymbolLabel _refHint = new();
    private UISymbolButton _addRefBtn = new();
    private UISymbolButton _removeRefBtn = new();
    private UISymbolButton _clearRefBtn = new();
    private UITextBox _promptBox = new();
    private UISymbolButton _generateBtn = new();

    // 参数
    private UIButton _btn1k = new();
    private UIButton _btn2k = new();
    private UIButton _btn4k = new();
    private UIButton _btnAuto = new();
    private UIComboBox _qualityCombo = new();
    private UIComboBox _formatCombo = new();
    private UIComboBox _sizeCombo = new();
    private NumericUpDown _countSpin = new();
    private UISwitch _transparentSwitch = new();

    // 画廊
    private UIPanel _galleryPanel = new();
    private FlowLayoutPanel _galleryFlow = new();
    private UISymbolLabel _emptyLabel = new();

    private readonly List<byte[]> _referenceImages = new();
    private readonly Dictionary<string, TaskCardControl> _cards = new();
    private readonly Dictionary<string, TaskRecord> _tasks = new();
    private bool _generating;

    // ===== 配色（统一现代浅色风格，取自 UiStyle）=====
    private static readonly Color C_ACCENT = UiStyle.Accent;
    private static readonly Color C_ACCENT_HOVER = UiStyle.AccentHover;
    private static readonly Color C_ACCENT_PRESS = UiStyle.AccentPress;
    private static readonly Color C_ACCENT_LIGHT = UiStyle.AccentLight;
    private static readonly Color C_WHITE = UiStyle.White;
    private static readonly Color C_BG = UiStyle.Bg;
    private static readonly Color C_PANEL = UiStyle.Panel;
    private static readonly Color C_BORDER = UiStyle.Border;
    private static readonly Color C_BORDER_LT = UiStyle.BorderLt;
    private static readonly Color C_TEXT = UiStyle.Text;
    private static readonly Color C_TEXT_SEC = UiStyle.TextSec;
    private static readonly Color C_TEXT_MUTED = UiStyle.TextMuted;
    private static readonly Color C_GHOST = UiStyle.Ghost;
    private static readonly Color C_GHOST_HOVER = UiStyle.GhostHover;
    private static readonly Color C_GHOST_PRESS = UiStyle.GhostPress;

    // ===== 通用样式（委托到 UiStyle）=====
    private static void StyleCombo(UIComboBox combo) => UiStyle.StyleCombo(combo);
    private static void StyleTextBox(UITextBox box) => UiStyle.StyleTextBox(box);
    private static void StyleGhostButton(UISymbolButton btn) => UiStyle.StyleGhostButton(btn);
    private static void StylePrimaryButton(UISymbolButton btn) => UiStyle.StylePrimaryButton(btn);
    private static void StyleGhostUIButton(UIButton btn) => UiStyle.StyleGhostUIButton(btn);

    public MainForm()
    {
        InitializeLayout();
        ReloadProfiles();
        LoadTasks();
    }

    // ===== 布局 =====
    private void InitializeLayout()
    {
        this.Text = "GPT Image Playground";
        this.Style = UIStyle.Custom;
        this.ShowTitle = true;
        this.Size = new Size(1200, 780);
        this.MinimumSize = new Size(1000, 660);
        this.TitleColor = Color.FromArgb(250, 250, 252);
        this.TitleForeColor = C_TEXT;
        this.BackColor = C_BG;

        BuildTopBar();
        BuildInputBar();
        BuildGallery();
    }

    private void BuildTopBar()
    {
        var bar = new UIPanel { Dock = DockStyle.Top, Height = 58, Style = UIStyle.Custom };
        bar.FillColor = C_WHITE;
        bar.RectColor = C_BORDER_LT;
        bar.RectSides = ToolStripStatusLabelBorderSides.Bottom;

        _profileLabel.Text = "配置";
        _profileLabel.Symbol = 61601;
        _profileLabel.Left = 18;
        _profileLabel.Top = 17;
        _profileLabel.Size = new Size(52, 24);
        _profileLabel.Style = UIStyle.Custom;
        _profileLabel.ForeColor = C_TEXT_SEC;
        _profileLabel.Font = new Font("微软雅黑", 9F);
        _profileLabel.SymbolColor = C_TEXT_SEC;
        bar.Controls.Add(_profileLabel);

        _profileCombo.Left = 72;
        _profileCombo.Top = 13;
        _profileCombo.Width = 260;
        _profileCombo.Height = 32;
        _profileCombo.DropDownStyle = UIDropDownStyle.DropDownList;
        StyleCombo(_profileCombo);
        _profileCombo.SelectedIndexChanged += ProfileCombo_SelectedIndexChanged;
        bar.Controls.Add(_profileCombo);

        _searchBox.Left = 352;
        _searchBox.Top = 13;
        _searchBox.Width = 300;
        _searchBox.Height = 32;
        _searchBox.Watermark = "搜索提示词…";
        StyleTextBox(_searchBox);
        bar.Controls.Add(_searchBox);

        _searchBtn.Text = "搜索";
        _searchBtn.Symbol = 61442;
        _searchBtn.Left = 662;
        _searchBtn.Top = 13;
        _searchBtn.Width = 92;
        _searchBtn.Height = 32;
        StylePrimaryButton(_searchBtn);
        _searchBtn.Click += (s, e) => LoadTasks(_searchBox.Text);
        bar.Controls.Add(_searchBtn);

        _settingsBtn.Text = "设置";
        _settingsBtn.Symbol = 61641;
        _settingsBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _settingsBtn.Left = bar.Width - 368;
        _settingsBtn.Top = 13;
        _settingsBtn.Width = 106;
        _settingsBtn.Height = 32;
        StyleGhostButton(_settingsBtn);
        _settingsBtn.Click += (s, e) => OpenSettings();
        bar.Controls.Add(_settingsBtn);

        _deleteBtn.Text = "清理失败";
        _deleteBtn.Symbol = 61552;
        _deleteBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _deleteBtn.Left = bar.Width - 252;
        _deleteBtn.Top = 13;
        _deleteBtn.Width = 106;
        _deleteBtn.Height = 32;
        StyleGhostButton(_deleteBtn);
        _deleteBtn.Click += (s, e) => DeleteFailedTasks();
        bar.Controls.Add(_deleteBtn);

        _clearBtn.Text = "清空";
        _clearBtn.Symbol = 61530;
        _clearBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clearBtn.Left = bar.Width - 136;
        _clearBtn.Top = 13;
        _clearBtn.Width = 106;
        _clearBtn.Height = 32;
        StyleGhostButton(_clearBtn);
        _clearBtn.Click += (s, e) => ClearAll();
        bar.Controls.Add(_clearBtn);

        this.Controls.Add(bar);
        bar.BringToFront();
    }

    private void BuildInputBar()
    {
        _inputPanel.Dock = DockStyle.Bottom;
        _inputPanel.Height = 224;
        _inputPanel.Style = UIStyle.Custom;
        _inputPanel.FillColor = C_WHITE;
        _inputPanel.RectColor = C_BORDER_LT;
        _inputPanel.RectSides = ToolStripStatusLabelBorderSides.Top;

        // 参考图
        var refLabel = new UISymbolLabel
        {
            Text = "参考图 (≤16)",
            Symbol = 61576,
            Left = 18,
            Top = 42,
            Width = 108,
            Height = 24,
            Style = UIStyle.Custom,
            ForeColor = C_TEXT_SEC,
            SymbolColor = C_TEXT_SEC,
            Font = new Font("微软雅黑", 9F)
        };
        _inputPanel.Controls.Add(refLabel);

        _refPanel.Left = 136;
        _refPanel.Top = 12;
        _refPanel.Height = 84;
        _refPanel.AutoScroll = true;
        _refPanel.Style = UIStyle.Custom;
        _refPanel.FillColor = Color.FromArgb(250, 251, 253);
        _refPanel.RectColor = C_BORDER;
        _refPanel.Radius = 10;
        _refPanel.RadiusSides = UICornerRadiusSides.All;
        _inputPanel.Controls.Add(_refPanel);

        _refHint.Text = "点击「添加」上传参考图";
        _refHint.Symbol = 61576;
        _refHint.Style = UIStyle.Custom;
        _refHint.ForeColor = C_TEXT_SEC;
        _refHint.SymbolColor = C_TEXT_SEC;
        _refHint.Font = new Font("微软雅黑", 9F);
        _refHint.TextAlign = ContentAlignment.MiddleCenter;
        _inputPanel.Controls.Add(_refHint);

        _addRefBtn.Text = "添加";
        _addRefBtn.Symbol = 61569;
        _addRefBtn.Top = 39; _addRefBtn.Width = 72; _addRefBtn.Height = 30;
        StyleGhostButton(_addRefBtn);
        _addRefBtn.Click += (s, e) => AddReference();
        _inputPanel.Controls.Add(_addRefBtn);
        _removeRefBtn.Text = "移除";
        _removeRefBtn.Symbol = 61571;
        _removeRefBtn.Top = 39; _removeRefBtn.Width = 72; _removeRefBtn.Height = 30;
        StyleGhostButton(_removeRefBtn);
        _removeRefBtn.Click += (s, e) => RemoveLastReference();
        _inputPanel.Controls.Add(_removeRefBtn);
        _clearRefBtn.Text = "清空";
        _clearRefBtn.Symbol = 61530;
        _clearRefBtn.Top = 39; _clearRefBtn.Width = 72; _clearRefBtn.Height = 30;
        StyleGhostButton(_clearRefBtn);
        _clearRefBtn.Click += (s, e) => ClearReferences();
        _inputPanel.Controls.Add(_clearRefBtn);

        // 提示词
        _promptBox.Left = 18; _promptBox.Top = 104; _promptBox.Height = 56;
        _promptBox.Multiline = true;
        _promptBox.Watermark = "输入图片生成提示词…";
        StyleTextBox(_promptBox);
        _inputPanel.Controls.Add(_promptBox);

        _generateBtn.Text = "生成";
        _generateBtn.Symbol = 61581;
        _generateBtn.Top = 104; _generateBtn.Width = 176; _generateBtn.Height = 56;
        StylePrimaryButton(_generateBtn);
        _generateBtn.Radius = 28;
        _generateBtn.FillColor = C_ACCENT;
        _generateBtn.FillColor2 = Color.FromArgb(79, 70, 229);
        _generateBtn.FillColorGradient = true;
        _generateBtn.FillColorGradientDirection = FlowDirection.TopDown;
        _generateBtn.Font = new Font("微软雅黑", 12F, FontStyle.Bold);
        _generateBtn.Click += (s, e) => Generate();
        _inputPanel.Controls.Add(_generateBtn);

        // 参数行
        BuildParamsRow();

        _inputPanel.Controls.SetChildIndex(_refHint, _inputPanel.Controls.Count - 1);
        _inputPanel.Resize += (s, e) => LayoutInputBar();
        LayoutInputBar();

        this.Controls.Add(_inputPanel);
        _inputPanel.BringToFront();
    }

    // 输入区随窗口宽度自适应
    private void LayoutInputBar()
    {
        int right = _inputPanel.Width - 18;

        _clearRefBtn.Left = right - _clearRefBtn.Width;
        _removeRefBtn.Left = _clearRefBtn.Left - 8 - _removeRefBtn.Width;
        _addRefBtn.Left = _removeRefBtn.Left - 8 - _addRefBtn.Width;
        _refPanel.Width = _addRefBtn.Left - _refPanel.Left - 10;
        _refHint.Left = _refPanel.Left;
        _refHint.Top = _refPanel.Top;
        _refHint.Width = _refPanel.Width;
        _refHint.Height = _refPanel.Height;

        _generateBtn.Width = 176;
        _generateBtn.Left = right - _generateBtn.Width;
        _promptBox.Width = _generateBtn.Left - _promptBox.Left - 10;
    }

    private void BuildParamsRow()
    {
        var y = 170;
        _inputPanel.Controls.Add(MakeParamLabel("尺寸", 61509, 18, y));

        AddSizePreset(_btnAuto, "自动", 76, y);
        AddSizePreset(_btn1k, "1K", 128, y);
        AddSizePreset(_btn2k, "2K", 180, y);
        AddSizePreset(_btn4k, "4K", 232, y);
        SelectSizePresetAppearance(_btnAuto, true);

        _sizeCombo.Left = 300; _sizeCombo.Top = y - 1; _sizeCombo.Width = 150; _sizeCombo.Height = 32;
        _sizeCombo.Items.AddRange(new object[] { "自动", "1024x1024", "1536x1024", "1024x1536", "1280x720" });
        _sizeCombo.SelectedIndex = 0;
        StyleCombo(_sizeCombo);
        _inputPanel.Controls.Add(_sizeCombo);

        _inputPanel.Controls.Add(MakeParamLabel("质量", 61500, 462, y));
        _qualityCombo.Left = 520; _qualityCombo.Top = y - 1; _qualityCombo.Width = 118; _qualityCombo.Height = 32;
        _qualityCombo.Items.AddRange(new object[] { "auto", "low", "medium", "high", "xhigh" });
        _qualityCombo.SelectedIndex = 0;
        StyleCombo(_qualityCombo);
        _inputPanel.Controls.Add(_qualityCombo);

        _inputPanel.Controls.Add(MakeParamLabel("格式", 61708, 650, y));
        _formatCombo.Left = 706; _formatCombo.Top = y - 1; _formatCombo.Width = 108; _formatCombo.Height = 32;
        _formatCombo.Items.AddRange(new object[] { "png", "jpeg", "webp" });
        _formatCombo.SelectedIndex = 0;
        StyleCombo(_formatCombo);
        _inputPanel.Controls.Add(_formatCombo);

        _inputPanel.Controls.Add(MakeParamLabel("张数", 61664, 826, y));
        _countSpin.Left = 882; _countSpin.Top = y; _countSpin.Width = 70; _countSpin.Height = 28;
        _countSpin.Minimum = 1; _countSpin.Maximum = 9;
        _countSpin.Value = 1;
        _countSpin.Font = new Font("微软雅黑", 9F);
        _countSpin.ForeColor = C_TEXT;
        _countSpin.BackColor = C_WHITE;
        _countSpin.BorderStyle = BorderStyle.FixedSingle;
        _inputPanel.Controls.Add(_countSpin);

        _inputPanel.Controls.Add(MakeParamLabel("透明", 61634, 968, y));
        _transparentSwitch.Left = 1020; _transparentSwitch.Top = y - 1; _transparentSwitch.Width = 60; _transparentSwitch.Height = 30;
        _transparentSwitch.ActiveColor = C_ACCENT;
        _transparentSwitch.InActiveColor = Color.FromArgb(203, 213, 225);
        _transparentSwitch.ButtonColor = C_WHITE;
        _inputPanel.Controls.Add(_transparentSwitch);
    }

    private static UISymbolLabel MakeParamLabel(string text, int symbol, int left, int y)
    {
        return new UISymbolLabel
        {
            Text = text,
            Symbol = symbol,
            Left = left,
            Top = y + 3,
            Width = 52,
            Height = 24,
            Style = UIStyle.Custom,
            ForeColor = C_TEXT_SEC,
            SymbolColor = C_TEXT_SEC,
            Font = new Font("微软雅黑", 9F)
        };
    }

    private void AddSizePreset(UIButton btn, string text, int left, int top)
    {
        btn.Text = text;
        btn.Left = left; btn.Top = top; btn.Width = 48; btn.Height = 28;
        btn.Style = UIStyle.Custom;
        btn.AutoSize = false;
        StyleGhostUIButton(btn);
        btn.Click += (s, e) => SelectSizePreset(text, btn);
        _inputPanel.Controls.Add(btn);
    }

    private void BuildGallery()
    {
        _galleryPanel.Dock = DockStyle.Fill;
        _galleryPanel.Style = UIStyle.Custom;
        _galleryPanel.FillColor = C_BG;
        _galleryPanel.RectSides = ToolStripStatusLabelBorderSides.None;
        _galleryPanel.Padding = new Padding(12);

        _galleryFlow.Dock = DockStyle.Fill;
        _galleryFlow.AutoScroll = true;
        _galleryFlow.WrapContents = true;
        _galleryFlow.Padding = new Padding(4);
        _galleryFlow.BackColor = C_BG;
        _galleryPanel.Controls.Add(_galleryFlow);

        _emptyLabel.Text = "暂无任务，输入提示词并点击「生成」";
        _emptyLabel.Symbol = 61576;
        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyLabel.Style = UIStyle.Custom;
        _emptyLabel.ForeColor = C_TEXT_MUTED;
        _emptyLabel.Font = new Font("微软雅黑", 12F);
        _galleryPanel.Controls.Add(_emptyLabel);

        this.Controls.Add(_galleryPanel);
        _galleryPanel.BringToFront();
    }

    // ===== 配置 =====
    private void ReloadProfiles()
    {
        _profileCombo.Items.Clear();
        var settings = AppServices.Settings.Settings;
        foreach (var p in settings.Profiles)
            _profileCombo.Items.Add($"{p.Name}  [{p.Provider}]");
        var active = settings.ActiveProfile;
        if (active != null)
        {
            var idx = settings.Profiles.FindIndex(x => x.Id == active.Id);
            if (idx >= 0) _profileCombo.SelectedIndex = idx;
        }
    }

    private void ProfileCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var idx = _profileCombo.SelectedIndex;
        if (idx < 0) return;
        AppServices.Settings.Settings.ActiveProfileId = AppServices.Settings.Settings.Profiles[idx].Id;
        AppServices.Settings.Save();
    }

    // ===== 参考图 =====
    private void AddReference()
    {
        if (_referenceImages.Count >= 16)
        {
            MessageBox.Show(this, "最多添加 16 张参考图。", "提示");
            return;
        }
        using var dlg = new OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        foreach (var file in dlg.FileNames)
        {
            if (_referenceImages.Count >= 16) break;
            _referenceImages.Add(File.ReadAllBytes(file));
        }
        RenderReferenceThumbs();
    }

    private void RemoveLastReference()
    {
        if (_referenceImages.Count == 0) return;
        _referenceImages.RemoveAt(_referenceImages.Count - 1);
        RenderReferenceThumbs();
    }

    private void ClearReferences()
    {
        _referenceImages.Clear();
        RenderReferenceThumbs();
    }

    private void RenderReferenceThumbs()
    {
        _refHint.Visible = _referenceImages.Count == 0;
        _refPanel.Controls.Clear();
        foreach (var bytes in _referenceImages)
        {
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);
            var pb = new PictureBox
            {
                Width = 64, Height = 64,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = (Image)img.Clone(),
                Margin = new Padding(6),
                BackColor = C_WHITE
            };
            _refPanel.Controls.Add(pb);
        }
    }

    // ===== 生成 =====
    private async void Generate()
    {
        if (_generating) return;
        var prompt = _promptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            MessageBox.Show(this, "请输入提示词。", "提示");
            return;
        }

        var profile = AppServices.Settings.GetActiveProfile();
        var settings = AppServices.Settings.Settings;

        var req = new ImageApiRequest
        {
            Settings = settings,
            Prompt = prompt,
            ReferenceImages = new List<byte[]>(_referenceImages),
            Params = BuildParams(),
            NativeTransparent = _transparentSwitch.Active
        };

        var task = BuildTask(req, profile);
        AppServices.Tasks.SaveTask(task);
        AddCard(task);

        _generating = true;
        _generateBtn.Enabled = false;
        try
        {
            // 流式回调（界面线程不更新，仅收集）
            var result = await AppServices.Api.GenerateAsync(req, CancellationToken.None);

            await ProcessResult(task, result, req, profile, settings);
        }
        catch (Exception ex)
        {
            task.Status = TaskStatus.Error;
            task.Error = ex.Message;
            task.FinishedAt = DateTime.UtcNow;
            AppServices.Tasks.SaveTask(task);
            UpdateCard(task);
            MessageBox.Show(this, ex.Message, "生成失败");
        }
        finally
        {
            _generating = false;
            _generateBtn.Enabled = true;
            if (settings.ClearInputAfterSubmit) _promptBox.Text = "";
            else settings.LastPrompt = prompt;
            AppServices.Settings.Save();
        }
    }

    private TaskParams BuildParams()
    {
        var format = GetComboText(_formatCombo, "png");
        var size = GetComboText(_sizeCombo, "auto");
        if (size == "自动") size = "auto";
        return new TaskParams
        {
            Size = ImageSizeHelper.NormalizeImageSize(size),
            Quality = GetComboText(_qualityCombo, "auto"),
            OutputFormat = format,
            Moderation = "auto",
            N = (int)_countSpin.Value,
            TransparentOutput = _transparentSwitch.Active
        };
    }

    private TaskRecord BuildTask(ImageApiRequest req, ApiProfile profile)
    {
        return new TaskRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Prompt = req.Prompt,
            Params = req.Params,
            ApiProvider = profile.Provider,
            ApiProfileId = profile.Id,
            ApiProfileName = profile.Name,
            ApiMode = profile.ApiMode,
            ApiModel = profile.Model,
            Status = TaskStatus.Running,
            CreatedAt = DateTime.UtcNow,
            SourceMode = "gallery"
        };
    }

    private async Task ProcessResult(TaskRecord task, ImageApiResult result, ImageApiRequest req, ApiProfile profile, AppSettings settings)
    {
        var outputIds = new List<string>();
        var originalIds = new List<string>();
        bool doLocalTransparent = req.Params.TransparentOutput &&
            (profile.TransparentBackgroundMethod == "local" || !req.NativeTransparent);

        foreach (var bytes in result.Images)
        {
            // 透明后处理
            byte[] finalBytes = bytes;
            if (doLocalTransparent)
            {
                using var src = new MemoryStream(bytes);
                using var img = new Bitmap(src);
                using var processed = TransparentImageProcessor.RemoveKeyedBackground(img);
                using var ms = new MemoryStream();
                processed.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                finalBytes = ms.ToArray();
            }

            var stored = AppServices.Images.SaveImage(finalBytes, ImageSource.Generated);
            outputIds.Add(stored.Id);
            if (doLocalTransparent)
            {
                var orig = AppServices.Images.SaveImage(bytes, ImageSource.Generated);
                originalIds.Add(orig.Id);
            }
        }

        task.Status = TaskStatus.Done;
        task.OutputImages = outputIds;
        task.TransparentOriginalImages = originalIds;
        task.RawImageUrls = result.RawImageUrls;
        task.FinishedAt = DateTime.UtcNow;
        task.Elapsed = (long)(task.FinishedAt - task.CreatedAt).Value.TotalMilliseconds;
        if (result.ActualParams != null) { task.ActualParams = result.ActualParams; task.HasActualParams = true; }
        AppServices.Tasks.SaveTask(task);
        UpdateCard(task);
    }

    // ===== 画廊 =====
    private void LoadTasks(string? search = null)
    {
        _galleryFlow.Controls.Clear();
        _cards.Clear();
        _tasks.Clear();

        var tasks = AppServices.Tasks.LoadTasks(500, null, search);
        foreach (var task in tasks)
        {
            _tasks[task.Id] = task;
            AddCard(task);
        }
        UpdateEmptyState();
    }

    private void AddCard(TaskRecord task)
    {
        if (_cards.ContainsKey(task.Id)) return;
        var card = new TaskCardControl(task);
        card.Margin = new Padding(8);
        card.OpenRequested += (s, e) => OpenDetail(task.Id);
        card.RetryRequested += (s, e) => RetryTask(task.Id);
        _cards[task.Id] = card;
        _galleryFlow.Controls.Add(card);
        UpdateEmptyState();
    }

    private void UpdateCard(TaskRecord task)
    {
        if (_cards.TryGetValue(task.Id, out var card)) card.RefreshTask(task);
    }

    private void UpdateEmptyState()
    {
        _emptyLabel.Visible = _galleryFlow.Controls.Count == 0;
    }

    private void OpenDetail(string id)
    {
        if (!_tasks.TryGetValue(id, out var task)) return;
        using var form = new TaskDetailForm(task, AppServices.Images);
        form.ShowDialog(this);
        // 详情页可能删除，刷新手势
        LoadTasks(_searchBox.Text);
    }

    private void RetryTask(string id)
    {
        if (!_tasks.TryGetValue(id, out var task)) return;
        _promptBox.Text = task.Prompt;
        _countSpin.Value = Math.Max(1, task.Params.N);
        SelectCombo(_formatCombo, task.Params.OutputFormat);
        SelectCombo(_qualityCombo, task.Params.Quality);
        MessageBox.Show(this, "已把该任务参数回填到输入区，可修改后重新生成。", "重新生成");
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(AppServices.Settings);
        form.ShowDialog(this);
        ReloadProfiles();
        LoadTasks(_searchBox.Text);
    }

    private void DeleteFailedTasks()
    {
        var list = AppServices.Tasks.LoadTasks(500, TaskStatus.Error);
        foreach (var t in list) AppServices.Tasks.DeleteTask(t.Id);
        LoadTasks(_searchBox.Text);
    }

    private void ClearAll()
    {
        if (MessageBox.Show(this, "确定清空所有任务历史？（不影响已保存的图片文件）", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        AppServices.Tasks.ClearAll();
        LoadTasks(_searchBox.Text);
    }

    // ===== 工具 =====
    private static string GetComboText(UIComboBox combo, string def)
    {
        return combo.SelectedItem?.ToString() ?? def;
    }

    private static void SelectCombo(UIComboBox combo, string value)
    {
        for (var i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i]?.ToString() == value) { combo.SelectedIndex = i; return; }
    }

    private void SelectSizePreset(string tier, UIButton btn)
    {
        var ratio = _sizeCombo.SelectedIndex switch
        {
            1 or 2 or 5 => "1:1",
            3 or 6 => "3:2",
            _ => "16:9"
        };
        var size = tier == "自动" ? "auto" : ImageSizeHelper.CalculateImageSize(tier, ratio) ?? "auto";
        if (tier == "自动") SelectCombo(_sizeCombo, "自动");
        else SelectCombo(_sizeCombo, size);
        // 视觉高亮
        foreach (var b in new[] { _btnAuto, _btn1k, _btn2k, _btn4k })
            SelectSizePresetAppearance(b, b == btn);
    }

    private static void SelectSizePresetAppearance(UIButton btn, bool selected)
    {
        btn.FillColor = selected ? C_ACCENT : C_GHOST;
        btn.FillHoverColor = selected ? C_ACCENT_HOVER : C_GHOST_HOVER;
        btn.FillPressColor = selected ? C_ACCENT_PRESS : C_GHOST_PRESS;
        btn.RectColor = selected ? C_ACCENT : C_BORDER_LT;
        btn.ForeColor = selected ? C_WHITE : C_TEXT_SEC;
    }
}
