# GPT Image Playground（WinForms 桌面版）

将开源的前端项目 [CookSleep/gpt_image_playground](https://github.com/CookSleep/gpt_image_playground)
（基于 OpenAI gpt-image-2.5 API 的图片生成/编辑工作台）复刻为 **.NET 8 WinForms 桌面应用**。

- **UI 库**：SunnyUI（免费、开源、国内使用广泛）
- **目标框架**：.NET 8（net8.0-windows）
- **存储**：SQLite（任务历史）+ 本地图片文件（SHA-256 去重）+ settings.json
- **图片处理**：System.Drawing（缩略图、透明背景本地抠图）

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

## 数据模型与 API

数据模型移植自原项目 `src/types.ts`，请求/响应结构移植自
`src/lib/openaiCompatibleImageApi.ts` 与 `devProxy.ts`。已实现：

- OpenAI 兼容 **Images API**：`images/generations`（JSON）、`images/edits`（multipart）
- OpenAI **Responses API**：`/responses` + `image_generation` 工具
- 多配置管理与快速切换（OpenAI 兼容、sub2api 异步、fal.ai、自定义供应商）
- 尺寸 1K/2K/4K 预设 + 安全规整（`ImageSizeHelper`）
- 透明背景本地抠图（键色检测 + 连通域抠图，移植 `transparentImage.ts`）
- 任务历史持久化（SQLite）、图片文件去重存储、缩略图
- 流式传输（SSE 解析，接收中间步骤图）

## 构建与运行

```bash
cd WPF_Image
dotnet build GptImagePlayground.slnx
dotnet run --project src/GptImagePlayground.App
```

数据默认存于 `%LOCALAPPDATA%\GptImagePlayground\data`，可在 **设置 → 数据存储目录** 中
自选目录并一键迁移（图片与历史随目录迁移）。

## 使用步骤

1. 打开 **设置**，新建或编辑 API 配置：
   - 供应商：`openai` / `sb2api-async` / `fal` / `custom`
   - **接口地址**（如 `https://api.openai.com/v1`）、**API Key**、模型等
2. 在输入区填写提示词，选择尺寸/质量/张数/格式，可添加参考图
3. 点击 **生成**，任务进入画廊；双击卡片查看大图、参数与实际生效值、下载
4. 支持一键 **另存为** / **下载全部**，以及失败任务 **清理** / **重试** 回填参数

## 已实现 / 待实现

**已实现**
- 文本生图、参考图编辑、批量并发（n>1）、尺寸预设、透明背景（API 原生/本地抠图）
- 多 API 配置管理、数据目录自选与迁移、任务历史、缩略图、详情/下载
- OpenAI Images API + Responses API + SSE 流式解析、自定义供应商（submit/poll）

**待实现（后续阶段）**
- 遮罩可视化编辑器、收藏夹/收藏夹视图、ZIP 备份导出/导入
- Agent 多轮对话模式（分支、@引用、上下文记忆、web search）
- fal.ai 队列接口、sub2api 异步任务的 UI 化配置、Codex CLI 兼容模式精细设置
- 自定义供应商的可视化创建（JSON 模板编辑）
