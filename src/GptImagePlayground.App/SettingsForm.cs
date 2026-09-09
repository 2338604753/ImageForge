using GptImagePlayground.Core.Models;
using GptImagePlayground.Core.Services;
using Sunny.UI;
using System.Drawing;
using System.Windows.Forms;

namespace GptImagePlayground.App;

public class SettingsForm : UIForm
{
    private readonly SettingsService _service;

    private ListView _profileList = new();
    private UILabel _listTitle = new();
    private UISymbolButton _btnNew = new();
    private UISymbolButton _btnEdit = new();
    private UISymbolButton _btnDelete = new();
    private UISymbolButton _btnCopy = new();
    private UISymbolButton _btnDefault = new();

    private UITextBox _dataDirBox = new();
    private UISymbolButton _btnBrowseDir = new();
    private UISymbolButton _btnApplyDir = new();

    private UICheckBox _chkClearInput = new();
    private UICheckBox _chkPersistInput = new();
    private UICheckBox _chkReuseTask = new();
    private UICheckBox _chkAllowRewrite = new();
    private UICheckBox _chkNotify = new();

    private UIButton _btnSave = new();
    private UIButton _btnClose = new();

    public SettingsForm(SettingsService service)
    {
        _service = service;
        BuildLayout();
        ReloadList();
        LoadHabits();
    }

    private void BuildLayout()
    {
        this.Text = "设置";
        UiStyle.ApplyForm(this, new Size(840, 660));
        this.ShowTitle = true;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = this.Size;
        this.MaximumSize = this.Size;
        this.ShowInTaskbar = false;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;

        _listTitle.Text = "API 配置";
        _listTitle.Left = 18; _listTitle.Top = 20; _listTitle.Width = 120; _listTitle.Height = 24;
        _listTitle.Style = UIStyle.Custom;
        _listTitle.ForeColor = UiStyle.Text;
        _listTitle.Font = new Font("微软雅黑", 10.5F, FontStyle.Bold);
        this.Controls.Add(_listTitle);

        _profileList.Left = 18; _profileList.Top = 48; _profileList.Width = 470; _profileList.Height = 440;
        _profileList.View = View.Details;
        _profileList.FullRowSelect = true;
        _profileList.GridLines = false;
        _profileList.HideSelection = false;
        _profileList.BackColor = UiStyle.White;
        _profileList.ForeColor = UiStyle.Text;
        _profileList.BorderStyle = BorderStyle.FixedSingle;
        _profileList.Columns.Add("名称", 220);
        _profileList.Columns.Add("类型", 110);
        _profileList.Columns.Add("默认", 60);
        _profileList.MultiSelect = false;
        _profileList.DoubleClick += (s, e) => EditSelected();
        this.Controls.Add(_profileList);

        var btnX = 500;
        var btnY = 48;
        StackButton(_btnNew, "新建", 61556, btnX, btnY);
        _btnNew.Click += (s, e) => NewProfile();
        StackButton(_btnEdit, "编辑", 61553, btnX, btnY += 40);
        _btnEdit.Click += (s, e) => EditSelected();
        StackButton(_btnCopy, "复制", 61567, btnX, btnY += 40);
        _btnCopy.Click += (s, e) => CopySelected();
        StackButton(_btnDefault, "设为默认", 61626, btnX, btnY += 40);
        _btnDefault.Click += (s, e) => SetDefault();
        StackButton(_btnDelete, "删除", 61571, btnX, btnY += 40);
        _btnDelete.Click += (s, e) => DeleteSelected();

        // ===== 存储设置 =====
        var storageLabel = new UILabel { Text = "数据存储目录", Left = 520, Top = 250, Width = 130, Height = 22, Style = UIStyle.Custom };
        this.Controls.Add(storageLabel);
        _dataDirBox.Left = 520; _dataDirBox.Top = 276; _dataDirBox.Width = 240; _dataDirBox.Height = 28;
        _dataDirBox.Text = _service.DataDirectory;
        _dataDirBox.ReadOnly = false;
        UiStyle.StyleTextBox(_dataDirBox);
        this.Controls.Add(_dataDirBox);
        _btnBrowseDir.Text = "浏览"; _btnBrowseDir.Symbol = 61577;
        _btnBrowseDir.Left = 766; _btnBrowseDir.Top = 276; _btnBrowseDir.Width = 56; _btnBrowseDir.Height = 28;
        UiStyle.StyleGhostButton(_btnBrowseDir);
        _btnBrowseDir.Click += (s, e) => BrowseDir();
        this.Controls.Add(_btnBrowseDir);
        _btnApplyDir.Text = "生效并迁移"; _btnApplyDir.Symbol = 61579;
        _btnApplyDir.Left = 520; _btnApplyDir.Top = 310; _btnApplyDir.Width = 120; _btnApplyDir.Height = 30;
        UiStyle.StyleGhostButton(_btnApplyDir);
        _btnApplyDir.Click += (s, e) => ApplyDir();
        this.Controls.Add(_btnApplyDir);
        var dirTip = new UILabel { Text = "更换目录会迁移已保存的图片与历史。", Left = 520, Top = 346, Width = 260, Height = 20, Style = UIStyle.Custom, ForeColor = UiStyle.TextMuted, Font = new Font("微软雅黑", 8F) };
        this.Controls.Add(dirTip);

        // ===== 习惯配置 =====
        var habitLabel = new UILabel { Text = "习惯配置", Left = 520, Top = 380, Width = 120, Height = 22, Style = UIStyle.Custom, ForeColor = UiStyle.Text, Font = new Font("微软雅黑", 10.5F, FontStyle.Bold) };
        this.Controls.Add(habitLabel);
        var hx = 520;
        var hy = 406;
        _chkClearInput.Text = "提交后清空输入";
        _chkPersistInput.Text = "重启保留输入";
        _chkReuseTask.Text = "复用任务 API 配置";
        _chkAllowRewrite.Text = "允许改写提示词";
        _chkNotify.Text = "完成通知";
        AddCheck(_chkClearInput, hx, hy);
        AddCheck(_chkPersistInput, hx, hy += 28);
        AddCheck(_chkReuseTask, hx, hy += 28);
        AddCheck(_chkAllowRewrite, hx, hy += 28);
        AddCheck(_chkNotify, hx, hy += 28);

        // 底部
        _btnSave.Text = "保存";
        _btnSave.Left = 560; _btnSave.Top = 574; _btnSave.Width = 120; _btnSave.Height = 40;
        _btnSave.Style = UIStyle.Custom;
        _btnSave.FillColor = UiStyle.Accent;
        _btnSave.FillHoverColor = UiStyle.AccentHover;
        _btnSave.FillPressColor = UiStyle.AccentPress;
        _btnSave.RectColor = UiStyle.Accent;
        _btnSave.ForeColor = UiStyle.White;
        _btnSave.Radius = 20;
        _btnSave.RadiusSides = UICornerRadiusSides.All;
        _btnSave.Font = new Font("微软雅黑", 10F);
        _btnSave.Click += (s, e) => SaveAll();
        this.Controls.Add(_btnSave);
        _btnClose.Text = "关闭";
        _btnClose.Left = 694; _btnClose.Top = 574; _btnClose.Width = 120; _btnClose.Height = 40;
        _btnClose.Style = UIStyle.Custom;
        _btnClose.FillColor = UiStyle.Ghost;
        _btnClose.FillHoverColor = UiStyle.GhostHover;
        _btnClose.FillPressColor = UiStyle.GhostPress;
        _btnClose.RectColor = UiStyle.BorderLt;
        _btnClose.ForeColor = UiStyle.TextSec;
        _btnClose.Radius = 20;
        _btnClose.RadiusSides = UICornerRadiusSides.All;
        _btnClose.Click += (s, e) => DialogResult = DialogResult.Cancel;
        this.Controls.Add(_btnClose);
    }

    private void StackButton(UISymbolButton btn, string text, int symbol, int x, int y)
    {
        btn.Text = text;
        btn.Symbol = symbol;
        btn.Left = x; btn.Top = y; btn.Width = 100; btn.Height = 32;
        UiStyle.StyleGhostButton(btn);
        this.Controls.Add(btn);
    }

    private void AddCheck(UICheckBox chk, int x, int y)
    {
        chk.Left = x; chk.Top = y; chk.Width = 180; chk.Height = 24;
        chk.ForeColor = UiStyle.Text;
        this.Controls.Add(chk);
    }

    private void ReloadList()
    {
        _profileList.Items.Clear();
        foreach (var p in _service.Settings.Profiles)
        {
            var item = new ListViewItem(new[] { p.Name, LabelProvider(p.Provider), p.IsDefault ? "●" : "" });
            item.Tag = p;
            _profileList.Items.Add(item);
        }
        var active = _service.Settings.ActiveProfile;
        if (active != null)
        {
            for (var i = 0; i < _profileList.Items.Count; i++)
                if (((ApiProfile)_profileList.Items[i].Tag!).Id == active.Id) { _profileList.Items[i].Selected = true; break; }
        }
    }

    private static string LabelProvider(string provider) => provider switch
    {
        "openai" => "OpenAI 兼容",
        "sb2api-async" => "sub2api 异步",
        "fal" => "fal.ai",
        "custom" => "自定义",
        _ => provider
    };

    private ApiProfile? GetSelected()
    {
        return _profileList.SelectedItems.Count > 0 ? (ApiProfile)_profileList.SelectedItems[0].Tag! : null;
    }

    private void NewProfile()
    {
        var p = new ApiProfile { Name = "新配置", Provider = "openai", Model = "gpt-image-2.5-sunburst", Timeout = 600, ApiMode = ApiMode.Images };
        using var form = new ProfileEditForm(p, true);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _service.Settings.Profiles.Add(p);
            if (string.IsNullOrEmpty(_service.Settings.ActiveProfileId)) _service.Settings.ActiveProfileId = p.Id;
            _service.Save();
            ReloadList();
        }
    }

    private void EditSelected()
    {
        var p = GetSelected();
        if (p == null) { MessageBox.Show("请先选择一条配置。"); return; }
        using var form = new ProfileEditForm(p, false);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _service.Save();
            ReloadList();
        }
    }

    private void DeleteSelected()
    {
        var p = GetSelected();
        if (p == null) { MessageBox.Show("请先选择一条配置。"); return; }
        if (_service.Settings.Profiles.Count <= 1)
        {
            MessageBox.Show("至少保留一条配置。");
            return;
        }
        if (MessageBox.Show($"确定删除配置「{p.Name}」？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        _service.Settings.Profiles.Remove(p);
        if (_service.Settings.ActiveProfileId == p.Id)
            _service.Settings.ActiveProfileId = _service.Settings.Profiles[0].Id;
        _service.Save();
        ReloadList();
    }

    private void CopySelected()
    {
        var p = GetSelected();
        if (p == null) { MessageBox.Show("请先选择一条配置。"); return; }
        var copy = p.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = p.Name + "（副本）";
        copy.IsDefault = false;
        _service.Settings.Profiles.Add(copy);
        _service.Save();
        ReloadList();
    }

    private void SetDefault()
    {
        var p = GetSelected();
        if (p == null) { MessageBox.Show("请先选择一条配置。"); return; }
        foreach (var x in _service.Settings.Profiles) x.IsDefault = false;
        p.IsDefault = true;
        _service.Save();
        ReloadList();
    }

    private void BrowseDir()
    {
        using var dlg = new FolderBrowserDialog { Description = "选择数据存储目录", SelectedPath = _dataDirBox.Text };
        if (dlg.ShowDialog(this) == DialogResult.OK) _dataDirBox.Text = dlg.SelectedPath;
    }

    private void ApplyDir()
    {
        var dir = _dataDirBox.Text.Trim();
        if (string.IsNullOrEmpty(dir)) return;
        try
        {
            if (_service.ChangeDataDirectory(dir))
            {
                _service.Save();
                _dataDirBox.Text = _service.DataDirectory;
                MessageBox.Show("数据目录已更新并完成迁移。");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("目录切换失败：" + ex.Message);
        }
    }

    private void LoadHabits()
    {
        var s = _service.Settings;
        _chkClearInput.Checked = s.ClearInputAfterSubmit;
        _chkPersistInput.Checked = s.PersistInputOnRestart;
        _chkReuseTask.Checked = s.ReuseTaskApiProfileTemporarily;
        _chkAllowRewrite.Checked = s.AllowPromptRewrite;
        _chkNotify.Checked = s.TaskCompletionNotification;
    }

    private void SaveAll()
    {
        var s = _service.Settings;
        s.ClearInputAfterSubmit = _chkClearInput.Checked;
        s.PersistInputOnRestart = _chkPersistInput.Checked;
        s.ReuseTaskApiProfileTemporarily = _chkReuseTask.Checked;
        s.AllowPromptRewrite = _chkAllowRewrite.Checked;
        s.TaskCompletionNotification = _chkNotify.Checked;
        _service.Save();
        MessageBox.Show("设置已保存。");
        DialogResult = DialogResult.OK;
    }
}
