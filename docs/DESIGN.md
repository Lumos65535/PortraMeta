# NfoForge — 设计文档 v1.0

> 一个面向 Infuse 用户的本地视频元数据管理工具，生成标准 NFO 文件和竖屏海报。
## 背景文档
详细的产品设计和业务场景见 `docs/DESIGN.md`，
遇到业务逻辑决策时请先阅读。
---

## 1. 项目定位

### 解决的问题
现有工具（如 TinyMediaManager、StashApp）在以下场景表现不足：
- 不支持竖屏海报（Infuse 海报墙所需格式）
- 对非主流视频内容（如 OnlyFans 等平台资源）缺乏针对性支持
- 界面陈旧，缺少现代 Web UI

### 目标用户
个人媒体收藏者，使用 Infuse 作为播放器，视频存放于 NAS（群晖等）。

### 开源定位
GitHub 开源项目，README 以 SFW 语言描述为"本地媒体元数据管理工具，兼容 Infuse/Kodi/Jellyfin"。

---

## 2. 技术栈

| 层级   | 技术                            |
| ---- | ----------------------------- |
| 后端   | .NET 9 (ASP.NET Core Web API) |
| 前端   | React + Material UI           |
| 数据库  | SQLite（轻量索引，NFO 为真相来源）        |
| 部署   | Docker + Docker Compose       |
| 运行环境 | NAS 或本地 Mac，浏览器访问             |

---

## 3. 核心设计原则

### NFO 为真相来源
- SQLite 只作为索引缓存，用于快速查询和筛选
- 所有元数据写操作同时写入 NFO 文件和 SQLite
- NFO 被外部修改时，重新扫描可同步进 SQLite
- 即使删除数据库，元数据仍完整保留在文件系统

### 轻量化
- 单用户设计，无需认证系统
- SQLite 零配置，Docker 一个容器即可运行
- 不做视频播放、文件重命名、字幕管理

---

## 4. 文件规范

### NFO 格式
Kodi Movie NFO（Infuse 原生兼容）：

```xml
<movie>
  <title>Video Title</title>
  <year>2024</year>
  <plot>Description here.</plot>
  <studio>OnlyFans</studio>
  <director>Director Name</director>
  <rating>8.5</rating>
  <actor>
    <name>Actor Name</name>
  </actor>
  <tag>Tag1</tag>
  <tag>Tag2</tag>
  <set>
    <name>Series Name</name>
    <order>1</order>
  </set>
</movie>
```

### 文件命名规范
所有相关文件与视频同目录，同名不同后缀：

```
/videos/
  SomeVideo.mp4
  SomeVideo.nfo              ← 元数据
  SomeVideo.poster.jpg       ← 竖屏海报（Infuse 主封面）
  SomeVideo.fanart.jpg       ← 横屏缩略图（可选）
```

---

## 5. 数据模型

### Library（媒体库）
用户配置的扫描目录。

| 字段                | 类型       | 说明              |
| ----------------- | -------- | --------------- |
| id                | int      | 主键              |
| name              | string   | 库名称，如"OnlyFans" |
| path              | string   | NAS 目录路径        |
| last\_scanned\_at | datetime | 最后扫描时间          |
| created\_at       | datetime | -               |

### Studio（厂牌）
独立管理厂牌信息，供 NFO 引用，也用于海报生成器叠加 Logo。

| 字段          | 类型       | 说明           |
| ----------- | -------- | ------------ |
| id          | int      | 主键           |
| name        | string   | 厂牌名称         |
| logo\_path  | string?  | 本地 Logo 文件路径 |
| website     | string?  | 网站地址         |
| created\_at | datetime | -            |

### Series（系列）
视频系列/合集概念。

| 字段          | 类型       | 说明        |
| ----------- | -------- | --------- |
| id          | int      | 主键        |
| name        | string   | 系列名称      |
| description | string?  | 简介        |
| studio\_id  | int?     | 关联厂牌（可为空） |
| created\_at | datetime | -         |

### Actor（演员）

| 字段           | 类型       | 说明           |
| ------------ | -------- | ------------ |
| id           | int      | 主键           |
| name         | string   | 姓名           |
| aliases      | string   | JSON 数组，别名列表 |
| bio          | string?  | 个人简介         |
| avatar\_path | string?  | 本地头像路径       |
| birthdate    | date?    | 生日           |
| nationality  | string?  | 国籍           |
| website      | string?  | 个人网站         |
| created\_at  | datetime | -            |

### VideoFile（视频文件，核心实体）

| 字段                 | 类型        | 说明             |
| ------------------ | --------- | -------------- |
| id                 | int       | 主键             |
| library\_id        | int       | 关联 Library     |
| file\_path         | string    | 完整路径（唯一键）      |
| file\_name         | string    | 文件名（不含扩展名）     |
| file\_size         | long?     | 文件大小（bytes）    |
| duration           | int?      | 时长（秒）          |
| resolution         | string?   | 如 "1920x1080"  |
| title              | string?   | 标题             |
| year               | int?      | 年份             |
| plot               | string?   | 简介             |
| studio\_id         | int?      | 关联 Studio 表    |
| studio\_name       | string?   | 冗余字符串，直接写入 NFO |
| series\_id         | int?      | 关联 Series      |
| series\_order      | int?      | 系列中的顺序         |
| director           | string?   | 导演             |
| rating             | float?    | 评分             |
| has\_nfo           | bool      | 是否存在 NFO 文件    |
| has\_poster        | bool      | 是否存在海报文件       |
| has\_fanart        | bool      | 是否存在 fanart 文件 |
| nfo\_last\_written | datetime? | NFO 最后写入时间     |
| created\_at        | datetime  | -              |
| updated\_at        | datetime  | -              |

### 关联表
- `VideoActor`：video\_id + actor\_id（多对多）
- `VideoTag`：video\_id + tag\_name（MVP 阶段 tag 不单独建表）

---

## 6. API 设计

### Library
```
GET    /api/libraries                    # 获取所有媒体库
POST   /api/libraries                    # 新建媒体库
PUT    /api/libraries/{id}               # 修改媒体库
DELETE /api/libraries/{id}               # 删除媒体库
POST   /api/libraries/{id}/scan          # 触发扫描（异步）
GET    /api/libraries/{id}/scan/status   # 获取扫描进度
```

### VideoFile
```
GET    /api/videos                       # 列表（分页 + 筛选）
GET    /api/videos/{id}                  # 单个视频详情
PUT    /api/videos/{id}                  # 更新元数据
DELETE /api/videos/{id}                  # 从数据库移除（不删文件）

POST   /api/videos/{id}/nfo              # 生成/覆盖 NFO 文件
GET    /api/videos/{id}/nfo/preview      # 预览 NFO 内容（XML 字符串）

POST   /api/videos/{id}/poster           # 上传本地图片作为海报
DELETE /api/videos/{id}/poster           # 删除海报文件
```

`GET /api/videos` 查询参数：
```
?library_id=1
?studio_id=2
?series_id=3
?actor_id=4
?has_nfo=false
?has_poster=false
?q=关键词
?page=1&page_size=50
?sort=title|year|created_at
?order=asc|desc
```

### Studio
```
GET    /api/studios                      # 所有厂牌
POST   /api/studios                      # 新建厂牌
GET    /api/studios/{id}                 # 厂牌详情
PUT    /api/studios/{id}                 # 修改厂牌
DELETE /api/studios/{id}                 # 删除厂牌
POST   /api/studios/{id}/logo            # 上传 Logo
```

### Actor
```
GET    /api/actors                       # 所有演员（支持搜索）
POST   /api/actors                       # 新建演员
GET    /api/actors/{id}                  # 演员详情
PUT    /api/actors/{id}                  # 修改演员信息
DELETE /api/actors/{id}                  # 删除演员
POST   /api/actors/{id}/avatar           # 上传头像
GET    /api/actors/{id}/videos           # 该演员的所有视频
```

### Series
```
GET    /api/series                       # 所有系列
POST   /api/series                       # 新建系列
GET    /api/series/{id}                  # 系列详情
PUT    /api/series/{id}                  # 修改系列
DELETE /api/series/{id}                  # 删除系列
GET    /api/series/{id}/videos           # 系列下所有视频（按 order 排序）
```

### 批量操作
```
POST   /api/batch/nfo                    # 批量生成 NFO
GET    /api/batch/status/{job_id}        # 批量任务进度
```

### 刮削器（接口预留，MVP 不实现）
```
GET    /api/scrapers                     # 获取可用刮削器列表（插件式）
POST   /api/scrapers/search              # 搜索元数据
POST   /api/scrapers/apply               # 应用结果到视频
```

### 系统
```
GET    /api/system/config                # 获取系统配置
PUT    /api/system/config                # 更新系统配置
GET    /api/health                       # 健康检查
```

---

## 7. 核心界面

### 主界面布局
```
┌─────────────────────┬────────────────────────────────────┐
│  文件列表            │  详情面板                           │
│                     │                                    │
│  ✅ video1.mp4      │  [poster]   标题: __________       │
│  ✅ video2.mp4      │             年份: __________       │
│  ○  video3.mp4      │             厂牌: __________       │
│  ○  video4.mp4      │             演员: __________       │
│                     │             简介: __________       │
│  筛选/搜索栏         │             系列: __________       │
│  ✅ = 已有 NFO       │                                    │
│  ○  = 未处理         │  [选择海报]  [生成 NFO]  [保存]    │
└─────────────────────┴────────────────────────────────────┘
```

后续可扩展海报墙视图（网格模式切换）。

---

## 8. 开发阶段规划

### MVP（第一阶段）
- [ ]() 项目脚手架：.NET 9 + React + SQLite + Docker
- [ ]() Library 扫描：遍历目录，识别视频文件，检测 NFO/海报存在
- [ ]() VideoFile CRUD：列表、详情、元数据编辑
- [ ]() NFO 读取：启动/扫描时解析现有 NFO 同步进数据库
- [ ]() NFO 生成：手动触发，输出 Kodi 标准格式
- [ ]() 海报管理：上传图片，重命名为 `.poster.jpg`
- [ ]() 基础筛选：按有无 NFO、有无海报过滤

### 第二阶段
- [ ]() Studio / Actor / Series 完整 CRUD UI
- [ ]() 批量 NFO 生成
- [ ]() 刮削器插件接口

### 第三阶段
- [ ]() 海报生成器：视频截图 → 竖屏海报
- [ ]() 截图拼接（横屏→竖屏）
- [ ]() Logo 叠加（厂牌 Logo）
- [ ]() 海报墙视图（网格模式）

---

## 9. 项目结构（规划）

```
nfoforge/
├── backend/
│   ├── NfoForge.Api/          # Web API 入口
│   ├── NfoForge.Core/         # 业务逻辑、服务
│   ├── NfoForge.Data/         # EF Core + SQLite + 数据模型
│   └── NfoForge.Tests/        # 单元测试
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   └── api/               # API 客户端
│   └── package.json
├── docker-compose.yml
├── Dockerfile
├── CLAUDE.md
└── README.md
```

