using GptImagePlayground.Core.Models;
using Sunny.UI;
using System.Drawing;
using System.Windows.Forms;
using TaskStatus = GptImagePlayground.Core.Models.TaskStatus;

namespace GptImagePlayground.App;

public class ProfileEditForm : UIForm
{
    private readonly ApiProfile _profile;
    private bool _isNew;

    private readonly Panel _content = new();
    private int _y = 12;

    private UITextBox _name = new();
    private UIComboBox _provider = new();
    private UITextBox _baseUrl = new();
    private UITextBox _apiKey = new();
    private UITextBox _model = new();
    private UITextBox _imageGenModel = new();
    private NumericUpDown _timeout = new();
    private UIComboBox _apiMode = new();
    private UIComboBox _reasoning = new();
    private UIComboBox _transparent = new();
    private UISwitch _codexCli = new();
    private UISwitch _apiProxy = new();
    private UISwitch _b64 = new();
    private UISwitch _stream = new();
    private UIComboBox _streamPartial = new();
    private UIButton _okBtn = new();
    private UIButton _cancelBtn = new();

    public ProfileEditForm(ApiProfile profile, bool isNew)
    {
        _profile = profile;
        _isNew = isNew;
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        this.Text = _isNew ? "新建 API 配置" : "编辑 API 配置";
        UiStyle.ApplyForm(this, new Size(620, 700));
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowTitle = true;
        this.MinimumSize = new Size(560, 620);
        this.MaximumSize = new Size(620, 720);
        this.ShowInTaskbar = false;

        // 可滚动内容区（字段较多，保证任意屏幕都能排版）
        _content.Dock = DockStyle.Fill;
        _content.AutoScroll = true;
        _content.BackColor = UiStyle.Bg;
        _content.Padding = new Padding(16, 10, 16, 10);
        this.Controls.Add(_content);

        // 字段区
        AddField("名称", _name);
        AddField("供应商", _provider);
        _provider.Items.AddRange(new object[] { "openai", "sb2api-async", "fal", "custom" });

        AddField("接口地址", _baseUrl, 420, "支持通过查询参数覆盖：?apiUrl=");
        _apiKey.PasswordChar = '*';
        AddField("API Key", _apiKey, 420, "支持通过查询参数覆盖：?apiKey=");
        EnableShowKey(_apiKey);

        AddField("API 接口", _apiMode);
        _apiMode.Items.AddRange(new object[] { "Images API (/v1/images)", "Responses API (/v1/responses)" });

        AddField("模型", _model, 420, "Images API 需要使用 GPT Image 模型，例如 gpt-image-2。支持查询参数覆盖：?model=");
        AddField("图像生成模型", _imageGenModel, 420, "Responses API 的 image_generation 工具模型，留空用 API 默认值。");

        AddField("推理强度", _reasoning);
        _reasoning.Items.AddRange(new object[] { "", "none", "minimal", "low", "medium", "high", "xhigh", "max" });

        AddField("透明背景", _transparent);
        _transparent.Items.AddRange(new object[] { "api", "local" });

        AddField("请求超时（秒）", _timeout, 160);
        _timeout.Minimum = 10; _timeout.Maximum = 3600; _timeout.Value = 600;

        AddField("请求中间步骤图像数", _streamPartial, 160, "对应 partial_images 参数（0-3），建议 2-3 防超时断开；0 则无中间图。");
        _streamPartial.Items.AddRange(new object[] { "0 张", "1 张", "2 张", "3 张" });

        AddSwitch("流式传输", _stream, "并非所有服务商/网关都支持流式；生成异常时请关闭。官方仅支持单图流式，多图会被拆分。");

        AddSwitch("返回 Base64 图片数据", _b64, "追加 response_format: b64_json，接口直接返回 Base64 而非 URL。");
        AddSwitch("Codex CLI 兼容模式", _codexCli, "应用 Codex CLI 实际支持的参数，支持查询参数覆盖：codexCli=true。");
        AddSwitch("API 代理", _apiProxy);

        // 底部按钮（固定，不随内容滚动）
        var btnBar = new UIPanel { Dock = DockStyle.Bottom, Height = 64, Style = UIStyle.Custom };
        btnBar.FillColor = UiStyle.White;
        btnBar.RectColor = UiStyle.BorderLt;
        btnBar.RectSides = ToolStripStatusLabelBorderSides.Top;
        this.Controls.Add(btnBar);

        _okBtn.Text = "保存";
        _okBtn.Left = btnBar.Width - 262; _okBtn.Top = 13; _okBtn.Width = 120; _okBtn.Height = 40;
        _okBtn.Style = UIStyle.Custom;
        _okBtn.FillColor = UiStyle.Accent;
        _okBtn.FillHoverColor = UiStyle.AccentHover;
        _okBtn.FillPressColor = UiStyle.AccentPress;
        _okBtn.RectColor = UiStyle.Accent;
        _okBtn.ForeColor = UiStyle.White;
        _okBtn.Radius = 20;
        _okBtn.RadiusSides = UICornerRadiusSides.All;
        _okBtn.Font = new Font("微软雅黑", 10F);
        _okBtn.Click += (s, e) => { if (SaveValues()) DialogResult = DialogResult.OK; };
        btnBar.Controls.Add(_okBtn);

        _cancelBtn.Text = "取消";
        _cancelBtn.Left = btnBar.Width - 132; _cancelBtn.Top = 13; _cancelBtn.Width = 120; _cancelBtn.Height = 40;
        _cancelBtn.Style = UIStyle.Custom;
        _cancelBtn.FillColor = UiStyle.Ghost;
        _cancelBtn.FillHoverColor = UiStyle.GhostHover;
        _cancelBtn.FillPressColor = UiStyle.GhostPress;
        _cancelBtn.RectColor = UiStyle.BorderLt;
        _cancelBtn.ForeColor = UiStyle.TextSec;
        _cancelBtn.Radius = 20;
        _cancelBtn.RadiusSides = UICornerRadiusSides.All;
        _cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;
        btnBar.Controls.Add(_cancelBtn);

        this.AcceptButton = _okBtn;
        this.CancelButton = _cancelBtn;
    }

    // ===== 布局辅助 =====
    // 在控件上追加查看/隐藏明文 Key 的小眼睛按钮
    private static void EnableShowKey(UITextBox box)
    {
        box.ShowButton = true;
        box.ButtonWidth = 34;
        box.ButtonSymbol = 61550; // fa-eye：点击显示明文
        box.ButtonSymbolSize = 20;
        box.ButtonFillColor = UiStyle.Ghost;
        box.ButtonForeColor = UiStyle.TextSec;
        box.ButtonRectColor = Color.Transparent;
        box.ButtonClick += (s, e) =>
        {
            var shown = box.PasswordChar == '\0';
            box.PasswordChar = shown ? '*' : '\0';
            box.ButtonSymbol = shown ? 61550 : 61552; // 显示时切到 fa-eye-slash
        };
    }

    private void AddField(string label, Control c, int controlWidth = 420, string? hint = null)
    {
        _content.Controls.Add(MakeLabel(label, 22, _y + 4));
        c.Left = 162; c.Top = _y; c.Width = controlWidth; c.Height = 28;
        StyleControl(c);
        _content.Controls.Add(c);
        if (hint == null) _y += 40;
        else { _content.Controls.Add(MakeHint(hint, 22, _y + 32)); _y += 70; }
    }

    private void AddSwitch(string label, UISwitch sw, string? hint = null)
    {
        _content.Controls.Add(MakeLabel(label, 22, _y + 4));
        sw.Left = 162; sw.Top = _y - 2; sw.Width = 72; sw.Height = 30;
        sw.ActiveColor = UiStyle.Accent;
        sw.InActiveColor = Color.FromArgb(203, 213, 225);
        sw.ButtonColor = UiStyle.White;
        _content.Controls.Add(sw);
        if (hint == null) _y += 40;
        else { _content.Controls.Add(MakeHint(hint, 22, _y + 32)); _y += 70; }
    }

    private static UILabel MakeLabel(string text, int left, int top)
    {
        return new UILabel
        {
            Text = text,
            Left = left,
            Top = top,
            Width = 130,
            Height = 22,
            Style = UIStyle.Custom,
            ForeColor = UiStyle.TextSec,
            Font = new Font("微软雅黑", 9F)
        };
    }

    private static UILabel MakeHint(string text, int left, int top)
    {
        return new UILabel
        {
            Text = text,
            Left = left,
            Top = top,
            Width = 540,
            Height = 30,
            Style = UIStyle.Custom,
            ForeColor = UiStyle.TextMuted,
            Font = new Font("微软雅黑", 8F)
        };
    }

    private static void StyleControl(Control c)
    {
        switch (c)
        {
            case UITextBox tb: UiStyle.StyleTextBox(tb); break;
            case UIComboBox cb: UiStyle.StyleCombo(cb); break;
            case NumericUpDown n:
                n.Font = new Font("微软雅黑", 9F);
                n.BackColor = UiStyle.White;
                n.ForeColor = UiStyle.Text;
                n.BorderStyle = BorderStyle.FixedSingle;
                break;
        }
    }

    // ===== 数据读写 =====
    private void LoadValues()
    {
        _name.Text = _profile.Name;
        SelectCombo(_provider, _profile.Provider);
        _baseUrl.Text = _profile.BaseUrl;
        _apiKey.Text = _profile.ApiKey;
        _model.Text = _profile.Model;
        _imageGenModel.Text = _profile.ImageGenerationModel;
        _timeout.Value = Math.Clamp(_profile.Timeout, 10, 3600);
        SelectCombo(_apiMode, _profile.ApiMode == ApiMode.Responses ? "Responses API (/v1/responses)" : "Images API (/v1/images)");
        SelectCombo(_reasoning, _profile.ReasoningEffort);
        SelectCombo(_transparent, _profile.TransparentBackgroundMethod);
        _streamPartial.SelectedIndex = Math.Clamp(_profile.StreamPartialImages, 0, 3);
        _codexCli.Active = _profile.CodexCli;
        _apiProxy.Active = _profile.ApiProxy;
        _b64.Active = _profile.ResponseFormatB64Json;
        _stream.Active = _profile.StreamImages;
    }

    private bool SaveValues()
    {
        _profile.Name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(_profile.Name))
        {
            MessageBox.Show("请填写配置名称。");
            return false;
        }
        _profile.Provider = GetCombo(_provider, "openai");
        _profile.BaseUrl = _baseUrl.Text.Trim();
        _profile.ApiKey = _apiKey.Text.Trim();
        _profile.Model = _model.Text.Trim();
        _profile.ImageGenerationModel = _imageGenModel.Text.Trim();
        _profile.Timeout = (int)_timeout.Value;
        _profile.ApiMode = GetCombo(_apiMode, "Images API (/v1/images)") == "Responses API (/v1/responses)" ? ApiMode.Responses : ApiMode.Images;
        _profile.ReasoningEffort = GetCombo(_reasoning, "");
        _profile.TransparentBackgroundMethod = GetCombo(_transparent, "api");
        _profile.StreamPartialImages = _streamPartial.SelectedIndex < 0 ? 1 : _streamPartial.SelectedIndex;
        _profile.CodexCli = _codexCli.Active;
        _profile.ApiProxy = _apiProxy.Active;
        _profile.ResponseFormatB64Json = _b64.Active;
        _profile.StreamImages = _stream.Active;
        return true;
    }

    private static string GetCombo(UIComboBox c, string def) => c.SelectedItem?.ToString() ?? def;
    private static void SelectCombo(UIComboBox c, string value)
    {
        for (var i = 0; i < c.Items.Count; i++)
            if (c.Items[i]?.ToString() == value) { c.SelectedIndex = i; return; }
    }
}
