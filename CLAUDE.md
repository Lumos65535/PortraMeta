# NfoForge

本地视频元数据管理工具，为 Infuse 用户生成 NFO 文件和竖屏海报。

## 技术栈

- 后端：.NET 9 ASP.NET Core Web API
- 前端：React + Material UI (MUI)
- 数据库：SQLite（通过 EF Core）
- 部署：Docker + Docker Compose

## 项目结构

```
nfoforge/
├── backend/
│   ├── NfoForge.Api/          # Web API 入口、Controllers
│   ├── NfoForge.Core/         # 业务逻辑、Services、接口定义
│   ├── NfoForge.Data/         # EF Core DbContext、Entities、Migrations
│   └── NfoForge.Tests/        # xUnit 单元测试
├── frontend/
│   ├── src/
│   │   ├── components/        # 可复用组件
│   │   ├── pages/             # 页面组件
│   │   └── api/               # API 客户端（axios）
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
npm test                                   # 运行测试

# Docker
docker compose up --build                  # 构建并启动
docker compose down                        # 停止
```

## 架构原则

### NFO 为真相来源
- NFO 文件是元数据的唯一持久化来源，Infuse 直接读取
- SQLite 只是索引缓存，用于快速查询和筛选
- 所有写操作必须同时写入 NFO 文件和 SQLite
- 扫描操作从 NFO 文件同步数据进 SQLite

### 分层架构
- Controller 只做路由和参数验证，不含业务逻辑
- 业务逻辑全部在 Core 层的 Service 中
- Data 层只负责数据库操作，不含业务规则
- 严格禁止跨层直接调用（Controller 不直接访问 DbContext）

### 代码规范
- 使用 async/await 贯穿所有 I/O 操作
- Service 通过接口注入（IVideoService、INfoService 等）
- 错误处理统一用 Result<T> 模式，不用异常控制业务流程
- API 返回统一格式：`{ data, error, success }`

## 数据模型（核心实体）

- `Library`：媒体库（扫描目录配置）
- `VideoFile`：视频文件（核心实体，关联所有元数据）
- `Studio`：厂牌（含 Logo 路径）
- `Series`：系列/合集
- `Actor`：演员（含别名、头像等）
- `VideoActor`：视频-演员多对多关联

## NFO 格式

Kodi Movie NFO 标准（Infuse 兼容），文件命名：`{filename}.nfo`

海报命名：`{filename}.poster.jpg`（竖屏）
Fanart 命名：`{filename}.fanart.jpg`（横屏，可选）

## API 规范

- 基础路径：`/api/`
- 分页参数：`?page=1&page_size=50`
- 筛选参数：`?has_nfo=false&has_poster=false&studio_id=1`
- 异步任务：POST 返回 `job_id`，GET `/api/batch/status/{job_id}` 轮询进度

## 当前开发阶段：MVP

优先实现顺序：
1. 项目脚手架（Docker + .NET + React 跑通）
2. Library 扫描服务（遍历目录，检测视频/NFO/海报文件）
3. VideoFile 列表 API + 前端文件列表组件
4. NFO 读取解析（扫描时同步现有 NFO 进数据库）
5. 元数据编辑 + NFO 生成
6. 海报上传和重命名

刮削器功能暂不实现，只预留接口 `/api/scrapers`。
