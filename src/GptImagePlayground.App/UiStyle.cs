using Sunny.UI;
using System.Drawing;
using System.Windows.Forms;

namespace GptImagePlayground.App;

/// <summary>统一的现代浅色配色与常规控件样式，供各窗口复用。</summary>
internal static class UiStyle
{
    // ===== 配色 =====
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);
    public static readonly Color AccentHover = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentPress = Color.FromArgb(29, 78, 216);
    public static readonly Color AccentLight = Color.FromArgb(219, 234, 254);
    public static readonly Color AccentDisable = Color.FromArgb(147, 197, 253);
    public static readonly Color White = Color.White;
    public static readonly Color Bg = Color.FromArgb(248, 250, 252);
    public static readonly Color Panel = Color.FromArgb(252, 253, 255);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color BorderLt = Color.FromArgb(238, 242, 247);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color TextSec = Color.FromArgb(100, 116, 140);
    public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
    public static readonly Color Ghost = Color.FromArgb(241, 245, 249);
    public static readonly Color GhostHover = Color.FromArgb(226, 232, 240);
    public static readonly Color GhostPress = Color.FromArgb(203, 213, 225);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);

    // ===== 常规控件样式 =====
    public static void ApplyForm(UIForm form, Size size)
    {
        form.Style = UIStyle.Custom;
        form.TitleColor = Color.FromArgb(250, 250, 252);
        form.TitleForeColor = Text;
        form.BackColor = Bg;
        form.Size = size;

        // 让标题栏右上角的最小化/最大化/关闭按钮清晰可见
        form.ControlBox = true;
        form.MinimizeBox = true;
        form.MaximizeBox = true;
        form.ControlBoxForeColor = Color.FromArgb(51, 65, 85);
        form.ControlBoxFillHoverColor = Color.FromArgb(226, 232, 240);
        form.ControlBoxCloseFillHoverColor = Color.FromArgb(220, 38, 38);
    }

    public static void StyleCombo(UIComboBox combo)
    {
        combo.FillColor = White;
        combo.RectColor = Border;
        combo.ForeColor = Text;
        combo.Radius = 8;
        combo.RadiusSides = UICornerRadiusSides.All;
        combo.ItemFillColor = White;
        combo.ItemForeColor = Text;
        combo.ItemSelectBackColor = Accent;
        combo.ItemSelectForeColor = White;
        combo.ItemHoverColor = AccentLight;
        combo.ItemRectColor = Border;
    }

    public static void StyleTextBox(UITextBox box)
    {
        box.FillColor = White;
        box.RectColor = Border;
        box.ForeColor = Text;
        box.Radius = 8;
        box.RadiusSides = UICornerRadiusSides.All;
        box.WatermarkColor = TextMuted;
        box.WatermarkActiveColor = TextSec;
    }

    public static void StyleGhostButton(UISymbolButton btn)
    {
        btn.FillColor = Ghost;
        btn.FillHoverColor = GhostHover;
        btn.FillPressColor = GhostPress;
        btn.FillDisableColor = Ghost;
        btn.RectColor = BorderLt;
        btn.RectHoverColor = Border;
        btn.RectPressColor = Border;
        btn.ForeColor = TextSec;
        btn.ForeHoverColor = Text;
        btn.ForePressColor = Text;
        btn.SymbolColor = TextSec;
        btn.Radius = 16;
        btn.RadiusSides = UICornerRadiusSides.All;
        btn.Font = new Font("微软雅黑", 9F);
    }

    public static void StylePrimaryButton(UISymbolButton btn)
    {
        btn.FillColor = Accent;
        btn.FillHoverColor = AccentHover;
        btn.FillPressColor = AccentPress;
        btn.FillDisableColor = AccentDisable;
        btn.RectColor = Accent;
        btn.RectHoverColor = AccentHover;
        btn.RectPressColor = AccentPress;
        btn.ForeColor = White;
        btn.ForeHoverColor = White;
        btn.ForePressColor = White;
        btn.SymbolColor = White;
        btn.Radius = 16;
        btn.RadiusSides = UICornerRadiusSides.All;
        btn.Font = new Font("微软雅黑", 9.5F);
    }

    public static void StyleGhostUIButton(UIButton btn)
    {
        btn.FillColor = Ghost;
        btn.FillHoverColor = GhostHover;
        btn.FillPressColor = GhostPress;
        btn.RectColor = BorderLt;
        btn.RectHoverColor = Border;
        btn.RectPressColor = Border;
        btn.ForeColor = TextSec;
        btn.ForeHoverColor = Text;
        btn.ForePressColor = Text;
        btn.Radius = 14;
        btn.RadiusSides = UICornerRadiusSides.All;
        btn.Font = new Font("微软雅黑", 8.5F);
    }

    public static UISymbolLabel MakeLabel(string text, int symbol, int left, int y, int width = 52, float size = 9F, Color? fore = null)
    {
        return new UISymbolLabel
        {
            Text = text,
            Symbol = symbol,
            Left = left,
            Top = y,
            Width = width,
            Height = 24,
            Style = UIStyle.Custom,
            ForeColor = fore ?? TextSec,
            SymbolColor = fore ?? TextSec,
            Font = new Font("微软雅黑", size)
        };
    }
}
