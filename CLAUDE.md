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

海报命名：`{videofile}.poster.jpg`（竖屏）
Fanart 命名：`{videofile}.fanart.jpg`（横屏，可选）

## API 规范

- 基础路径：`/api/`
- 分页参数：`?page=1&page_size=50`
- 筛选参数：`?has_nfo=false&has_poster=false&studio_id=1`
- 视频更新：`PUT /api/videos/{id}`，同时写 SQLite + NFO 文件

## 前端路由

- `/` → 重定向到 `/videos`
- `/videos` → 视频文件列表（可搜索，点击行跳转详情）
- `/videos/:id` → 视频详情 + 编辑（查看/编辑合一，编辑保存后写 NFO）
- `/libraries` → 媒体库管理（增删扫描）
- `/settings` → 设置（语言切换等）

## 当前开发状态（2026-03-31）

已完成：
1. ✅ 项目脚手架（Docker + .NET + React 跑通）
2. ✅ Library 扫描服务（遍历目录，检测视频/NFO/海报文件）
3. ✅ VideoFile 列表 API + 前端文件列表组件（含搜索、分页）
4. ✅ NFO 读取解析（扫描时同步元数据、Studio、Actor 进 SQLite）
5. ✅ 元数据编辑 + NFO 生成（PUT /api/videos/{id} + VideoDetailPage）
6. ✅ 后端日志（ILogger）、全局异常处理中间件
7. ✅ 前端通知系统（useNotify/Snackbar）、loading 状态、搜索 debounce、react-router

待实现：
6. 海报上传和重命名（`POST /api/videos/{id}/poster`）
7. 刮削器接口预留 `/api/scrapers`（暂不实现）
