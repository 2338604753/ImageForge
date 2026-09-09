# ImageForge

> 多供应商 AI 图像生成桌面工作台 —— 把创意交给模型，把作品留在本地。

**ImageForge** 是一款基于 **.NET 8 + WinForms（SunnyUI）** 的桌面 AI 图像生成与编辑应用。
它直连 OpenAI 兼容接口，提供文本生图、参考图编辑、批量绘制、透明背景、流式传输与本地任务管理，让你的 AI 创作更高效、更可控。

- **UI 库**：SunnyUI（免费、开源、国内使用广泛）
- **目标框架**：.NET 8（net8.0-windows）
- **存储**：SQLite（任务历史）+ 本地图片文件（SHA-256 去重）+ settings.json
- **图片处理**：System.Drawing（缩略图、透明背景本地抠图）

## 功能亮点

- **多供应商接入**：OpenAI 兼容、sub2api 异步、fal.ai、自定义供应商，多套配置一键切换
- **双 API 模式**：Images API（`images/generations`、`images/edits`）与 Responses API（`/responses` + `image_generation` 工具）
- **丰富的生成参数**：1K/2K/4K 尺寸预设、质量、格式、张数（1–9）、透明背景（API 原生 / 本地智能抠图）
- **参考图 / 掩码编辑**：上传参考图与遮罩进行图生图 / 局部重绘
- **流式传输**：SSE 解析，实时接收中间步骤图像
- **本地任务管理**：历史持久化、缩略图预览、详情 / 下载 / 失败重试，图片按 SHA-256 去重存储

## 目录结构

```
WPF_Image/
├─ GptImagePlayground.slnx
├─ src/
│  ├─ GptImagePlayground.Core/        # 领域模型 + 服务（可独立测试）
│  │  ├─ Models/                      # ApiProfile / TaskRecord / AppSettings / 供应商定义 等
│  │  └─ Services/                    # SettingsService / TaskStore / ImageStore /
│  │                                  #   ImageApiService / ImageSizeHelper /
│  │                                  #   TransparentImageProcessor / BaseUrlHelper
│  └─ GptImagePlayground.App/         # WinForms + SunnyUI
│     ├─ Program.cs                   # 入口，主题初始化
│     ├─ UiStyle.cs                   # 统一配色与控件样式（各窗口复用）
│     ├─ MainForm.cs                  # 主工作台（顶部栏 / 输入区 / 画廊）
│     ├─ SettingsForm.cs              # API 配置管理 + 数据目录 + 习惯配置
│     ├─ ProfileEditForm.cs           # 单条 API 配置编辑弹窗
│     ├─ TaskDetailForm.cs            # 任务详情（大图 / 参数 / 下载 / 删除）
│     ├─ TaskCardControl.cs           # 画廊卡片控件
│     └─ AppServices.cs               # 服务容器（组合根）
```

## 构建与运行

```bash
cd WPF_Image
dotnet build GptImagePlayground.slnx
dotnet run --project src/GptImagePlayground.App
```

数据默认存于 `%LOCALAPPDATA%\GptImagePlayground\data`，可在 **设置 → 数据存储目录** 中
自选目录并一键迁移（图片与历史随目录迁移）。

### 打包为单文件可执行程序（免安装 .NET）

```bash
dotnet publish src/GptImagePlayground.App/GptImagePlayground.App.csproj -c Release \
  -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

产物为 `dist/GptImagePlayground.App.exe`，双击即可运行，目标机器无需安装 .NET 运行时。

## 使用步骤

1. 打开 **设置**，新建或编辑 API 配置：
   - 供应商：`openai` / `sb2api-async` / `fal` / `custom`
   - **接口地址**（如 `https://api.openai.com/v1`）、**API Key**、模型等
2. 在输入区填写提示词，选择尺寸 / 质量 / 张数 / 格式，可添加参考图
3. 点击 **生成**，任务进入画廊；双击卡片查看大图、参数与实际生效值、下载
4. 支持一键 **另存为** / **下载全部**，以及失败任务 **清理** / **重试** 回填参数

## 已实现功能

- 文本生图、参考图编辑、批量并发（n > 1）、尺寸预设、透明背景（API 原生 / 本地抠图）
- 多 API 配置管理、数据目录自选与迁移、任务历史、缩略图、详情 / 下载
- OpenAI Images API + Responses API + SSE 流式解析、自定义供应商（submit / poll）
