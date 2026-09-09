using Sunny.UI;

namespace GptImagePlayground.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 初始化应用服务（数据目录可在设置中更改，默认在 LocalAppData）
        AppServices.Initialize();

        // 设置 SunnyUI 主题
        UIStyles.SetStyle(UIStyle.Custom);

        Application.Run(new MainForm());
    }
}
