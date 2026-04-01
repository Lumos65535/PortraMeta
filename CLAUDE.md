# NfoForge

本地视频元数据管理工具，为 Infuse 用户生成 NFO 文件和竖屏海报。

## 技术栈

- 后端：.NET 9 ASP.NET Core Web API
- 前端：React + TypeScript + Material UI (MUI) + react-router-dom
- 数据库：SQLite（通过 EF Core）
- 部署：Docker + Docker Compose

## 项目结构

```
nfoforge/
├── backend/
│   ├── NfoForge.Api/          # Web API 入口、Controllers、Program.cs
│   ├── NfoForge.Core/         # 接口定义（ILibraryService、IVideoService、INfoParser、INfoService）、Models（Result<T>、PagedResult<T>）、DTOs
│   ├── NfoForge.Data/         # EF Core DbContext、Entities、Migrations、Service 实现、NfoParser、NfoService、FileSystemScanner
│   └── NfoForge.Tests/        # xUnit 单元测试（待实现）
├── frontend/
│   ├── src/
│   │   ├── api/               # axios 客户端（client.ts）、librariesApi、videosApi
│   │   ├── contexts/          # NotifyContext（全局 Snackbar 通知）
│   │   ├── i18n/              # react-i18next 配置（index.ts）、翻译文件（zh.json、en.json）
│   │   ├── pages/             # LibrariesPage、VideosPage、VideoDetailPage、SettingsPage
│   │   └── App.tsx            # BrowserRouter + Layout + Routes
│   └── package.json
├── docker-compose.yml
└── CLAUDE.md
```

## 常用命令

```bash
# 后端
cd backend
dotnet run --project NfoForge.Api          # 启动 API（端口 5000）
dotnet test                                # 运行测试
dotnet ef migrations add <Name> --project NfoForge.Data --startup-project NfoForge.Api
dotnet ef database update --project NfoForge.Data --startup-project NfoForge.Api

# 前端
cd frontend
npm run dev                                # 启动开发服务器（端口 3000）
npm run build                              # 生产构建

# Docker
docker compose up --build                  # 构建并启动
docker compose down                        # 停止
```

## 架构原则

### NFO 为真相来源
- NFO 文件是元数据的唯一持久化来源，Infuse 直接读取
- SQLite 只是索引缓存，用于快速查询和筛选
- 所有写操作必须同时写入 NFO 文件和 SQLite（通过 `INfoService.WriteAsync`）
- 扫描操作从 NFO 文件同步数据进 SQLite（通过 `INfoParser.ParseAsync`）

### 分层架构
- Controller 只做路由和参数验证，不含业务逻辑
- 业务逻辑全部在 Core 层接口 + Data 层 Service 实现中
- Data 层只负责数据库操作，不含业务规则
- 严格禁止跨层直接调用（Controller 不直接访问 DbContext）

### 代码规范
- 使用 async/await 贯穿所有 I/O 操作
- Service 通过接口注入（IVideoService、INfoService 等）
- 错误处理统一用 `Result<T>` 模式，不用异常控制业务流程
- API 返回统一格式：`{ data, error, success }`
- 全局异常处理中间件兜底（`UseExceptionHandler`），返回 500 统一格式
- 前端错误通过 `useNotify()` hook 统一展示 Snackbar
- axios 响应拦截器统一提取 `error` 字段并 reject

### 多语言规范（i18n）
- 前端使用 `react-i18next`，翻译文件位于 `frontend/src/i18n/zh.json`（中文）和 `en.json`（英文）
- **所有用户可见的文本必须通过 `t()` 读取，严禁在 TSX 中硬编码中文或英文字符串**
- 新增页面或组件时，先在两个翻译文件中同步添加对应 key，再在组件中使用 `useTranslation()` 调用
- 翻译 key 按页面/模块分组（`nav.*`、`common.*`、`videos.*`、`videoDetail.*`、`libraries.*`、`settings.*`）
- 动态内容（含变量的字符串）使用 i18next 插值语法：`t('key', { name: value })`，对应翻译文件中写 `{{name}}`
- 默认语言为中文，用户选择持久化到 `localStorage`（key: `nfoforge_lang`）

### 日志规范
- `ILogger<T>` 注入所有 Service
- 关键操作记录 Information 级别（创建库、删除库、扫描完成、NFO 写入）
- 警告记录 Warning 级别（资源未找到）
- 异常记录 Error 级别（含完整 Exception 对象）
- 查询记录 Debug 级别（避免日志污染）

## 数据模型（核心实体）

- `Library`：媒体库（扫描目录配置）
- `VideoFile`：视频文件（核心实体，关联所有元数据）
- `Studio`：厂牌（含 Logo 路径；扫描时按 Name 查找或创建）
- `Actor`：演员（含别名、头像等；扫描时按 Name 查找或创建）
- `VideoActor`：视频-演员多对多关联（含 Role、Order）
- `ExcludedFolder`：扫描排除目录（关联 Library，存储绝对路径）

## NFO 格式

Kodi Movie NFO 标准（Infuse 兼容），文件命名：`{videofile}.nfo`

```xml
<movie>
  <title>...</title>
  <originaltitle>...</originaltitle>
  <year>2023</year>
  <plot>...</plot>
  <studio>...</studio>
  <actor>
    <name>...</name>
    <role>...</role>
    <order>0</order>
  </actor>
</movie>
```

海报命名：`{videofile}-poster.jpg`（竖屏）
Fanart 命名：`{videofile}-fanart.jpg`（横屏，可选）

## API 规范

- 基础路径：`/api/`
- 分页参数：`?page=1&page_size=50`
- 筛选参数：`?has_nfo=false&has_poster=false&studio_id=1`
- 视频更新：`PUT /api/videos/{id}`，同时写 SQLite + NFO 文件
- 获取子目录：`GET /api/libraries/{id}/subdirectories`（用于排除目录 UI 浏览）
- 排除目录管理：`GET /api/libraries/{id}/excluded-folders`、`PUT /api/libraries/{id}/excluded-folders`

## 前端路由

- `/` → 重定向到 `/videos`
- `/videos` → 视频文件列表（可搜索，点击行跳转详情）
- `/videos/:id` → 视频详情 + 编辑（查看/编辑合一，编辑保存后写 NFO）
- `/libraries` → 媒体库管理（增删扫描）
- `/settings` → 设置（语言切换等）

## 后端配置说明

### CORS 配置

支持多种模式，通过 `appsettings.json` 或环境变量控制：

```json
"Cors": {
  "AllowedOrigins": "http://localhost:3000",  // 逗号分隔，支持多个 Origin
  "AllowAnyOrigin": false                      // true = 允许任意来源（本地/桌面模式）
}
```

| 场景 | 配置 |
|------|------|
| Docker 默认 Web | `AllowedOrigins=http://localhost:3000` |
| 多个前端域名 | `AllowedOrigins=http://a.com,https://b.com` |
| Tauri/Electron 桌面客户端 | `AllowAnyOrigin=true` |

> 旧配置键 `Cors:AllowedOrigin`（单数）向后兼容，仍可使用。

### API Key 认证（可选）

默认**禁用**。设置非空值后，所有 API 请求需携带 `X-Api-Key` 头：

```json
"Auth": {
  "ApiKey": ""   // 空字符串 = 禁用；填入密钥则启用
}
```

Docker 中通过环境变量启用：

```yaml
environment:
  - Auth__ApiKey=your-secret-key
```

前端对应构建参数（`VITE_API_KEY`），有值时 axios 自动附加请求头：

```bash
VITE_API_KEY=your-secret-key npm run build
```

> CORS OPTIONS 预检请求豁免 API Key 校验，不影响浏览器跨域协商。

## 多平台扩展方向

当前后端架构已为多平台接入做好准备，**后端代码无需改动**，通过配置适配不同场景：

```
              .NET REST API（不变）
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   浏览器          Docker Nginx    桌面壳（Tauri）
   （已完成）       （已完成）      macOS / Windows
```

### 桌面客户端（推荐方案：Tauri）

- Tauri 作为原生窗口壳，内嵌 WebView 加载现有 React 前端
- .NET 后端作为 Tauri sidecar 子进程在本地启动
- 后端配置：`Cors__AllowAnyOrigin=true`（桌面本地模式）
- 同一份 Tauri 代码支持 macOS 和 Windows 打包
- **无需修改任何前后端代码**

### 阶段路线图

| 阶段 | 状态 | 内容 |
|------|------|------|
| 0 | ✅ 完成 | Docker + Web 基础功能 |
| 1 | ✅ 完成 | CORS 多 Origin + 可选 API Key |
| 2 | 待实现 | Tauri 项目 + 嵌入后端子进程（macOS） |
| 3 | 待实现 | Windows 安装包打包（同一份 Tauri 代码） |

## 当前开发状态（2026-03-31）

已完成：
1. ✅ 项目脚手架（Docker + .NET + React 跑通）
2. ✅ Library 扫描服务（遍历目录，检测视频/NFO/海报文件）
3. ✅ VideoFile 列表 API + 前端文件列表组件（含搜索、分页）
4. ✅ NFO 读取解析（扫描时同步元数据、Studio、Actor 进 SQLite）
5. ✅ 元数据编辑 + NFO 生成（PUT /api/videos/{id} + VideoDetailPage）
6. ✅ 后端日志（ILogger）、全局异常处理中间件
7. ✅ 前端通知系统（useNotify/Snackbar）、loading 状态、搜索 debounce、react-router
8. ✅ 排除目录功能（ExcludedFolder 实体 + LibraryService + 前端 UI 弹窗选择）
9. ✅ 视频列表列配置持久化（列可见性、列宽自动保存至 localStorage）
10. ✅ 海报上传（POST /api/videos/{id}/poster）+ 预览（GET /api/videos/{id}/poster）
11. ✅ Fanart 上传（POST /api/videos/{id}/fanart）+ 预览（GET /api/videos/{id}/fanart）
12. ✅ 演员编辑 UI（VideoDetailPage 编辑模式下可增删改演员及角色）
13. ✅ 亮色/暗色/跟随系统主题切换（ThemeModeContext + 设置页）
14. ✅ CORS 多 Origin + 可选 API Key 认证（阶段一多平台铺垫）

待实现：
15. 刮削器接口预留 `/api/scrapers`（暂不实现）
16. Tauri 桌面客户端打包（macOS / Windows）

## 键盘操作规划（待实现）

计划在 VideoDetailPage 及全局添加键盘快捷键支持，提升浏览效率。

### 文件导航
| 快捷键 | 动作 |
|--------|------|
| `←` / `[` | 跳转到上一个文件 |
| `→` / `]` | 跳转到下一个文件 |
| `Backspace` / `Escape` | 返回视频列表 |

### 编辑操作
| 快捷键 | 动作 |
|--------|------|
| `E` | 进入编辑模式（非输入框聚焦时） |
| `Ctrl+S` | 保存当前编辑 |
| `Escape` | 取消编辑（编辑模式下） |

### 实现思路
- 使用 `useEffect` + `window.addEventListener('keydown', ...)` 注册全局快捷键
- 需判断当前焦点是否在输入框/文本区域内（`event.target` 为 `INPUT`、`TEXTAREA`、`SELECT` 或 `[contenteditable]`），若是则不触发快捷键
- 可封装为自定义 hook `useKeyboardNav`，接受 `prevId`、`nextId`、`editing` 状态作为参数
- 注意：快捷键注册应在 `editing` 状态变化时重新绑定（deps array 更新）

## 已知限制

- `Actor.AvatarPath`、`Actor.Aliases`、`Studio.LogoPath` 字段已定义在实体中，但尚未在 API 或前端使用

## 插件系统规划（待实现）

### 设计目标

保持核心项目轻量，将依赖重型系统组件的可选功能（如视频截图、刮削器）以**插件**形式独立提供，用户按需集成。

### 插件形式（方向待定，实现前需另行规划）

| 方向 | 说明 |
|------|------|
| 后端动态加载程序集 | 插件为 .NET DLL，后端运行时按需加载 |
| 独立 sidecar 进程 | 插件为独立进程，通过 HTTP API 与主后端通信 |
| 前端独立页面模块 | 插件为前端页面/组件，按需挂载到路由 |

### 已规划的插件

- **海报生成插件**（见下方章节）：官方第一个参考插件，演示视频截图 + 图像拼接能力

### 扩展方向

用户和第三方开发者可基于插件接口自行开发其他插件（如刮削器、字幕管理、封面裁剪等）。

---

## 后续扩展计划

### 海报生成插件规划

将以插件形式实现，不内置到核心项目。核心逻辑：后端提取视频帧 → 前端展示供用户选择 → 后端拼接生成海报。

#### 技术选型：后端 FFmpeg CLI（最轻量）

| 方案 | 说明 | 结论 |
|------|------|------|
| 前端 HTML5 `<video>` + Canvas | 需将完整视频传输到浏览器，5GB 文件代价不可接受 | ❌ |
| 前端 ffmpeg.wasm | ~50MB 初始下载 + 需修改 Nginx COOP/COEP 头 + 速度慢 | ❌ |
| **后端 FFmpeg CLI** | 视频本就在服务器，零传输，支持所有格式（MKV/MP4/AVI/TS 等） | ✅ |

#### 用户交互流程

1. 点击"从视频生成海报"按钮（仅在无海报时可用）
2. 后端调用 `ffmpeg` 在视频有效范围内均匀提取 16 帧缩略图
3. 前端弹窗以 4×4 网格展示 16 张缩略图
4. 用户手动点选 2 张（有顺序：第 1 张在上，第 2 张在下）
5. 后端重新提取对应 2 帧原始分辨率图像，用 `SixLabors.ImageSharp` 上下拼接
6. 裁剪/填充为标准 **3:4** 竖屏比例（如 900×1200），保存为 `{videofile}-poster.jpg`

#### API 草稿

```
POST /api/videos/{id}/poster/frames
→ 返回 16 张缩略图（base64 或临时 URL）

POST /api/videos/{id}/poster/generate
Body: { frameIndices: [3, 11] }
→ 拼接生成并保存海报，返回更新后的 VideoFile
```

#### 依赖增量

- Docker：`apt-get install -y ffmpeg`（约 70-90MB 镜像增量）
- NuGet：`SixLabors.ImageSharp`（纯托管库，约 5MB，无系统依赖）

### 批量文件编辑规划

**功能描述：** 在视频列表页多选文件，对选中文件批量赋值某些 NFO 字段（如统一设置厂牌、年份、标签等），并同步写入对应的 NFO 文件和 SQLite。

#### 现状分析

- 后端：无批量更新接口，当前只有 `PUT /api/videos/{id}`（单条）
- 前端：DataGrid 配置了 `disableRowSelectionOnClick`，无复选框多选机制
- 需从零构建，但现有 `UpdateVideoRequest` DTO 所有字段均可为 `null`（天然支持"部分字段更新"语义）

#### 实现路线

**后端**

1. 新增 `BatchUpdateVideoRequest` DTO，包含：
   - `ids: int[]`（目标视频 ID 列表）
   - 与 `UpdateVideoRequest` 相同的可选字段（`null` 表示"不修改该字段"）
2. `IVideoService` 新增 `BatchUpdateAsync(BatchUpdateVideoRequest request)` 方法
3. 新增端点 `PUT /api/videos/batch`，对每条记录调用 NFO 写入，事务包裹保证原子性
4. 返回 `{ updated: int, failed: int[] }`，前端据此刷新列表

**前端**

1. `VideosPage` DataGrid 去掉 `disableRowSelectionOnClick`，改为 `checkboxSelection`，同时保留行点击导航（通过 `onRowClick` 跳转）
2. 选中行数 > 0 时，工具栏出现"批量编辑"按钮
3. 弹出 `BatchEditDialog`：展示即将编辑的文件数量，提供各字段输入（留空 = 不修改），确认后调用批量 API
4. 操作完成后清空选中状态并刷新列表

#### API 草稿

```
PUT /api/videos/batch
Body: {
  ids: [1, 2, 3],
  studioName: "Acme",   // null = 不修改
  year: 2023,
  title: null,          // 不修改
  ...
}
Response: { data: { updated: 3, failed: [] }, success: true }
```

#### 注意事项

- `actors` 字段批量覆盖风险较高（会清空各文件原有演员），建议批量编辑暂不开放演员字段
- 批量操作不可撤销，Dialog 中应明确提示影响文件数量

---

### 文件名智能解析规划

**功能描述：** 按照用户定义的正则或分词规则，从视频文件名中自动提取片段，映射到元数据字段（标题、年份、厂牌、演员等），预填后供用户确认再写入 NFO。

#### 核心交互模式

1. **模板解析**（推荐，对非技术用户友好）：用户定义带占位符的模板字符串，如：
   ```
   {studio} {title} ({year})
   ```
   系统将模板转换为正则，从文件名中提取对应分组。

2. **自定义正则**（高级模式）：用户直接输入正则表达式，使用具名捕获组（`(?P<title>...)`）映射字段。

3. **分隔符分词**（最简模式）：按空格/下划线/连字符分词，用户手动将每个 token 拖拽到对应字段。

#### 实现路线

**后端**

1. 新增 `POST /api/videos/parse-filename` 端点（**纯计算，不写库**）：
   - 接收 `{ pattern: string, patternType: "template"|"regex"|"split", fileNames: string[] }`
   - 返回每个文件名的解析结果 `{ fileName, parsed: { title?, year?, studioName?, ... } }`
2. 模板转正则的转换逻辑放在后端（C# `Regex` 库），规避各浏览器正则差异

**前端**

1. 在视频列表页"批量编辑"工具栏中新增"从文件名填充"入口
2. 用户在 `FilenameParserDialog` 中：
   - 选择解析模式（模板 / 正则 / 分词）
   - 输入规则，实时预览当前页前 5 条文件名的解析结果（调用后端预览 API）
   - 确认后，将解析结果批量预填到各文件的草稿元数据中
3. 预览阶段不写入任何数据；用户点击"应用并保存"才调用批量更新 API

#### 模板占位符设计

| 占位符 | 映射字段 | 示例 |
|--------|----------|------|
| `{title}` | 标题 | `Inception` |
| `{originaltitle}` | 原标题 | `インセプション` |
| `{year}` | 年份（4位数字） | `2010` |
| `{studio}` | 厂牌 | `Warner` |
| `{actor}` | 演员（逗号分隔） | `DiCaprio` |
| `{ignore}` | 忽略该片段 | — |

#### API 草稿

```
POST /api/videos/parse-filename
Body: {
  pattern: "{studio} {title} ({year})",
  patternType: "template",
  fileNames: ["Warner Inception (2010).mkv", "..."]
}
Response: {
  data: [
    { fileName: "Warner Inception (2010).mkv",
      parsed: { studio: "Warner", title: "Inception", year: 2010 } },
    ...
  ]
}
```

#### 注意事项

- 解析结果可能有歧义（如文件名中有多个括号），预览界面需允许用户手动修正单条结果
- 正则模式应对特殊字符做安全检查，避免 ReDoS
- 文件名解析属于辅助工具，最终以用户确认后的批量保存为准，不自动写入

---

### VideoDetailPage — FileInfo Section 扩展
当前 FileInfo 仅显示路径、大小、扫描时间、NFO/Poster/Fanart 状态。
计划后续在此 section 中增加以下字段（需后端扫描时解析并存入 SQLite）：
- 分辨率（如 1920×1080）
- 视频编码（如 H.264、HEVC）
- 音频编码（如 AAC、DTS）
- 帧率、时长等技术参数
- 搜索区分大小写（SQLite `LIKE` 默认行为）
- 无法单独重新扫描某个视频文件（只能扫描整个媒体库）
