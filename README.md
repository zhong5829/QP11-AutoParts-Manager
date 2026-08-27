# QP11 汽配管理系统

QP11 是一套面向**汽车配件门店/经销商的进销存一体化管理系统**，基于 **C# / .NET 8** 开发，采用 **WPF 桌面端 + ASP.NET Core WebApi 服务端**架构，直连 SQL Server 数据库，覆盖销售开单、采购进货、库存管理、应收账款、财务报表、VIN 查询、AI 智能助手等完整业务流程，并通过 Inno Setup 自包含发布，免预装运行时环境即可部署。

---

## 目录

- [核心功能](#核心功能)
- [技术栈](#技术栈)
- [解决方案结构](#解决方案结构)
- [目录结构](#目录结构)
- [环境要求](#环境要求)
- [构建与发布](#构建与发布)
- [数据库](#数据库)
- [测试](#测试)
- [文档](#文档)
- [版本历史](#版本历史)
- [许可](#许可)

---

## 核心功能

### 销售管理
- 销售开单：选客户 → 加商品 → 调价格 → 设折扣 → 收款结算（现金/微信/支付宝/刷卡/欠款）
- 销售单查询、单据作废、销售退货、销售换货、单据编辑
- 客户对账单（SellBalance）、客户应收/余额管理

### 采购与库存
- 采购进货（`bill_buy`）、进货退货、计划单 / 订单（JHDH）管理
- 库存查询、库存预警（`part_stock`）、库存盘点、配件台账与批次管理
- 多编码查询、配件图片预览、位置/库位管理

### 财务与报表
- 应收应付管理（应收/应付/往来账款）、收支录入（IncomeExpense）
- 账户管理、转账、会员卡与积分
- 销售排行榜、报表中心、单据打印（Web 打印 + 打印预览/设置）

### 智能化
- **VIN 查询**：支持多数据源聚合，解码车型并匹配配件
- **AI 智能助手（Agnes）**：对接 DeepSeek，支持配件价格/库存查询、工具调用
- **拼音助手**：拼音码生成与修复

### 系统
- 用户管理、角色权限、基础数据（客户/供应商/配件分类/地区/编码规则）、系统日志
- 数据库迁移、软件在线升级（Update）、桌面导航工作台（今日销售/今日销量排行）

### 跨平台移动端（规划中）
- 基于 .NET MAUI 的「销售开单 App」，复用现有实体的经验，目标平台 Android / iOS / Windows，详见 [sell-app PRD](.trae/documents/sell-app-prd.md)。

---

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 语言 / 框架 | C# / .NET 8 | 全部代码 |
| 桌面 UI | WPF (XAML) | 主客户端界面，MVVM 架构 |
| 服务端 | ASP.NET Core WebApi | 单据打印、HTTP 接口等 |
| ORM | Dapper | 轻量 SQL 映射、零配置 |
| 数据库 | Microsoft SQL Server | 核心库 `qipei`，经 ODBC DSN 连接 |
| 数据库驱动 | System.Data.Odbc / Microsoft.Data.SqlClient | 兼容 SQL Server 2000/2008+ |
| MVVM 工具 | CommunityToolkit.Mvvm | ObservableObject / RelayCommand |
| 依赖注入 / 配置 | Microsoft.Extensions.* | DI 容器、appsettings.json |
| 序列化 | System.Text.Json | 本地缓存 / 配置 |
| Excel | NPOI | 数据导入导出 |
| 日志 | Serilog | 文件日志 |
| 图像 | SixLabors.ImageSharp | 图片处理 |
| 加密 | BouncyCastle | 加解密 |
| 安装打包 | Inno Setup | 自包含发布、构建安装包 |
| AI | DeepSeek API | Agnes 智能助手 |

---

## 解决方案结构

解决方案位于 [`src/QP11.sln`](src/QP11.sln)，包含 6 个项目：

| 项目 | 类型 | 说明 |
|------|------|------|
| **QP11.Wpf** | WPF 桌面应用 | 主客户端：所有 View / ViewModel / Controls / Services |
| **QP11.Core** | 类库 | 实体（Entities）、接口（Interfaces）、常量，与业务无关的基础层 |
| **QP11.Data** | 类库 | Dapper 仓储层（Repositories）、工作单元（UnitOfWork） |
| **QP11.Services** | 类库 | 业务服务层：销售/采购/库存/VIN/AI/升级/迁移等 |
| **QP11.WebApi** | ASP.NET Core WebApi | 认证、配件、销售 Web 接口，Web 打印服务 |
| **QP11.Tests** | xUnit 测试 | 实体与服务层单元测试 |

另有独立工程（不在解决方案中）：
- `src/Models/QP11.Models.csproj`：独立数据模型工程
- `src/QueryA204/QueryA204.csproj`：A204 查询工具
- `Tools/`：迁移、拼音修复、Schema 查询等辅助脚本（Python / C# / PowerShell）

---

## 目录结构

```
f:\qp11
├── src/                        # 源码（解决方案入口 QP11.sln）
│   ├── QP11.Core/              # 实体、接口、常量
│   ├── QP11.Data/              # Dapper 仓储、UnitOfWork
│   ├── QP11.Services/          # 业务服务层
│   ├── QP11.Wpf/               # WPF 桌面主程序
│   ├── QP11.WebApi/            # ASP.NET Core WebApi
│   ├── QP11.Tests/             # 单元测试
│   ├── Models/                 # 独立数据模型
│   ├── QueryA204/              # A204 查询工具
│   └── QP11.sln                # 解决方案
├── docs/                       # 数据库字段说明等文档
├── installer/                  # Inno Setup 安装脚本
├── InstallPackage/             # 打包输出的安装包（二进制，gitignore 忽略）
├── Publish/                    # 发布输出（gitignore 忽略）
├── Tools/                      # 辅助迁移/修复脚本
├── .trae/documents/            # PRD / 技术文档
├── LICENSE.txt                 # 软件许可协议
├── QP11_Setup.iss              # 根安装打包脚本
└── 修改记录.md                 # 版本变更记录
```

---

## 环境要求

- **开发环境**：Visual Studio 2022（或 VS Code）、.NET 8 SDK
- **运行时**：Windows 10/11；安装包为自包含发布，免预装 .NET 运行时
- **数据库**：Microsoft SQL Server，业务库 `qipei`（ODBC DSN `qipei`）
- 国内网络环境建议配置 NuGet 镜像源加速还原

---

## 构建与发布

### 还原与编译

```powershell
dotnet restore src/QP11.Wpf/QP11.Wpf.csproj
dotnet build src/QP11.Wpf/QP11.Wpf.csproj -c Debug
```

### 自包含发布

```powershell
# 清理旧发布
Remove-Item -Recurse -Force f:\qp11\Publish\*.*

# 发布
dotnet publish f:\qp11\src\QP11.Wpf\QP11.Wpf.csproj -c Release -o f:\qp11\Publish
```

### 打包安装程序（Inno Setup）

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" f:\qp11\QP11_Setup.iss
```

打包输出位于 `InstallPackage\`，例如 `QP11Setup_v2.2.9.exe`。

> 注意：.NET 8 自包含发布默认会打包多语言资源（cs/de/es/fr/.../zh-Hans 等）。不要手动删除 `Publish` 下的语言文件夹，否则 Inno Setup（ISCC）会报文件缺失。

### 运行单元测试

```powershell
dotnet test src/QP11.Tests/QP11.Tests.csproj
```

---

## 数据库

系统直连 SQL Server 业务库 `qipei`，核心数据表如下：

| 表名 | 用途 | 主要操作 |
|------|------|---------|
| `bill_sell` | 销售单头 | SELECT / INSERT / UPDATE（作废 `flag=-1`）|
| `detail_sell` | 销售明细 | SELECT / INSERT |
| `bill_buy` | 采购进货单 | SELECT / INSERT / UPDATE |
| `part_data` | 配件主数据 | SELECT（只读）/ 维护 |
| `part_stock` | 配件库存 | SELECT / UPDATE（出库冲减需校验防止负数）|
| `client_infor` | 客户信息 | SELECT / INSERT / UPDATE |
| `supplier_infor` | 供应商信息 | SELECT / INSERT / UPDATE |
| `work_infor` | 用户/员工 | SELECT（登录验证）|
| `account` | 账户 | 应收应付往来 |

- 连接方式支持 **ODBC DSN**（`qipei`）与 `Microsoft.Data.SqlClient` 直连。
- 关键约束：库存冲减必须校验返回值，防止库存为负；操作远程库时优先使用异步连接避免阻塞 UI。
- 详细字段说明见 [`docs/数据库字段说明.md`](docs/数据库字段说明.md)。

---

## 测试

`src/QP11.Tests` 当前覆盖：

- 实体测试：`BillSellTests`、`ClientInforTests`、`DetailSellTests`
- 服务测试：`CalcServiceTests`（金额/折扣计算）

---

## 文档

| 文档 | 路径 |
|------|------|
| 销售开单 App 产品需求（PRD） | [.trae/documents/sell-app-prd.md](.trae/documents/sell-app-prd.md) |
| 销售开单 App 技术架构 | [.trae/documents/sell-app-tech.md](.trae/documents/sell-app-tech.md) |
| 数据库字段说明 | [docs/数据库字段说明.md](docs/数据库字段说明.md) |
| 变更记录 | [修改记录.md](修改记录.md) |

---

## 版本历史

| 版本 | 说明 |
|------|------|
| v2.2.9 | 桌面导航工作台改造：今日销售单据、今日配件销售排行、UI 优化 |
| … | 历史迭代详见 [修改记录.md](修改记录.md) |

---

## 许可

本项目依据 [`LICENSE.txt`](LICENSE.txt) 许可协议提供，版权所有 © 2026 QP11。软件"按原样"提供，使用前请仔细阅读许可条款。

- **不可商用**：本软件仅供个人学习/研究或单位内部非营利使用，禁止商业用途、转售、托管服务（SaaS）或任何收费服务
- 不得对本软件进行反向工程、反编译或反汇编
- 不得出租、出借或以其他方式分发本软件
- 商业使用授权请与 QP11 联系
- 技术支持：support@qp11.com