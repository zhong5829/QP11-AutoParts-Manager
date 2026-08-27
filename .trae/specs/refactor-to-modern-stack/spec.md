# QP11汽配管理系统现代化重构方案 Spec

## Why

当前QP11系统基于PowerBuilder 11（2007年技术），面临技术栈老化、维护困难、人才稀缺等严峻挑战。需要制定一套完整的现代化重构方案，将系统迁移到 **WPF/WinUI 3 (.NET 8) 桌面端平台**，同时**100%保留原有功能、UI交互、业务逻辑和使用习惯**，并**完全兼容现有SQL Server数据库（qipei）**，确保数据零丢失、业务无缝衔接。

**关键约束：原项目数据量巨大（配件表可能10万+行，销售明细百万级行），必须保证大数据量场景下界面响应达到秒级（<1s），杜绝卡顿。WPF原生虚拟滚动+ODBC/ADO.NET直连数据库可完美解决此问题（原PB项目使用ODBC连接SQL Server）。**

## What Changes

* 制定从PB11到 **WPF/WinUI 3 (.NET 8)** 的完整重构方案（桌面端高性能架构）

* **必须包含原项目所有200+功能点**（进销存/财务/会员/报表等）【已移除维修/在线支付/软件授权模块】

* **UI界面1:1还原**（原285个PB窗口→约250个WPF窗口/UserControl，已移除维修模块50+窗口）

* **业务逻辑完整迁移**（所有计算公式、校验规则、工作流）

* **使用逻辑保持一致**（操作流程、快捷键、交互模式、焦点管理）

* **保留原数据库qipei**（95张表结构不变，ODBC/ADO.NET直连，兼容原PB的ODBC连接方式）

* **⚡ 性能保障：大数据量（万行+表格）加载<500ms，操作响应<100ms**

## Impact

* 重构范围：整个QP11系统（15个PBL库、550+源文件、95张数据表）

* 目标平台：**WPF / WinUI 3 (.NET 8) 桌面应用**

* 性能目标：配件列表10万行加载<500ms，销售明细千行级操作无卡顿

* 受影响方：全体业务用户、IT运维团队、管理层

* 数据保障：零数据迁移，ADO.NET直读写原SQL Server数据库

* 部署方式：MSIX安装包 或 ClickOnce 部署

## ADDED Requirements

### Requirement: 技术栈选型与架构设计（WPF/.NET 8 高性能方案）

系统 SHALL 采用 **WPF (Windows Presentation Foundation) 或 WinUI 3 桌面端框架**，专门针对大数据量场景和PB架构高度兼容性设计：

#### 核心技术栈（必选）

##### 1. 运行时与框架

* **.NET Runtime**: **.NET 8.0 LTS** (长期支持版本)

  * ✅ 微软官方支持至2026年11月

  * ✅ 性能比.NET Framework 4.x提升50%+

  * ✅ 原生支持AOT编译（启动速度极快）

  * ✅ 跨平台能力（如未来需迁移到Mac/Linux）

##### 2. UI框架（二选一）

* **方案A（推荐）: WPF (.NET 8)**

  * ✅ 成熟稳定（2006年发布，18年打磨）

  * ✅ 生态最丰富（DevExpress/Telerik/RadControls等商业组件库）

  * ✅ MDI多文档框架天然支持

  * ✅ DataGrid虚拟滚动成熟（UIVirtualization）

  * ✅ XAML可视化设计器（类似PB的画布式开发）

  * ✅ 学习资源丰富，招聘容易

  * ⚠️ 视觉风格偏传统（可通过MaterialDesignInXAML美化）

* **方案B: WinUI 3 (Windows App SDK)**

  * ✅ 微软最新UI框架（Windows 11原生风格）

  * ✅ Fluent Design现代视觉（圆角、阴影、动画）

  * ✅ 性能更优（Composition API硬件加速）

  * ⚠️ 生态较新（第三方组件库较少）

  * ⚠️ 不支持MDI（需用Tab布局替代）

  * ⚠️ 仅限Windows 10 1809+

> **🎯 推荐选择方案A（WPF + DevExpress）**，原因：
>
> 1. 与PB的MDI架构最接近（约250个窗口可直接映射，已移除维修模块）
> 2. DevExpress DataGrid完美替代DataWindow（功能更强）
> 3. 企业级稳定性（银行/证券/制造业广泛使用）
> 4. 团队学习成本低（C# + XAML资料海量）

##### 3. 架构模式

* **MVVM (Model-View-ViewModel)**

  * View: XAML窗口/用户控件（对应PB的.srw窗口）

  * ViewModel: C#类（对应PB的实例变量+事件处理代码）

  * Model: 实体类（对应PB的结构体st\_\*和数据表）

  * ✅ 与PB的事件驱动模型高度契合

  * ✅ 支持双向数据绑定（类似PB的DataWindow联动）

  * ✅ 便于单元测试（ViewModel可脱离UI测试）

##### 4. 数据访问层

* **ORM选择**: **Dapper** (推荐) 或 **Entity Framework Core**

  * **Dapper优势**:

    * ✅ 轻量级微ORM（性能接近手写ADO.NET）

    * ✅ 查询速度比EF Core快2-5倍

    * ✅ 大数据量查询无内存开销（不跟踪实体状态）

    * ✅ 灵活性高（可写任意SQL，包括存储过程调用）

    * ✅ 与95张表的简单CRUD完美匹配

  * **EF Core优势**:

    * ✅ LINQ查询语法（类型安全）

    * ✅ ChangeTracking自动变更追踪

    * ✅ Migration数据库迁移工具

    * ⚠️ 大数据量时有性能损耗

* **数据库连接**: **System.Data.Odbc**（首选，兼容原PB的ODBC）或 **Microsoft.Data.SqlClient**（备选，纯ADO.NET方案）

  * ✅ **ODBC方式**（推荐）：与原PB项目一致，使用原ODBC数据源配置（DSN），无需修改连接字符串格式

  * ✅ **SqlClient方式**（备选）：原生ADO.NET连接SQL Server，性能略优

  * ✅ 连接池管理（ConnectionStringPooling=true）

  * ✅ 异步操作支持（async/await）

  * ✅ 直连原qipei数据库，零中间层

  * 📝 **为何选择ODBC兼容**：原PB11项目使用ODBC连接SQL Server（通过ODBC数据源名DSN），WPF项目可通过`System.Data.Odbc`保持一致的连接方式，便于运维和迁移

##### 5. 第三方组件库（关键！）

| 组件类型               | 推荐库                                           | 用途                        | 对应PB对象                     |
| ------------------ | --------------------------------------------- | ------------------------- | -------------------------- |
| **DataGrid**       | **DevExpress DXDataGrid for WPF**             | 万行虚拟滚动、分组汇总、Master-Detail | DataWindow Grid            |
| **DataForm**       | DevExpress DXDataForm 或自定义                    | 动态表单生成、字段绑定               | DataWindow Freeform        |
| **Docking**        | DevExpress DXDocking                          | MDI多文档窗口管理                | w\_main MDI框架              |
| **Ribbon/Toolbar** | DevExpress DXRibbon                           | 工具栏、菜单栏                   | toolbar目录                  |
| **TreeView**       | WPF原生TreeView或DevExpress                      | 分类树形导航                    | u\_treeview                |
| **ComboBox**       | WPF原生ComboBox + AutoCompleteBox               | 下拉搜索、拼音检索                 | DropDownListBox/DropDownDW |
| **DatePicker**     | WPF原生DatePicker                               | 日期时间选择                    | EditMask                   |
| **PrintPreview**   | DevExpress DXPrintingSystem                   | 打印预览、报表输出                 | w\_print\_preview\*        |
| **Chart**          | DevExpress DXCharts 或 LiveCharts              | 图表展示（柱状/折线/饼图）            | DataWindow Graph           |
| **Excel导出**        | **NPOI** 或 **EPPlus** 或 DevExpressSpreadsheet | Excel导出（替代dw2xls）         | dw2xls模块                   |
| **SplashScreen**   | DevExpress DXSplashScreen                     | 启动闪屏                      | w\_check登录前                |
| **MessageBox**     | DevExpress DXMessageBox 或 WPF原生               | 消息提示框                     | PB MessageBox()            |
| **WaitIndicator**  | DevExpress DXWaitIndicator                    | 加载等待动画                    | 无（PB同步阻塞）                  |

##### 6. 辅助工具库

* **依赖注入**: Microsoft.Extensions.DependencyInjection (官方DI)

* **日志记录**: Serilog (结构化日志，替代f\_syslog)

* **配置管理**: Microsoft.Extensions.Configuration (appsettings.json)

* **JSON序列化**: System.Text.Json (内置)

* **加密**: System.Security.Cryptography (MD5/AES，替代des64.dll)

* **中文处理**: 自研（拼音转换，替代ShuChinese.dll/getBiHua.dll）

* **FTP客户端**: FluentFTP (替代nvo\_ftp.sru)

* **HTTP客户端**: HttpClient (替代nvo\_internet\_main.sru)

#### 🏗️ WPF架构设计（MVVM + 分层架构）

```
┌─────────────────────────────────────────────────────────┐
│                  QP11.Wpf.Desktop (WPF客户端)             │
│                                                           │
│  ┌──────────────┐ ┌────────────┐ ┌────────────────────┐ │
│  │  Views 层     │ │ ViewModels │ │   Converters       │ │
│  │  (XAML窗口)   │ │  (C#逻辑)  │ │   (数据转换器)      │ │
│  │              │ │            │ │                    │ │
│  │ ·MainWindow  │ │ ·MainVM    │ │ ·BoolToVisibility │ │
│  │ ·SellOrderV  │ │ ·SellOrderVM│ │ ·DateTimeFormat   │ │
│  │  ·PartSelectV │ │ ·PartSelectVM│ │ ·MoneyConverter   │ │
│  │  ... (250个)  │ │ ... (250个) │ │ ...                │ │
│  └──────┬───────┘ └─────┬──────┘ └────────────────────┘ │
│         │               │                                │
│  ┌──────▼───────────────▼────────────────────────────┐  │
│  │                 Services 层 (业务服务)               │  │
│  │                                                   │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────────────┐  │  │
│  │  │SellService│ │BuyService│ │FinanceService    │  │  │
│  │  │(销售业务) │ │(采购业务) │ │(财务业务)        │  │  │
│  │  ├──────────┤ ├──────────┤ ├──────────────────┤  │  │
│  │  │PartService│ │ClientSvc │ │ReportService     │  │  │
│  │  │(配件业务) │ │(客户业务) │ │(报表服务)        │  │  │
│  │  ├──────────┤ ├──────────┤ ├──────────────────┤  │  │
│  │  │CalcService│ │ValidSvc  │ │MemberService     │  │  │
│  │  │(计算引擎) │ │(校验引擎) │ │(会员服务)        │  │  │
│  │  └──────────┘ └──────────┘ └──────────────────┘  │  │
│  └──────────────────────┬───────────────────────────┘  │
│                         │                               │
│  ┌──────────────────────▼───────────────────────────┐  │
│  │              Repository 层 (数据仓库)              │  │
│  │                                                   │  │
│  │  ┌──────────────────────────────────────────┐    │  │
│  │  │ BaseRepository<T> (泛型基类)              │    │  │
│  │  │  · GetById / GetAll / Find / PageQuery   │    │  │
│  │  │  · Insert / Update / Delete / BatchInsert│    │  │
│  │  │  · ExecuteSql / ExecuteScalar           │    │  │
│  │  └──────────────────────────────────────────┘    │  │
│  │                                                   │  │
│  │  · PartRepository (part_data/part_stock表)       │  │
│  │  · SellRepository (bill_sell/detail_sell表)      │  │
│  │  · ClientRepository (client_infor表)             │  │
│  │  · AccountRepository (account/pays表)            │  │
│  │  · MemberRepository (member_card/borrow表)        │  │
│  │  ... (共95张表 → ~35个Repository)                │  │
│  └──────────────────────┬───────────────────────────┘  │
│                         │                               │
│  ┌──────────────────────▼───────────────────────────┐  │
│  │              Infrastructure 层 (基础设施)          │  │
│  │                                                   │  │
│  │  ┌────────────┐ ┌────────────┐ ┌──────────────┐  │  │
│  │  │DbContext   │ │ DapperORM  │ │ SqlHelper    │  │  │
│  │  │(EF Core)   │ │ (轻量ORM)  │ │ (原始SQL)    │  │  │
│  │  └─────┬──────┘ └─────┬──────┘ └──────┬───────┘  │  │
│  │        │              │              │           │  │
│  │  ┌─────▼──────────────▼──────────────▼────────┐  │  │
│  │  │         DatabaseFactory (工厂模式)          │  │  │
│  │  │  · 创建IDbConnection (连接池管理)            │  │  │
│  │  │  · 事务管理 (TransactionScope)              │  │  │
│  │  │  · 连接字符串配置 (appsettings.json)         │  │  │
│  │  └────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
│                         │                               │
│  ┌──────────────────────▼───────────────────────────┐  │
│  │              SQL Server (原 qipei 数据库)          │  │
│  │              95张表 · 零改动 · ODBC/ADO.NET直连（兼容原PB的ODBC）         │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

#### ⚡ 大数据量性能保障方案（WPF核心优势）

##### 1. DataGrid虚拟滚动（万行流畅的关键）

```xml
<!-- DevExpress DataGrid 配置示例 -->
<dxg:GridControl x:Name="dgParts"
                 ItemsSource="{Binding Parts}"
                 VirtualizationMode="Row"  <!-- 行虚拟化 -->
                 EnableSmartColumnsGeneration="True">
    
    <dxg:GridView x:Name="gvParts"
                  AutoWidth="True"
                  NavigationStyle="Row"
                  ShowAutoFilterRow="True"  <!-- 自动筛选行（替代PB的筛选）-->
                  BestFitMode="AllRows">
        
        <!-- 列定义（只渲染可见列）-->
        <dxg:GridColumn FieldName="partno" Header="件号" Width="100"/>
        <dxg:GridColumn FieldName="name" Header="名称" Width="200"/>
        <dxg:GridColumn FieldName="carname" Header="适用车型" Width="150"/>
        <dxg:GridColumn FieldName="stock" Header="库存" Width="80"/>
        <dxg:GridColumn FieldName="lsprice" Header="零售价" Width="100">
            <dxg:GridColumn.EditSettings>
                <dxe:TextEditSettings MaskType="Numeric" MaskUseAsDisplayFormat="True"
                                       DisplayFormat="c2"/>  <!-- 货币格式 -->
            </dxg:GridColumn.EditSettings>
        </dxg:GridColumn>
    </dxg:GridView>
</dxg:GridControl>

// 后台代码：异步加载数据（不阻塞UI）
public async Task LoadPartsAsync(string keyword = null)
{
    IsLoading = true;  // 显示等待指示器
    
    // 在后台线程查询数据库
    var parts = await Task.Run(() => 
    {
        using var db = DbConnectionFactory.Create();
        return db.Query<PartData>(@"
            SELECT TOP 100000 partid, partno, name, carname, 
                   unit, stock, lsprice, inprice, namePy, className
            FROM part_data 
            WHERE del IS NULL OR del <> 'Y'
            ORDER BY partid").ToList();
    });
    
    Parts.Clear();
    foreach (var part in parts)
    {
        Parts.Add(part);  // ObservableCollection自动通知UI更新
    }
    
    IsLoading = false;
}
```

##### 2. ODBC/ADO.NET直连性能优化（兼容原PB的ODBC连接方式）

```csharp
// 连接配置（appsettings.json）
{
  "ConnectionStrings": {
    "QipeiDb": "Server=localhost;Database=qipei;User Id=sa;Password={pwd};TrustServerCertificate=True;Max Pool Size=100;Connection Timeout=30;"
  }
}

// 高性能查询示例（Dapper + 异步）
public class PartRepository : BaseRepository<PartData>
{
    public async Task<PagedResult<PartData>> GetPagedAsync(
        PartQueryCriteria criteria, int page = 1, int pageSize = 50)
    {
        using var conn = GetConnection();  // 从连接池获取
        
        // 分页查询（避免全表扫描）
        var sql = @"
            SELECT * FROM (
                SELECT ROW_NUMBER() OVER (ORDER BY p.partid) AS RowNum,
                       p.*
                FROM part_data p WITH(NOLOCK)
                WHERE (@Keyword IS NULL OR 
                       p.name LIKE '%' + @Keyword + '%' OR 
                       p.partno LIKE '%' + @Keyword + '%' OR
                       p.namePy LIKE '%' + @Keyword + '%')
                  AND (@ClassId IS NULL OR p.classId = @ClassId)
                  AND (p.del IS NULL OR p.del <> 'Y')
            ) AS Paged
            WHERE RowNum BETWEEN @StartRow AND @EndRow
            ORDER BY RowNum";
            
        var parameters = new
        {
            Keyword = criteria.Keyword,
            ClassId = criteria.ClassId,
            StartRow = (page - 1) * pageSize + 1,
            EndRow = page * pageSize
        };
        
        // 异步执行（不阻塞UI线程）
        var data = await conn.QueryAsync<PartData>(sql, parameters);
        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM part_data WHERE ...", parameters);
            
        return new PagedResult<PartData>(data, total, page, pageSize);
    }
}
```

##### 3. 内存管理策略

```csharp
// 使用分页加载 + 虚拟滚动，内存恒定
// 即使数据库有10万行，内存中只保持~100行数据

// 配置DevExpress DataGrid的分页模式
dgParts.DataContext = new PagingDataSource(
    async (page, size) => await _partRepo.GetPagedAsync(currentCriteria, page, size),
    pageSize: 50  // 每页50行（可视区域约显示30-40行）
);

// 内存占用估算：
// 10万行 × 每行1KB = 100MB（如果全部加载）❌
// 虚拟滚动后：只缓存3页 = 150行 × 1KB = 150KB ✅
// 内存节省99.85%！
```

#### 🎯 性能指标承诺（SLA）— WPF版

| 场景          | 数据规模       | 性能目标          | 优化手段                                 | 对比Vue/Svelte                 |
| ----------- | ---------- | ------------- | ------------------------------------ | ---------------------------- |
| **配件列表加载**  | **10万+行**  | **< 300ms** ⚡ | DataGrid虚拟滚动 + ODBC/ADO.NET直连 + 异步查询 | Vue: 3-5s ❌ Svelte: 1-2s ⚠️  |
| **销售明细展示**  | **5000+行** | **< 200ms** ⚡ | 虚拟滚动 + 本地缓存                          | Vue: 1-2s ❌ Svelte: 500ms ⚠️ |
| **销售开单保存**  | 含100+明细    | **< 200ms** ⚡ | Dapper批量插入 + TransactionScope        | Web: 300-500ms               |
| **配件模糊搜索**  | 全文检索       | **< 100ms** ⚡ | SQL索引 + LIKE模糊匹配 + 异步查询              | Web: 200-500ms               |
| **报表数据统计**  | 百万级行聚合     | **< 1.5s** ⚡  | SQL聚合函数 + 存储过程 + 后台线程                | Web: 2-5s                    |
| **页面首次加载**  | 完整窗口       | **< 800ms** ⚡ | AOT编译 + 延迟加载 + 并行初始化                 | Web: 1.5-3s                  |
| **操作响应时间**  | 点击/输入      | **< 50ms** ⚡  | 本地计算 + UI线程优先 + 避免同步IO               | Web: 100-200ms               |
| **Excel导出** | 1万行数据      | **< 2s** ⚡    | NPOI后台线程生成 + 进度提示                    | Web: 5-10s                   |

#### Scenario: 技术架构确认

* **WHEN** 架构评审委员会审查WPF/.NET 8技术选型方案

* **THEN** 输出完整的技术栈对比报告、架构图、性能基准测试结果、DevExpress组件演示

***

### Requirement: 功能完整性保证（200+功能点100%覆盖）

系统 SHALL 实现原项目的全部功能域，不允许遗漏任何功能点：

#### 4.1 进销存管理模块（45+功能点）

##### 采购管理（12个功能）

* [ ] 采购开单（w\_buy）：供应商选择、配件录入、入库确认、多支付方式

* [ ] 采购编辑（w\_buy\_edit）：修改未审核采购单、变更明细

* [ ] 采购查询（w\_buy\_query）：按日期/供应商/状态筛选

* [ ] 采购退货（w\_buy\_th）：退货开单、扣减库存、退款处理

* [ ] 采购对账（w\_buy\_balance）：供应商对账单生成

* [ ] 采购打印（w\_print\_preview\_buy）：采购单据打印预览

* [ ] 进货订货（bill\_jhdh）：向供应商发起订货

* [ ] 订货管理：订货单跟踪、到货确认

* [ ] 供应商选择器（w\_pop\_supplier）：模糊搜索、拼音检索

* [ ] 配件选择器（w\_part\_choose）：分类树+列表、多条件筛选

* [ ] 批量采购：Excel导入采购清单

* [ ] 采购报表：采购汇总、供应商统计、价格趋势

##### 销售管理（18个功能）

* [ ] 销售开单（w\_sell）：客户选择、配件录入、折扣计算、多支付

* [ ] 销售编辑（w\_sell\_edit）：修改未结算销售单

* [ ] 销售查询（w\_sell\_query）：多维度查询筛选

* [ ] 销售退货（w\_sell\_th）：退货处理、库存回补

* [ ] 销售换货：换货流程管理

* [ ] 销售对账（w\_sell\_balance\*/w\_sell\_balance\_jhxs）：客户对账

* [ ] 销售打印（w\_print\_preview\_sell\*）：6种销售单据模板

* [ ] 零售开单（shop表POS模式）：快速扫码销售

* [ ] 批量销售：批量出库、套餐销售

* [ ] 会员销售（w\_sellhy）：会员卡刷卡、自动折扣

* [ ] 销售转单（w\_selltoxs/w\_selltobj）：订单转销售单

* [ ] 客户选择器（w\_pop\_client\*）：拼音搜索、欠款提示

* [ ] 发货管理：物流信息录入、发货单打印

* [ ] 销售排行榜（w\_top\_sell）：按金额/数量排名

* [ ] TOP客户（w\_top\_client）：客户贡献度分析

* [ ] 销售日报/月报：销售趋势图表

* [ ] 业务员业绩（w\_top\_user）：个人销售统计

* [ ] 发货单管理（w\_fax/w\_fax\*系列）：传真报价、发货通知

##### 库存管理（15个功能）

* [ ] 库存查询（w\_part\_choose）：实时库存查看

* [ ] 库存更新（w\_part\_up）：手动调整库存

* [ ] 价格管理（w\_price/w\_price\_order）：多级价格设置

* [ ] 报价管理（w\_baojia/w\_faxkcbj）：成本报价、库存报价

* [ ] 库存预警：低库存报警、缺货提醒

* [ ] 库存盘点：盘点单录入、盈亏统计

* [ ] 批次管理（part\_pdb）：先进先出FIFO批次跟踪

* [ ] 仓位管理（part\_place）：多仓库/多仓位

* [ ] 配件档案（w\_edit\_part）：配件主数据维护

* [ ] 配件分类：三级分类树管理

* [ ] 条码管理：条码生成、扫描入库

* [ ] 图片管理：配件图片上传/查看

* [ ] 替代件管理（part\_sub）：替代关系维护

* [ ] 库存台账：进出存明细账

* [ ] 库存报表：库存汇总、呆滞料分析

#### 4.2 客户关系管理模块（25+功能点）

* [ ] 客户档案（w\_edit\_client）：新增/编辑/删除/查询

* [ ] 客户信息：名称、联系人、电话、地址、银行账户

* [ ] 客户等级：VIP/普通/一般分级管理

* [ ] 信用额度：授信额度设置、超额预警

* [ ] 客户分类：零售/批发/修理厂分类

* [ ] 欠款管理（w\_arrear系列8个窗口）：欠款查询/催款/收款/对账

* [ ] 收款方式：现金/支票/支付宝/微信/银行卡

* [ ] 车辆档案（car\_mark）：车牌/车型/VIN码/照片

* [ ] 供应商管理（w\_edit\_supplier）：供应商CRUD

* [ ] 供应商评级：A/B/C/D级供应商评估

* [ ] 供应商对账：应付账款管理

* [ ] 物流商管理（wuliu\_infor）：物流商信息

* [ ] 客户购买记录：历史订单查看

* [ ] 客户统计：消费频次/金额/偏好分析

* [ ] 生日提醒：客户/会员生日提醒

* [ ] 回访提醒：定期回访计划

* [ ] 客户弹窗选择器（w\_pop\_client\*系列5个）：多种场景选择器

* [ ] 供应商弹窗（w\_pop\_supplier\*系列2个）

* [ ] 车辆弹窗（w\_pop\_car\_mark）

* [ ] 地区管理（area）：省市区三级地区字典

#### 4.3 财务管理模块（30+功能点）【已移除维修厂管理模块(50+功能点)】

##### 账户管理（10个功能）

* [ ] 账户设置（account）：现金/银行/支付宝/微信/运费账户

* [ ] 账户CRUD：新增/编辑/停用/启用

* [ ] 余额查看：实时余额显示

* [ ] 账户选择器（w\_pop\_account\*系列3个）：多场景账户选择

* [ ] 现金账户（w\_account\_xj）：现金日记账

* [ ] 银行账户（w\_account\_yh）：银行存款账

* [ ] 会员账户（w\_account\_hy）：会员卡资金池

* [ ] 运费账户（w\_account\_yunfei）：物流费用专户

* [ ] 账户转账：账户间资金划转

* [ ] 日结/月结：账户期间汇总

##### 收支管理（8个功能）

* [ ] 收款录入（pays表）：销售收款登记

* [ ] 付款录入：采购付款登记

* [ ] 费用支出：日常费用报销

* [ ] 其他收入：非经营性收入

* [ ] 收支明细（w\_account\_query\*系列4个）：多维度查询

* [ ] 收支对账：银行对账单核对

* [ ] 充值管理（w\_chongzhi）：账户充值

* [ ] 收支报表：收支趋势图

##### 欠款管理（6个功能）

* [ ] 应收欠款（arrearage type=1）：客户欠款管理

* [ ] 应付欠款（arrearage type=2）：供应商欠款管理

* [ ] 欠款查询（w\_arrear系列6个）：按客户/日期/金额

* [ ] 催款管理：逾期欠款提醒

* [ ] 收款方式（w\_arrear\_fkfs\*2个）：分期付款/一次性

* [ ] 欠款报表：账龄分析/坏账预估

##### 凭证与科目（2个功能）【已移除在线支付模块(4个功能)】

* [ ] 会计凭证（voucher）：凭证录入/审核/记账

* [ ] 科目管理（subject）：科目体系维护

#### 4.5 会员与借还管理模块（15+功能点）

* [ ] 会员卡管理（w\_hykc/w\_hykcbj\*3个）：会员卡全生命周期

* [ ] 借还管理（w\_borrow）：借出登记

* [ ] 借还编辑（w\_borrow\_edit\*2个）：借还信息修改

* [ ] 借还退货（w\_lend\_th）：借还退回

* [ ] 借还结算（w\_lend\_balance）：借还对账

* [ ] 借还查询：按人员/配件/日期

* [ ] 借还统计：借还频次/超期提醒

* [ ] 借还规则：借还期限/超期罚款设置

#### 4.6 系统管理与报表模块（40+功能点）

##### 用户权限（8个功能）

* [ ] 用户管理（w\_edituser/w\_edituser1）：用户新增/编辑/禁用

* [ ] 角色管理（groups表）：角色定义/权限分配

* [ ] 权限规则（rules表）：细粒度权限控制

* [ ] 菜单权限（mnu表）：菜单级别权限

* [ ] 密码管理（w\_user\_password）：密码修改/重置

* [ ] 用户组切换（w\_change\_user）：多身份切换【已移除磁盘绑定/软件授权功能】

* [ ] 操作日志（sys\_log）：全程操作审计

##### 数据管理（6个功能）

* [ ] 数据备份（w\_database\_backup\*3个）：全量/增量备份

* [ ] 数据恢复（w\_database\_re\*2个）：备份恢复

* [ ] 数据同步（down\*系列12个表）：连锁店数据同步

* [ ] FTP配置（ftpsz）：FTP服务器设置

* [ ] 数据清理：历史数据归档

* [ ] 数据校验：数据完整性检查

##### 系统设置（10个功能）

* [ ] 参数设置（w\_set\_config）：全局参数配置

* [ ] 默认值（w\_set\_default）：默认字段值

* [ ] 隐藏项（w\_set\_hide）：可选功能开关

* [ ] 打印设置（w\_setprint/w\_print\_define\*2个）：打印机/纸张

* [ ] 公司信息（business\_infor）：企业资料

* [ ] 编码规则（bs\_code）：单据号生成规则

* [ ] 地区设置（area）：省市区数据

* [ ] 帮助文档（helpfile）：在线帮助

* [ ] 在线升级（w\_app\_updatefile）：版本更新【已移除软件授权/系统注册功能】

##### 报表中心（16+功能）

* [ ] 销售报表：销售明细/汇总/排行/趋势

* [ ] 采购报表：采购明细/汇总/供应商分析

* [ ] 库存报表：库存台账/盘点/预警/呆滞

* [ ] 财务报表：资产负债/损益/现金流

* [ ] 会员报表：充值/消费/流失【已移除维修报表】

* [ ] 打印预览（w\_print\_preview\*20+个）：各类单据预览

* [ ] 导出功能：Excel/PDF/HTML多格式导出

* [ ] 图表展示：柱状图/折线图/饼图

* [ ] 自定义报表：用户自定义查询条件

#### Scenario: 功能完整性验证

* **WHEN** 功能测试团队逐项对照原系统进行功能验收

* **THEN** 所有200+功能点全部通过测试，无遗漏功能

***

### Requirement: UI界面1:1还原（原285个窗口→约250个WPF Window/UserControl，已移除维修模块）

系统 SHALL 将原PowerBuilder的约250个窗口（已移除维修模块50+窗口）还原为WPF窗口/UserControl，保持视觉风格和交互体验：

#### 5.1 UI还原原则（WPF特有优势）

1. **MDI框架完美还原**：

   * PB: w\_main作为MDI框架窗口，包含菜单栏、工具栏、状态栏、子窗口区

   * WPF: MainWindow + DevExpress DXDockingManager（原生MDI支持）

   * 子窗口可以浮动、停靠、层叠、平铺（与PB行为一致）

2. **控件映射（PB→WPF几乎1:1）**：

   * DataWindow Grid → DevExpress GridControl（功能更强）

   * DataWindow Freeform → DevExpress DataForm / 自定义UserControl

   * CommandButton → Button（支持ButtonSkin样式）

   * SingleLineEdit → TextBox（支持Mask属性）

   * MultiLineEdit → TextBox(AcceptsReturn=True)

   * EditMask → TextBox + MaskedTextBox

   * DropDownListBox → ComboBoxEdit(IsTextEditable=True)

   * CheckBox → CheckBox

   * RadioButton → RadioButton

   * GroupBox → GroupBox

   * StaticText → TextBlock

   * Tab → DXTabControl

   * TreeView → TreeListControl（增强版树形控件）

   * PictureBox → ImageControl / PictureEdit

   * ProgressBar → ProgressBarControl

   * DataWindow Graph → ChartControl（图表控件）

3. **Windows经典风格还原**：

   * 使用MahApps.Metro或MaterialDesignInXAML主题库

   * 可选：自定义XP风格主题（灰蓝色调、立体按钮效果）

   * 字体：Microsoft YaHei UI / SimSun（宋体）9pt-12pt

4. **交互一致性（WPF原生支持）**：

   * 键盘快捷键：InputBindings全局绑定

   * 右键菜单：ContextMenu PlacementTarget

   * 焦点管理：KeyboardNavigation + FocusManager

   * 拖拽操作：DragDrop（支持窗口间拖拽）

   * 消息框：MessageBox.Show()（与PB几乎一致）

5. **响应式适配**：

   * WPF原生支持DPI缩放（高分辨率显示器适配）

   * ViewBox实现整体缩放

   * Grid/StackPanel自适应布局

#### 5.2 PB控件→WPF组件详细映射表

| PB控件                        | WPF/DevExpress组件                        | 关键属性/方法                                                       | 还原度      |
| --------------------------- | --------------------------------------- | ------------------------------------------------------------- | -------- |
| **DataWindow (Grid)**       | `DXGridControl` + `GridView`            | `VirtualizationMode=Row`, `EnableSmartColumnsGeneration=True` | **98%**  |
| **DataWindow (Freeform)**   | `DXDataForm` 或 `GridControl(TableView)` | `AutoGenerateFields=True`, `Binding`                          | **95%**  |
| **DataWindow (DropdownDW)** | `ComboBoxEdit` + `PopupContentTemplate` | `IsTextEditable=True`, `ItemsSource`                          | **95%**  |
| **CommandButton**           | `Button` 或 `SimpleButton`               | `Command`, `InputGestureText`, `ToolTip`                      | **100%** |
| **SingleLineEdit**          | `TextEdit`                              | `MaskType`, `MaxLength`, `CharacterCasing`                    | **100%** |
| **MultiLineEdit**           | `MemoEdit`                              | `AcceptsReturn=True`, `ScrollBars=Vertical`                   | **100%** |
| **EditMask**                | `TextEdit` with `MaskProperties`        | `MaskType=DateTime/Numeric/RegEx`, `EditMask`                 | **100%** |
| **DropDownListBox**         | `ComboBoxEdit`                          | `ItemsSource`, `DisplayMember`, `ValueMember`                 | **100%** |
| **CheckBox**                | `CheckBox`                              | `IsChecked`, `Content`, `Command`                             | **100%** |
| **RadioButton**             | `RadioButton`                           | `GroupName`, `IsChecked`, `Content`                           | **100%** |
| **GroupBox**                | `GroupBox`                              | `Header`, `Content`                                           | **100%** |
| **StaticText**              | `TextBlock`                             | `Text`, `Foreground`, `FontWeight`                            | **100%** |
| **Tab**                     | `DXTabControl`                          | `Tabs`, `SelectedTabIndex`, `CloseTabButtonShowMode`          | **98%**  |
| **TreeView**                | `TreeListControl` 或原生`TreeView`         | `ItemsSource`, `KeyFieldName`, `ParentFieldName`              | **97%**  |
| **PictureBox**              | `PictureEdit` 或 `ImageControl`          | `Source`, `Stretch=Uniform`, `ShowMenu`                       | **95%**  |
| **ProgressBar**             | `ProgressBarControl`                    | `Minimum`, `Maximum`, `Value`, `ShowTitle=True`               | **100%** |
| **HScrollBar/VScrollBar**   | `ScrollBar` (内置)                        | `Orientation`, `Minimum`, `Maximum`, `Value`                  | **100%** |
| **DataWindow (Graph)**      | `ChartControl` (DevExpress)             | `Diagram`, `Series`, `Legend`                                 | **90%**  |
| **UserObject (u\_\*)**      | `UserControl` (WPF)                     | 自定义XAML模板                                                     | **100%** |

#### 5.3 核心界面原型还原规格（10个关键界面）

##### 5.3.1 登录界面（w\_check → LoginWindow\.xaml）

```xml
<!-- WPF登录窗口 - 类似PB的经典居中对话框 -->
<Window x:Class="QP11.Wpf.Views.LoginWindow"
        Title="QP11汽配管理系统 - 用户登录"
        Height="320" Width="420"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        WindowStyle="SingleBorderWindow">
    
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- 标题 -->
        <TextBlock Text="🚗 QP11 汽配管理系统" 
                   FontSize="20" FontWeight="Bold" 
                   HorizontalAlignment="Center" Margin="0,0,0,20"/>
        
        <!-- 登录表单 -->
        <StackPanel Grid.Row="1" VerticalAlignment="Center">
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="👤 用户名:" Width="80" VerticalAlignment="Center"/>
                <ComboBoxEdit x:Name="cboUsers" Width="250" IsTextEditable="True"
                             DisplayMember="Username" AutoComplete="True"/>
            </StackPanel>
            
            <StackPanel Orientation="Horizontal" Margin="0,10,0,5">
                <TextBlock Text="🔒 密  码:" Width="80" VerticalAlignment="Center"/>
                <PasswordBox x:Name="txtPassword" Width="250" 
                            PasswordChar="●"/>
            </StackPanel>
            
            <CheckBox x:Name="chkRememberPwd" Content="记住密码" Margin="85,5,0,0"/>
        </StackPanel>
        
        <!-- 按钮 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" 
                   HorizontalAlignment="Center" Margin="0,15,0,10">
            <Button Content="🔑 登 录" Width="100" Height="32" Margin="10,0"
                    Command="{Binding LoginCommand}" 
                    IsDefault="True"/>  <!-- Enter键触发 -->
            <Button Content="取 消" Width="100" Height="32" Margin="10,0"
                    Command="{Binding CancelCommand}"
                    IsCancel="True"/>   <!-- Esc键触发 -->
        </StackPanel>
        
        <!-- 版本信息 -->
        <TextBlock Grid.Row="3" Text="版本号: V2.0.2026 (.NET 8)" 
                  HorizontalAlignment="Center" Foreground="Gray" FontSize="10"/>
    </Grid>
</Window>
```

* 尺寸：420×320px 居中显示（ResizeMode=NoResize固定大小）

* 验证：用户名+密码+可选验证码

* 快捷键：Enter登录(IsDefault)、Esc取消(IsCancel)

* 记住密码：IsolatedStorage或Registry存储（比Web的localStorage更安全）

##### 5.3.2 主界面（w\_main → MainWindow\.xaml）【MDI框架】

```xml
<!-- WPF MDI主窗口 - 使用DevExpress Docking实现多文档界面 -->
<dxdo:DockingManager x:Name="dockManager">
    <dxdo:LayoutRoot>
        <dxdo:LayoutPanel Orientation="Vertical">
            
            <!-- 顶部区域：菜单栏 + 工具栏 -->
            <dxdo:DocumentPanel Caption="主框架" CanDock="False" CanFloat="False">
                <DockPanel>
                    
                    <!-- 菜单栏（模拟PB的主菜单 m_main）-->
                    <Menu DockPanel.Dock="Top">
                        <MenuItem Header="文件(_F)">
                            <MenuItem Header="数据备份..." Command="{Binding BackupCommand}"/>
                            <MenuItem Header="数据恢复..." Command="{Binding RestoreCommand}"/>
                            <Separator/>
                            <MenuItem Header="退出" Command="{Binding ExitCommand}" InputGestureText="Alt+F4"/>
                        </MenuItem>
                        <MenuItem Header="编辑(_E)">
                            <MenuItem Header="复制 Ctrl+C" Command="{Binding CopyCommand}"/>
                            <MenuItem Header="粘贴 Ctrl+V" Command="{Binding PasteCommand}"/>
                        </MenuItem>
                        <MenuItem Header="采购(_P)" ItemsSource="{Binding PurchaseMenus}"/>
                        <MenuItem Header="销售(_S)" ItemsSource="{Binding SellMenus}"/>
                        <MenuItem Header="库存(_K)" ItemsSource="{Binding StockMenus}"/>
                        <MenuItem Header="财务(_C)" ItemsSource="{Binding FinanceMenus}"/>【已移除维修菜单】
                        <MenuItem Header="会员(_M)" ItemsSource="{Binding MemberMenus}"/>
                        <MenuItem Header="报表(_R)" ItemsSource="{Binding ReportMenus}"/>
                        <MenuItem Header="帮助(_H)">
                            <MenuItem Header="关于..."/>
                        </MenuItem>
                    </Menu>
                    
                    <!-- 工具栏（对应toolbar目录）-->
                    <ToolBarTray DockPanel.Dock="Top">
                        <ToolBar>
                            <Button Content="💾" ToolTip="保存(F5)" Command="{Binding SaveCommand}"
                                   InputGestureText="F5"/>
                            <Button Content="➕" ToolTip="新增(F3)" Command="{Binding AddCommand}"
                                   InputGestureText="F3"/>
                            <Button Content="✏️" ToolTip="编辑" Command="{Binding EditCommand}"/>
                            <Button Content="🗑️" ToolTip="删除(Del)" Command="{Binding DeleteCommand}"
                                   InputGestureText="Delete"/>
                            <Separator/>
                            <Button Content="🖨️" ToolTip="打印(Ctrl+P)" Command="{Binding PrintCommand}"
                                   InputGestureText="Ctrl+P"/>
                            <Button Content="🔄" ToolTip="刷新(F9)" Command="{Binding RefreshCommand}"
                                   InputGestureText="F9"/>
                            <Separator/>
                            <Button Content="📤" ToolTip="导出Excel" Command="{Binding ExportExcelCommand}"/>
                        </ToolBar>
                    </ToolBarTray>
                    
                    <!-- MDI子窗口容器（对应PB的客户区）-->
                    <dxdo:DocumentGroup x:Name="documentGroup" MDIStyle="MDI">
                        <!-- 子窗口将动态添加到这里 -->
                    </dxdo:DocumentGroup>
                </DockPanel>
            </dxdo:DocumentPanel>
            
            <!-- 底部状态栏 -->
            <dxdo:LayoutPanel Height="24" CanDock="False" CanFloat="False">
                <StatusBar>
                    <StatusBarItem>
                        <TextBlock Text="{Binding CurrentUser, StringFormat='用户: {0}'}"/>
                    </StatusBarItem>
                    <Separator/>
                    <StatusBarItem>
                        <TextBlock Text="{Binding CurrentTime, StringFormat='{}{0:yyyy-MM-dd HH:mm:ss}'}"/>
                    </StatusBarItem>
                    <StatusBarItem HorizontalAlignment="Right">
                        <TextBlock Text="就绪"/>
                    </StatusBarItem>
                </StatusBar>
            </dxdo:LayoutPanel>
        </dxdo:LayoutPanel>
    </dxdo:LayoutRoot>
</dxdo:DockingManager>
```

* 布局：经典三段式（顶栏\[菜单+工具栏] + 中间\[MDI文档组] + 底栏\[状态栏]）

* MDI支持：子窗口可浮动、停靠、最大化、最小化、层叠、平铺

* 菜单栏：8-10个一级菜单（文件/编辑/采购/销售/库存/维修/财务/会员/报表/帮助）

* 工具栏：常用按钮（保存F5/新增F3/删除Del/打印Ctrl+P/刷新F9）

* 状态栏：当前用户、实时时间、系统状态

* 窗口管理：打开/关闭/刷新/排列子窗口

##### 5.3.3 销售开单界面（w\_sell → SellOrderWindow\.xaml）

```xml
<!-- 销售开单窗口 - Master-Detail布局（上表头+下明细）-->
<Window x:Class="QP11.Wpf.Views.SellOrderWindow"
        Title="销售开单" Height="600" Width="900"
        WindowStartupLocation="CenterOwner">
    
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>   <!-- 表头区域 -->
            <RowDefinition Height="*"/>      <!-- 明细区域 -->
            <RowDefinition Height="Auto"/>   <!-- 操作按钮区 -->
            <RowDefinition Height="Auto"/>   <!-- 合计/支付区 -->
        </Grid.RowDefinitions>
        
        <!-- 【表头区域】- 对应PB的上半部分 -->
        <GroupBox Grid.Row="0" Header="基本信息" Margin="0,0,0,5">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="200"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="150"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Text="客户:" VerticalAlignment="Center" Margin="5"/>
                <ComboBoxEdit Grid.Column="1" x:Name="cboClient" IsTextEditable="True"
                              DisplayMember="Name" ValueMember="Cid"
                              SelectedItem="{Binding SelectedClient}">
                    <dxe:ComboBoxEdit.StyleSettings>
                        <dxe:ComboBoxStyleSettings AllowNullInput="False"/>
                    </dxe:ComboBoxEdit.StyleSettings>
                </ComboBoxEdit>
                <Button Grid.Column="2" Content="🔍" Width="30" Margin="5,0"
                       Command="{Binding SelectClientCommand}"/>
                
                <TextBlock Grid.Column="3" Text="单号:" VerticalAlignment="Center" Margin="10,5,5,5"/>
                <TextBox grid.Column="4" Text="{Binding BillNo, Mode=OneWay}" 
                        IsReadOnly="True" Background="LightGray" Margin="0,5,5,5"/>
                
                <TextBlock Grid.Column="5" Text="(自动生成)" Foreground="Gray" 
                          VerticalAlignment="Center" Margin="5"/>
            </Grid>
            
            <!-- 第二行 -->
            <Grid Grid.Row="1" Margin="0,5,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="100"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <TextBlock Text="日期:" VerticalAlignment="Center" Margin="5"/>
                <DateEdit grid.Column="1" x:Name="dtBillDate" 
                         DateTime="{Binding BillDate}"/>
                
                <TextBlock grid.Column="2" Text="业务员:" VerticalAlignment="Center" Margin="10,5,5,5"/>
                <ComboBoxEdit grid.Column="3" x:Name="cboWorker"
                             ItemsSource="{Binding Workers}" DisplayMember="Name"/>
                
                <TextBlock grid.Column="4" Text="类型:" VerticalAlignment="Center" Margin="10,5,5,5"/>
                <ComboBoxEdit grid.Column="5" x:Name="cboType"
                             SelectedIndex="{Binding SellType}"/>
            </Grid>
            
            <!-- 备注 -->
            <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,5,0,0">
                <TextBlock Text="备注:" VerticalAlignment="Center" Margin="5"/>
                <TextBox Width="400" Text="{Binding Remark}" 
                        AcceptsReturn="True" MaxLength="200"/>
            </StackPanel>
        </GroupBox>
        
        <!-- 【明细区域】- 对应PB的下半部分DataWindow Grid -->
        <GroupBox Grid.Row="1" Header="销售明细">
            <DockPanel>
                <!-- 右侧合计信息 -->
                <StackPanel DockPanel.Dock="Right" Width="150" Margin="10,0,0,0">
                    <TextBlock Text="合计金额:" FontWeight="Bold" FontSize="14"/>
                    <TextEdit x:Name="txtTotalAmount" 
                             EditValue="{Binding TotalAmount, Mode=OneWay, StringFormat=c2}"
                             IsReadOnly="True" FontSize="16" Foreground="Red"
                             HorizontalContentAlignment="Right"/>
                    
                    <TextBlock Text="折扣率:" Margin="0,10,0,0"/>
                    <SpinEdit x:Name="seDiscountRate" 
                             EditValue="{Binding DiscountRate}"
                             MinValue="0.01" MaxValue="1" Increment="0.05"
                             FormatString="p0"/>
                    
                    <TextBlock Text="应收金额:" Margin="0,10,0,0" FontWeight="Bold"/>
                    <TextEdit x:name="txtBillTotal"
                             EditValue="{Binding BillTotal, Mode=OneWay, StringFormat=c2}"
                             IsReadOnly="True" FontSize="14" Foreground="Blue"/>
                </StackPanel>
                
                <!-- 左侧DataGrid（核心！使用DevExpress虚拟滚动）-->
                <dxg:GridControl x:Name="dgDetails"
                                 ItemsSource="{Binding Details}"
                                 VirtualizationMode="Row"  <!-- 🔑 虚拟滚动：只渲染可见行 -->
                                 SelectedItems="{Binding SelectedDetails}"
                                 CurrentItem="{Binding CurrentDetail}">
                    <dxg:TableView x:Name="tvDetails"
                                  AutoWidth="True"
                                  NavigationStyle="Cell"
                                  ShowAutoFilterRow="True"  <!-- 自动筛选行 -->
                                  EditorShowMode="MouseUp"
                                  NewItemRowPosition="Bottom">  <!-- 新增行在底部 -->
                        
                        <!-- 列定义（对应PB DataWindow的字段）-->
                        <dxg:GridColumn FieldName="PartNo" Header="配件编码" Width="100">
                            <dxg:GridColumn.EditSettings>
                                <dxe:TextEditSettings AllowNullInput="False"/>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <dxg:GridColumn FieldName="PartName" Header="名称" Width="180">
                            <dxg:GridColumn.EditSettings>
                                <dxe:ButtonEditSettings AllowDefaultButton="False">
                                    <!-- 选择配件按钮 -->
                                    <dxe:ButtonEditSettings.Buttons>
                                        <dxe:ButtonInfo Kind="Search" 
                                                     Command="{Binding SelectPartCommand}"
                                                     IsEnabled="{Binding IsEditing}"/>
                                    </dxe:ButtonEditSettings.Buttons>
                                </dxe:ButtonEditSettings>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <dxg:GridColumn FieldName="Spec" Header="规格" Width="100" ReadOnly="True"/>
                        <dxg:GridColumn FieldName="Unit" Header="单位" Width="60" ReadOnly="True"/>
                        
                        <dxg:GridColumn FieldName="Price" Header="单价" Width="100">
                            <dxg:GridColumn.EditSettings>
                                <dxe:SpinEditSettings MaskType="Numeric" 
                                                  DisplayFormat="c2" 
                                                  MinValue="0" MaxValue="999999"/>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <dxg:GridColumn FieldName="Quantity" Header="数量" Width="80">
                            <dxg:GridColumn.EditSettings>
                                <dxe:SpinEditSettings MaskType="Numeric" 
                                                  MinValue="0.001" MaxValue="99999"
                                                  Increment="1"/>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <dxg:GridColumn FieldName="DiscountRate" Header="折扣%" Width="70">
                            <dxg:GridColumn.EditSettings>
                                <dxe:SpinEditSettings MaskType="Numeric" 
                                                  DisplayFormat="p0"
                                                  MinValue="0" MaxValue="1" Increment="0.05"/>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <dxg:GridColumn FieldName="SubTotal" Header="小计" Width="100" ReadOnly="True">
                            <dxg:GridColumn.EditSettings>
                                <dxe:TextEditSettings MaskType="Numeric" 
                                                  DisplayFormat="c2" IsReadOnly="True"/>
                            </dxg:GridColumn.EditSettings>
                        </dxg:GridColumn>
                        
                        <!-- 删除按钮列 -->
                        <dxg:GridColumn Header="操作" Width="60" Fixed="Right">
                            <dxg:GridColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="✖" Command="{Binding View.DataContext.DeleteDetailCommand}"
                                           CommandParameter="{Binding Row.Data.Row}" 
                                           Width="25" Height="22" Margin="2"
                                           ToolTip="删除此行(Del)"/>
                                </DataTemplate>
                            </dxg:GridColumn.CellTemplate>
                        </dxg:GridColumn>
                    </dxg:TableView>
                </dxg:GridControl>
            </DockPanel>
        </GroupBox>
        
        <!-- 【操作按钮区】-->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center" Margin="5">
            <Button Content="➕ 添加(F3)" Width="100" Height="32" Margin="5"
                   Command="{Binding AddDetailCommand}" InputGestureText="F3"/>
            <Button Content="💾 保 存(F5)" Width="100" Height="32" Margin="5"
                   Command="{Binding SaveCommand}" InputGestureText="F5"/>
            <Button Content="🖨️ 打 印(P)" Width="100" Height="32" Margin="5"
                   Command="{Binding PrintCommand}" InputGestureText="Ctrl+P"/>
            <Button Content="❌ 退 出(Esc)" Width="100" Height="32" Margin="5"
                   Command="{Binding CloseCommand}" InputGestureText="Esc"/>
        </StackPanel>
        
        <!-- 【多支付方式区】-->
        <GroupBox Grid.Row="3" Header="支付方式">
            <Grid Margin="10">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                
                <CheckBox grid.Column="0" Content="现金:" IsChecked="{Binding PayCash}" VerticalAlignment="Center"/>
                <TextEdit grid.Column="1" MaskType="Numeric" DisplayFormat="c2"
                         EditValue="{Binding CashAmount}"/>
                
                <CheckBox grid.Column="2" Content="微信:" IsChecked="{Binding PayWeixin}" VerticalAlignment="Center"/>
                <TextEdit grid.Column="3" MaskType="Numeric" DisplayFormat="c2"
                         EditValue="{Binding WeixinAmount}"/>
                
                <CheckBox grid.Column="4" Content="支付宝:" IsChecked="{Binding PayZhifubao}" VerticalAlignment="Center"/>
                <TextEdit grid.Column="5" MaskType="Numeric" DisplayFormat="c2"
                         EditValue="{Binding ZhifubaoAmount}"/>
                
                <CheckBox grid.Column="6" Content="挂账:" IsChecked="{Binding PayCredit}" VerticalAlignment="Center"/>
                <TextEdit grid.Column="7" MaskType="Numeric" DisplayFormat="c2"
                         EditValue="{Binding CreditAmount}"/>
                
                <TextBlock grid.Column="8" Text="实收:" FontWeight="Bold" VerticalAlignment="Center" Margin="10,0,5,0"/>
                <TextEdit grid.Column="9" MaskType="Numeric" DisplayFormat="c2" Foreground="Red" FontSize="14"
                         EditValue="{Binding TotalPayment, Mode=OneWay}" IsReadOnly="True"/>
            </Grid>
        </GroupBox>
    </Grid>
    
    <!-- 全局快捷键绑定 -->
    <Window.InputBindings>
        <KeyBinding Key="F5" Command="{Binding SaveCommand}"/>
        <KeyBinding Key="F3" Command="{Binding AddDetailCommand}"/>
        <KeyBinding Key="Delete" Command="{Binding DeleteDetailCommand}"/>
        <KeyBinding Key="Escape" Command="{Binding CloseCommand}"/>
        <KeyBinding Key="S" Modifiers="Control" Command="{Binding SaveCommand}"/>
        <KeyBinding Key="P" Modifiers="Control" Command="{Binding PrintCommand}"/>
    </Window.InputBindings>
</Window>
```

* 布局：上下分栏（上表头GroupBox + 下明细GroupBox(DataGrid) + 底部按钮+支付）

* 明细区：DevExpress GridControl开启虚拟滚动（VirtualizationMode=Row），即使10000+明细也流畅

* 自动计算：数量×单价×折扣=小计（ViewModel中实现INotifyPropertyChanged）

* 多支付方式：现金/微信/支付宝/挂账组合（与PB一致）

* 快捷键：InputBindings全局绑定（F5保存/F3新增/Delete删除/Esc关闭/Ctrl+S保存/Ctrl+P打印）

##### 5.3.4 其他7个核心界面简要规格

* **配件选择器（PartSelectorWindow\.xaml）**：

  * 布局：左右分栏（左侧TreeList分类树 + 右侧GridControl配件列表）

  * 左侧树：三级分类展开/折叠（class表数据）

  * 右侧网格：虚拟滚动列表（支持排序/筛选/多选）

  * 搜索：拼音首字母/配件编码/名称模糊搜索（实时过滤）

  * 返回：选中配件集合（IList<PartData>）

* **客户档案（ClientFormWindow\.xaml）**：

  * 布局：DXTabControl标签页（基本/联系/财务/交易历史）

  * 基本页：姓名/电话/地址/身份证/等级/信用额度

  * 联系页：手机/QQ/微信/邮箱

  * 财务页：欠款余额/消费总额/最后交易时间

  * 交易页：历史订单DataGrid（双击查看详情）

* **采购管理（PurchaseOrderWindow\.xaml）**：

  * 布局：类似销售开单（Master-Detail）

  * 表头：供应商/采购单号/入库仓库/经手人

  * 明细：采购配件列表（编码/名称/采购价/数量/金额）

  * 特殊：入库确认按钮、质检标记、供应商对账链接

* **账户管理（AccountManageWindow\.xaml）**：【已移除维修接车界面】

  * 布局：左右分栏（左侧TreeList账户树 + 右侧DataGrid收支明细）

  * 左侧树：现金账户/银行账户/会员账户/运费账户（递归树形）

  * 右侧上：账户信息卡片（名称/期初/当前余额/类型）

  * 右侧下：收支明细列表（日期/摘要/收入/支出/余额/经办人）

* **打印预览（PrintPreviewWindow\.xaml）**：

  * 布局：全屏FixedDocumentViewer（所见即所得）

  * 工具栏：打印/页面设置/缩放(放大/缩小/适应宽度/适应页)/翻页(首页/上一页/下一页/末页)

  * 文档内容：FlowDocument动态生成（模拟纸张效果）

  * 页脚：页码显示（第X页/共Y页）

* **系统设置（ConfigWindow\.xaml）**：

  * 布局：DXTabControl多标签页设置界面

  * 标签1：公司信息（名称/地址/电话/税号/Logo上传）

  * 标签2：数据库设置（服务器/数据库名/连接测试按钮）

  * 标签3：打印设置（默认打印机/纸张大小/方向/边距）

  * 标签4：提醒设置（库存阈值/欠款天数/生日提前天数）

#### 5.4 特殊WPF组件开发清单（对应PB的自定义UserObject）

| 组件名                              | 对象文件                | 功能说明                                    | 复杂度   |
| -------------------------------- | ------------------- | --------------------------------------- | ----- |
| **PartSelectorControl.xaml**     | w\_part\_choose     | 配件选择器（左Tree右Grid+搜索+多选）                 | **高** |
| **ClientSelectorControl.xaml**   | w\_pop\_client      | 客户选择弹窗（带欠款提示/拼音搜索）                      | 中     |
| **SupplierSelectorControl.xaml** | w\_pop\_supplier    | 供应商选择弹窗                                 | 低     |
| **BillPreviewControl.xaml**      | w\_print\_preview\* | 单据打印预览（FlowDocument渲染）                  | 中     |
| **MultiPaymentControl.xaml**     | 自定义                 | 多支付方式组合（现金+微信+挂账等）                      | 中     |
| **StockAlertControl.xaml**       | 自定义                 | 库存预警闪烁提示（Animation）                     | 低     |
| **ShortcutMenuControl.xaml**     | m\_popupmenu        | 右键上下文菜单（ContextMenu模板）                  | 中     |
| **PinyinSearchBehavior.xaml**    | 自定义                 | 中文输入自动转拼音搜索（Attached Behavior）          | **高** |
| **DataWindowGrid.xaml**          | 自定义                 | PB DataWindow Grid的WPF封装（继承GridControl） | **高** |
| **DataWindowForm.xaml**          | 自定义                 | PB DataWindow Freeform的WPF封装（动态生成表单）    | **高** |

#### Scenario: UI还原验收

* **WHEN** UI设计师和产品经理逐一对比原系统每个PB窗口

* **THEN** 约250个WPF窗口/UserControl全部通过UI还原度验收（≥95%相似度），且大数据量操作流畅无卡顿【已移除维修模块50+窗口】

***

### Requirement: 业务逻辑完整迁移

系统 SHALL 100%保留原系统的所有业务逻辑、计算公式和校验规则：

#### 6.1 核心计算公式迁移清单（C#实现）

##### 销售相关公式（CalcService.cs）

```csharp
/// <summary>
/// 销售计算服务 - 对应PB中的计算逻辑
/// </summary>
public class CalcService : ICalcService
{
    /// <summary>
    /// 计算销售行小计
    /// 对应PB: stotal = price * amount * discount_rate;
    /// </summary>
    public decimal CalculateLineSubtotal(decimal price, decimal amount, decimal discountRate)
    {
        if (amount <= 0) throw new ArgumentException("数量必须大于0");
        if (discountRate <= 0 || discountRate > 1) throw new ArgumentException("折扣率必须在0-1之间");
        
        return Math.Round(price * amount * discountRate, 2);
    }
    
    /// <summary>
    /// 计算销售行原价小计
    /// 对应PB: btotal = bill_price * amount;
    /// </summary>
    public decimal CalculateLineOriginal(decimal billPrice, decimal amount)
    {
        return Math.Round(billPrice * amount, 2);
    }
    
    /// <summary>
    /// 计算销售单总金额
    /// 对应PB: total = SUM(detail.btotal);
    ///        bill_total = total * discount_rate;
    ///        total_payment = bill_total + yunfei;
    /// </summary>
    public SellOrderSummary CalculateSellOrderSummary(IEnumerable<SellDetail> details, 
                                                       decimal orderDiscountRate, 
                                                       decimal yunfei = 0)
    {
        var originalTotal = details.Sum(d => d.BillPrice * d.Amount);  // 原价总额
        var discountedTotal = Math.Round(originalTotal * orderDiscountRate, 2);  // 折后总额
        var totalPayment = discountedTotal + yunfei;  // 含运费应收
        
        return new SellOrderSummary
        {
            OriginalTotal = originalTotal,
            DiscountedTotal = discountedTotal,
            Yunfei = yunfei,
            TotalPayment = totalPayment
        };
    }
    
    /// <summary>
    /// 计算欠款
    /// 对应PB: arrear = total_payment - (cash + checks + cardpay + zhifubao + weixin);
    /// </summary>
    public decimal CalculateArrear(decimal totalPayment, PaymentInfo payment)
    {
        var paidAmount = payment.Cash + payment.Checks + payment.CardPay + 
                        payment.Zhifubao + payment.Weixin;
        var arrear = totalPayment - paidAmount;
        
        if (arrear < 0) arrear = 0;  // 不允许负数欠款（多收转为余额）
        
        return Math.Round(arrear, 2);
    }
    
    /// <summary>
    /// 折扣率限制检查
    /// 对应PB: if (client.level == 'VIP') max_discount = 0.70;
    ///        else if (client.level == '普通') max_discount = 0.85;
    ///        else max_discount = 0.95;
    /// </summary>
    public void ValidateDiscountRate(ClientInfor client, decimal requestedDiscount)
    {
        decimal maxAllowed;
        switch (client.Level)
        {
            case "VIP":
                maxAllowed = 0.70m;
                break;
            case "普通":
                maxAllowed = 0.85m;
                break:
            default:
                maxAllowed = 0.95m;
                break;
        }
        
        if (requestedDiscount > maxAllowed)
        {
            throw new BusinessRuleException(
                $"超出{client.Level}客户的最大折扣限制({maxAllowed:P0})");
        }
    }
    
    /// <summary>
    /// 会员折扣自动应用
    /// 对应PB: if (memberCard) final_discount = memberCard.zkl;
    /// </summary>
    public decimal ApplyMemberDiscount(MemberCard memberCard, decimal baseDiscount)
    {
        if (memberCard != null && memberCard.Status == "正常")
        {
            return memberCard.Zkl;  // 使用会员卡折扣率（通常更低）
        }
        return baseDiscount;
    }
}
```

##### 采购相关公式

```csharp
/// <summary>
/// 采购成本计算
/// 对应PB: intotal = inprice * amount;
///        total = SUM(detail.intotal);
/// </summary>
public PurchaseOrderSummary CalculatePurchaseOrder(IEnumerable<PurchaseDetail> details)
{
    var lineTotals = details.Select(d => new
    {
        LineId = d.Id,
        InTotal = Math.Round(d.InPrice * d.Amount, 2),
        OriginalTotal = Math.Round(d.BillPrice * d.Amount, 2)
    }).ToList();
    
    return new PurchaseOrderSummary
    {
        Details = lineTotals,
        TotalCost = lineTotals.Sum(lt => lt.InTotal),  // 采购总成本
        OriginalTotal = lineTotals.Sum(lt => lt.OriginalTotal)  // 原价总额
    };
}

/// <summary>
/// 入库库存增加
/// 对应PB: part_stock.amount += detail.amount;
///        part_stock.sell_use -= detail.amount;
/// </summary>
public async Task IncreaseStockOnPurchase(string partId, decimal quantity, IDbConnection db)
{
    await db.ExecuteAsync(@"
        UPDATE part_stock SET 
            amount = amount + @Qty,
            sell_use = sell_use - @Qty,
            update_time = GETDATE()
        WHERE partid = @PartId", 
        new { Qty = quantity, PartId = partId });
}
```

##### 单据编号生成规则【已移除维修相关公式】

```csharp
/// <summary>
/// 单据编号生成服务
/// 对应PB: prefix + date + padStart(seq, 4, "0")
/// 例: XS202605300001
/// </summary>
public class SerialNumberService : ISerialNumberService
{
    private readonly IRepository<SerialNumber> _serialRepo;
    
    public async Task<string> GenerateSellSN()
    {
        return await GenerateSN("XS");  // 销售前缀
    }
    
    public async Task<string> GenerateBuySN()
    {
        return await GenerateSN("CG");  // 采购前缀
    }
    
    private async Task<string> GenerateSN(string prefix)【已移除维修单据号生成】
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        
        // 从serialnumber表获取并递增序列号
        var serial = await _serialRepo.GetOrCreateAsync(prefix + today);
        serial.CurrentValue++;
        await _serialRepo.UpdateAsync(serial);
        
        return $"{prefix}{today}{serial.CurrentValue:D4}";
    }
}
```

#### 6.2 业务校验规则迁移清单（ValidationService.cs）

| 校验项      | 规则描述                                        | C#实现                                                            | 触发时机    |
| -------- | ------------------------------------------- | --------------------------------------------------------------- | ------- |
| 客户欠款超限   | `sell_use + newAmount > credit`             | `if(client.SellUse + newArrear > client.Credit) throw`          | 销售开单保存前 |
| 库存不足     | `stock.amount < sellDetail.Amount`          | `if(stock.Amount < qty) throw new InsufficientStockException()` | 销售明细添加时 |
| 单据日期不能未来 | `datetime > NOW()`                          | `if(billDate > DateTime.Now) throw new FutureDateException()`   | 所有单据窗口  |
| 必填字段非空   | 客户/配件/数量/价格不能为空                             | `[Required]` DataAnnotation 或手动校验                               | 表单提交前   |
| 价格合理性    | `price >= 0 && price < 100000`              | `Range(0, 100000)]` Attribute                                   | 明细录入时   |
| 数量为正整数   | `amount > 0 && amount == parseInt(amount)`  | 正则或类型校验                                                         | 明细录入时   |
| 折扣率范围    | `0 < discount_rate <= 1`                    | `Range(0.01, 1.0)]` Attribute                                   | 销售开单    |
| 会员卡有效    | `hykh exists && zt == '正常' && hyqx > NOW()` | 查询数据库验证                                                         | 刷卡时     |
| 会员密码正确   | `inputPwd == kmm`                           | `if(inputPwd != storedKmm) throw`                               | 密码验证    |
| 单据号唯一    | `sn not exists in table`                    | `INSERT IGNORE` 或 先查询再插入                                        | 开单时     |
| 操作权限     | `user.auth includes currentMenu`            | `[Authorize]` Attribute 或手动拦截                                   | 全局拦截器   |

#### 6.3 工作流状态机迁移（StateMachine.cs）

```csharp
/// <summary>
/// 销售单状态机 - 对应PB的状态标志(flag字段)
/// </summary>
public enum SellStatus
{
    Draft = 0,       // 草稿
    Confirmed = 1,   // 已审核
    Completed = 2,   // 已完成
    Void = 3,        // 已作废
    Cancelled = 4    // 已取消
}

/// <summary>
/// 会员卡状态机 - 对应PB的zt字段
/// </summary>【已移除维修接车单状态机(RepairStatus)】
public enum MemberCardStatus
{
    Active = 0,      // 正常
    Lost = 1,        // 挂失
    Expired = 2,     // 过期
    Renew = 3,       // 续费中
    Cancelled = 4    // 注销
}

// 状态转换规则（使用State Pattern或简单的switch-case）
public class WorkflowService : IWorkflowService
{
    public SellStatus TransitionSell(SellStatus current, SellStatus target)
    {
        // 定义合法的状态转换
        var validTransitions = new Dictionary<SellStatus, List<SellStatus>>
        {
            [SellStatus.Draft] = new() { SellStatus.Confirmed, SellStatus.Cancelled },
            [SellStatus.Confirmed] = new() { SellStatus.Completed, SellStatus.Void },
            [SellStatus.Completed] = new List<SellStatus>(),  // 终态
            [SellStatus.Void] = new List<SellStatus>(),      // 终态
            [SellStatus.Cancelled] = new List<SellStatus>()   // 终态
        };
        
        if (!validTransitions[current].Contains(target))
        {
            throw new InvalidTransitionException(
                $"不允许从状态'{current}'转换到'{target}'");
        }
        
        return target;
    }
}
```

#### Scenario: 业务逻辑验证

* **WHEN** 测试团队执行完整的业务流程测试（销售/采购/维修/财务）

* **THEN** 所有计算结果与原系统误差<0.01元，所有校验规则触发时机一致

***

### Requirement: 使用逻辑和交互模式保留（WPF原生完美支持）

系统 SHALL 保持与原系统一致的用户操作习惯和交互模式：

#### 7.1 键盘快捷键完整保留（WPF InputBindings）

| 快捷键        | 功能       | 适用场景   | WPF实现                                                                        |
| ---------- | -------- | ------ | ---------------------------------------------------------------------------- |
| **F5**     | 保存当前单据   | 所有编辑窗口 | `<KeyBinding Key="F5" Command="{Binding SaveCommand}"/>`                     |
| **F3**     | 新增记录     | 列表/明细  | `<KeyBinding Key="F3" Command="{Binding AddCommand}"/>`                      |
| **Insert** | 新增一行     | 明细表格   | `<KeyBinding Key="Insert" Command="{Binding AddRowCommand}"/>`               |
| **Delete** | 删除当前行/记录 | 列表/明细  | `<KeyBinding Key="Delete" Command="{Binding DeleteCommand}"/>`               |
| **Enter**  | 确认/下一字段  | 表单/对话框 | `IsDefault="True"` on Button                                                 |
| **Escape** | 取消/关闭窗口  | 所有窗口   | `IsCancel="True"` on Button                                                  |
| **F1**     | 帮助       | 全局     | `<KeyBinding Key="F1" Command="{Binding HelpCommand}"/>`                     |
| **F9**     | 刷新数据     | 列表页面   | `<KeyBinding Key="F9" Command="{Binding RefreshCommand}"/>`                  |
| **Ctrl+F** | 查找       | 列表/表格  | `<KeyBinding Key="F" Modifiers="Control" Command="{Binding FindCommand}"/>`  |
| **Ctrl+N** | 新建       | 主菜单    | `<KeyBinding Key="N" Modifiers="Control" Command="{Binding NewCommand}"/>`   |
| **Ctrl+P** | 打印       | 单据页面   | `<KeyBinding Key="P" Modifiers="Control" Command="{Binding PrintCommand}"/>` |
| **Alt+字母** | 菜单快捷键    | 菜单栏    | XAML `_F` 下划线语法自动支持                                                          |

#### 7.2 右键菜单保留（25+个ContextMenu）

WPF原生支持ContextMenu，示例：

```xml
<!-- 销售列表右键菜单 -->
<dxg:GridControl.ContextMenu>
    <ContextMenu>
        <MenuItem Header="新增(F3)" Command="{Binding AddCommand}" InputGestureText="F3"/>
        <MenuItem Header="编辑" Command="{Binding EditCommand}"/>
        <MenuItem Header="删除(Del)" Command="{Binding DeleteCommand}" InputGestureText="Del"/>
        <Separator/>
        <MenuItem Header="审核" Command="{Binding ApproveCommand}"/>
        <MenuItem Header="作废" Command="{Binding VoidCommand}"/>
        <Separator/>
        <MenuItem Header="打印(Ctrl+P)" Command="{Binding PrintCommand}" InputGestureText="Ctrl+P"/>
        <MenuItem Header="导出Excel" Command="{Binding ExportExcelCommand}"/>
        <Separator/>
        <MenuItem Header="查看详情" Command="{Binding ViewDetailCommand}" InputGestureText="Enter"/>
        <MenuItem Header="复制" Command="{Binding CopyCommand}" InputGestureText="Ctrl+C"/>
    </ContextMenu>
</dxg:GridControl.ContextMenu>
```

* **销售列表右键**：新增/编辑/删除/审核/作废/打印/导出/查看详情

* **配件列表右键**：查看详情/修改价格/调整库存/查看图片/复制

* **客户列表右键**：查看档案/查看欠款/查看历史/发短信/打电话

* **数据网格右键**：复制/粘贴/插入行/删除行/清除内容

* **工具栏右键**：自定义工具栏/显示文字/大图标/锁定位置

#### 7.3 操作流程保持一致（与PB完全相同）

```
【销售开单标准流程】（PB原系统 → WPF新系统 - 100%一致）
1. 点击"销售"菜单 → "销售开单"  ← 菜单项点击事件
2. 弹出SellOrderWindow窗口（模态/非模态）← Window.Show()/ShowDialog()
3. F3或点击"添加" → 光标跳到客户ComboBox ← FocusManager.SetFocus()
4. 输入客户编号/名称/拼音 → 回车 → 自动填充客户信息 ← ComboBox SelectionChanged事件
5. Tab键跳转到明细DataGrid ← KeyboardNavigation.TabIndex
6. Insert键或点击底部"添加" → DataGrid新增空行 ← GridView.NewItemRowPosition=Bottom
7. 光标定位到配件编码列 → 输入配件编码/名称/拼音 ← Cell编辑
8. 回车 → 弹出PartSelectorWindow配件选择器 ← ButtonEdit.Search按钮触发
9. 双击选择配件 → 自动填入名称/规格/单位/价格 ← 返回值填充
10. Tab跳到数量列 → 输入数量 → 回车 → 自动计算行小计 ← PropertyChanged触发计算
11. 重复步骤6-10直到完成所有明细 ← 循环操作
12. F5点击保存 → ViewModel.SaveCommand执行 → 业务校验 → 生成单据号 → 保存到DB
13. 显示"保存成功!"消息框 ← MessageBox.Show("保存成功!", MessageBoxButton.OK)
14. 点击"打印" → 弹出PrintPreviewWindow → 确认打印 → 调用打印机 ← DocumentViewer.Print()
15. 点击"退出"或按Escape → 关闭窗口 ← Window.Close()
```

#### 7.4 消息提示风格统一（WPF与PB几乎一致）

| 场景       | PB原系统                         | WPF新系统                                                                             | 一致度          |
| -------- | ----------------------------- | ---------------------------------------------------------------------------------- | ------------ |
| **成功保存** | `MessageBox("保存成功!")`         | `MessageBox.Show("保存成功!", "提示", MessageBoxButton.OK, MessageBoxImage.Information)` | **100%**     |
| **错误提示** | `MessageBox("错误: xxx")`       | `MessageBox.Show("错误: xxx", "错误", MessageBoxButton.OK, MessageBoxImage.Error)`     | **100%**     |
| **警告确认** | `MessageBox("是否继续?", YesNo!)` | `MessageBox.Show("是否继续?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question)` | **100%**     |
| **信息提示** | `MessageBox("提示: xxx", OK!)`  | `MessageBox.Show("提示: xxx", "信息", MessageBoxButton.OK, MessageBoxImage.Asterisk)`  | **100%**     |
| **加载中**  | 无（同步阻塞UI）                     | `new WaitIndicator().Show()` 或 `Mouse.OverrideCursor = Cursors.Wait`               | **改进**（不再阻塞） |
| **通知提醒** | 无（PB无托盘气泡）                    | `Notification balloon = new Notification(); balloon.Show();`                       | **新增**       |
| **输入确认** | `MessageBox("确定删除?", YesNo!)` | 同警告确认                                                                              | **100%**     |

#### 7.5 焦点管理（WPF强项）

```csharp
// PB的焦点跳转逻辑 → WPF的FocusManager
// 例：销售开单保存后，焦点回到客户选择框
private void OnSaved(object sender, EventArgs e)
{
    MessageBox.Show("保存成功!");
    
    // 焦点回到客户ComboBox（与PB行为一致）
    Dispatcher.BeginInvoke(new Action(() =>
    {
        Keyboard.Focus(cboClient);  // 设置键盘焦点
        cboClient.SelectAll();      // 选中文本便于重新输入
    }), System.Windows.Threading.DispatcherPriority.Input);
}

// Tab顺序设置（与PB的Tab Order完全对应）
<Grid KeyboardNavigation.TabNavigation="Cycle">  <!-- 循环Tab -->
    <ComboBox x:Name="cboClient" KeyboardIndex="1"/>  <!-- 第一个Tab停留点 -->
    <DateEdit x:Name="dtDate" KeyboardIndex="2"/>      <!-- 第二个 -->
    <GridControl x:Name="dgDetails" KeyboardIndex="3"/> <!-- 第三个（进入Grid内部）-->
</Grid>
```

#### Scenario: 使用习惯验证

* **WHEN** 原系统熟练用户首次使用WPF新系统

* **THEN** 无需培训即可上手操作，**98%以上操作路径完全一致**（仅UI外观略有现代化提升）

***

### Requirement: 数据库零改动兼容方案（ODBC/ADO.NET直连，兼容原PB的ODBC连接方式）

系统 SHALL 直接连接原SQL Server数据库qipei，不做任何DDL/DML改动：

#### 8.1 数据库连接配置（appsettings.json）

```json
{
  "ConnectionStrings": {
    "QipeiDb_ODBC": "DSN=QP11_SQLServer;Uid=sa;Pwd=${DB_PASSWORD};",  // ODBC方式（推荐，兼容原PB）
    "QipeiDb_SqlClient": "Server=localhost;Database=qipei;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=True;Max Pool Size=100;Connection Timeout=30;",  // ADO.NET SqlClient方式（备选）
    "Provider": "Odbc"  // 使用 "Odbc" 或 "SqlClient" 切换连接方式
  },
  "DatabaseSettings": {
    "CommandTimeout": 120,
    "EnableSqlLogging": true,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  }
}
```

**📝 ODBC vs SqlClient 对比**：

| 特性        | ODBC (System.Data.Odbc) | SqlClient (Microsoft.Data.SqlClient) |
| --------- | ----------------------- | ------------------------------------ |
| **兼容性**   | ✅ 与原PB完全一致（使用相同DSN）     | ⚠️ 需要修改连接字符串                         |
| **配置方式**  | Windows ODBC数据源管理器配置DSN | 直接在config中写连接参数                      |
| **部署便利性** | ⚠️ 需要在目标机器配置ODBC DSN    | ✅ 无需额外配置                             |
| **性能**    | 略低（多一层ODBC驱动抽象）         | ✅ 更优（原生SQL Server协议）                 |
| **推荐场景**  | 原系统迁移期、需要与PB并行运行时       | 新部署环境、性能敏感场景                         |

**🔧 ODBC DSN配置步骤（Windows）**：

1. 控制面板 → 管理工具 → ODBC数据源(64位)
2. 添加 → SQL Server → 输入数据源名`QP11_SQLServer`
3. 选择SQL Server服务器地址
4. 选择数据库`qipei`
5. 测试连接成功后保存

#### 8.1.1 DatabaseFactory代码示例（ODBC/SqlClient双模式支持）

```csharp
using System.Data;
using System.Data.Odbc;      // ODBC支持（兼容原PB）
using Microsoft.Data.SqlClient; // SqlClient支持（备选）
using Microsoft.Extensions.Configuration;

public static class DatabaseFactory
{
    private static readonly string _connectionString;
    private static readonly string _provider;

    static DatabaseFactory()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        _provider = config["ConnectionStrings:Provider"] ?? "Odbc";
        
        // 根据Provider选择连接字符串
        if (_provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
        {
            _connectionString = config["ConnectionStrings:QipeiDb_ODBC"];
        }
        else
        {
            _connectionString = config["ConnectionStrings:QipeiDb_SqlClient"];
        }
    }

    /// <summary>
    /// 创建数据库连接（ODBC或SqlClient，由配置决定）
    /// </summary>
    public static IDbConnection Create()
    {
        IDbConnection connection;
        
        if (_provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
        {
            // ODBC方式：与原PB项目一致
            connection = new OdbcConnection(_connectionString);
        }
        else
        {
            // SqlClient方式：纯ADO.NET，性能更优
            connection = new SqlConnection(_connectionString);
        }
        
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 获取当前使用的数据库提供程序类型
    /// </summary>
    public static string Provider => _provider;
}
```

**📝 使用示例**：
```csharp
// Repository中使用（Dapper自动适配IDbConnection）
public class PartRepository : BaseRepository<PartData>
{
    public async Task<List<PartData>> GetAllAsync()
    {
        using var db = DatabaseFactory.Create();  // 自动使用ODBC或SqlClient
        return (await db.QueryAsync<PartData>("SELECT * FROM part_data")).ToList();
    }
}
```

#### 8.2 MyBatis/Dapper实体类映射规则（95张表→95个Entity类）

```csharp
// 命名规则: 表名(下划线) → 类名(驼峰)，字段同理
// 示例1: part_data表 → PartData.cs
[Table("part_data")]
public class PartData
{
    [Key]
    [Column("partid")]
    public long Partid { get; set; }          // 配件ID（主键，自增）
    
    [Column("partno")]
    public string Partno { get; set; }         // 配件件号
    
    [Column("name")]
    public string Name { get; set; }            // 配件名称
    
    [Column("carname")]
    public string Carname { get; set; }         // 适用车名
    
    [Column("cartype")]
    public string Cartype { get; set; }         // 适用车型
    
    [Column("unit")]
    public string Unit { get; set; }            // 计量单位
    
    [Column("className")]  // className是C#关键字，用Column注解映射
    public string ClassName { get; set; }       // 配件分类
    
    [Column("inprice")]
    public decimal? Inprice { get; set; }       // 进货价（可空）
    
    [Column("lsprice")]
    public decimal? Lsprice { get; set; }       // 零售价
    
    [Column("pfprice")]
    public decimal? Pfprice { get; set; }       // 批发价
    
    [Column("namePy")]
    public string NamePy { get; set; }          // 名称拼音（用于快速搜索）
    
    [Column("memo")]
    public string Memo { get; set; }            // 备注
    
    [Column("del")]
    public string Del { get; set; }             // 删除标志（空=正常, "Y"=已删）
    
    // 扩展属性（非数据库字段，用于UI绑定）
    [NotMapped]
    public string DisplayName => $"{Partno} - {Name}";
    
    [NotMapped]
    public bool IsDeleted => Del == "Y";
}

// 示例2: bill_sell表 → BillSell.cs
[Table("bill_sell")]
public class BillSell
{
    [Key]
    [Column("sn")]
    public string Sn { get; set; }              // 销售单号（业务主键，非自增）
    
    [Column("client")]
    public string Client { get; set; }          // 客户编号（外键关联client_infor.cid）
    
    [Column("worker")]
    public string Worker { get; set; }          // 业务员（外键关联user_infor.uid）
    
    [Column("total")]
    public decimal? Total { get; set; }         // 原价总额
    
    [Column("bill_total")]
    public decimal? BillTotal { get; set; }     // 折后总额
    
    [Column("discount_rate")]
    public decimal? DiscountRate { get; set; }  // 折扣率
    
    [Column("cash")]
    public decimal? Cash { get; set; }          // 现金收款
    
    [Column("weixin")]
    public decimal? Weixin { get; set; }        // 微信支付
    
    [Column("zhifubao")]
    public decimal? Zhifubao { get; set; }      // 支付宝支付
    
    [Column("flag")]
    public int? Flag { get; set; }              // 状态标志（0草稿/1已审/2完成/3作废/4取消）
    
    [Column("datetime")]
    public DateTime? Datetime { get; set; }     // 开单时间
    
    // 导航属性（可选，用于LINQ Join查询）
    [ForeignKey("Client")]
    public virtual ClientInfor ClientInfo { get; set; }
    
    [ForeignKey("Worker")]
    public virtual UserInfor WorkerInfo { get; set; }
    
    // 导航集合
    public virtual ICollection<SellDetail> Details { get; set; }
}
```

#### 8.3 只读兼容层设计（保护原数据）

```sql
-- 创建兼容视图（如需字段重命名或联合查询）
-- 注意：WPF应用直接操作原表，视图仅用于复杂报表查询
CREATE VIEW v_sell_detail_with_client AS
SELECT 
    ds.id,
    ds.sn,
    ds.partid,
    pd.name AS part_name,
    pd.partno,
    ds.amount,
    ds.price,
    ds.stotal,
    bs.client,
    ci.name AS client_name,
    ci.phone,
    bs.datetime AS sell_date,
    bs.flag AS sell_flag
FROM detail_sell ds
LEFT JOIN part_data pd ON ds.partid = pd.partid
LEFT JOIN bill_sell bs ON ds.sn = bs.sn
LEFT JOIN client_infor ci ON bs.client = ci.ci
WHERE (ds.del IS NULL OR ds.del <> 'Y')
  AND (bs.del IS NULL OR bs.del <> 'Y');
```

#### 8.4 数据安全措施（WPF桌面端特有优势）

1. **连接加密**：`Encrypt=True;TrustServerCertificate=False;` SSL/TLS加密
2. **权限隔离**：应用账号只有`SELECT/INSERT/UPDATE/DELETE`权限，无`DDL/DROP`权限
3. **操作审计**：所有写操作通过拦截器记录到sys\_log表（包含用户IP、机器名、操作时间）
4. **备份策略**：保留原PB的备份机制（w\_database\_backup），新增应用层定时备份
5. **并发控制**：乐观锁（version字段或时间戳）防止并发冲突；悲观锁（WITH(UPDLOCK)）用于库存扣减
6. **本地安全**：WPF是桌面应用，无需暴露HTTP端口，减少攻击面
7. **配置加密**：连接字符串密码使用DPAPI加密存储在本地（比Web的明文config更安全）

#### Scenario: 数据库兼容性验证

* **WHEN** 新WPF系统连接原qipei数据库执行完整业务流程（销售开单→审核→打印→查询）

* **THEN** 所有CRUD操作正常，数据与原系统完全一致，无数据损坏，性能优于原PB系统

***

### Requirement: 分阶段实施路线图（12个月计划，WPF版优化）

系统 SHALL 采用分阶段渐进式重构策略，降低风险：

#### Phase 1: 基础设施搭建（第1-2个月）

* [ ] 搭建WPF (.NET 8) 项目脚手架（Solution结构：Core/Data/Services/Views/ViewModels）

* [ ] 配置数据库连接（appsettings.json + Dapper/EF Core）

* [ ] 实现95张表的Entity类生成（可用T4模板或逆向工程工具自动生成）

* [ ] 搭建DI容器和基础服务层（Microsoft.Extensions.DependencyInjection）

* [ ] 实现认证授权模块（登录窗口LoginWindow + JWT Token本地存储）

* [ ] 实现主框架布局（MainWindow + DevExpress DXDocking MDI框架）

* [ ] 开发通用组件（DataWindowGrid/DataWindowForm/PartSelector等UserControl）

* [ ] 实现全局快捷键系统和右键菜单框架

* [ ] 配置DevExpress主题（还原Windows XP经典风格或现代化主题）

#### Phase 2: 核心业务模块（第3-5个月）

* [ ] 实现配件管理模块（11张part\_\*表的CRUD + 虚拟滚动列表 + 图片管理）

* [ ] 实现客户/供应商管理（client\_infor/supplier\_infor + 欠款管理）

* [ ] 实现销售开单全流程（bill\_sell + detail\_sell + 计算 + 校验 + 打印）

* [ ] 实现采购管理全流程（bill\_buy + detail\_buy + 入库确认）

* [ ] 实现库存管理（part\_stock实时更新 + 预警 + 盘点）

* [ ] 实现财务管理基础（account/pays/arrearage + 收支明细）

#### Phase 3: 特色业务模块（第6-8个月）【已移除维修厂管理】

* [ ] 实现会员管理系统（xl\_hygl/xl\_klb + 刷卡/充值/折扣）

* [ ] 实现借还管理（borrow/lend + 借还编辑/结算）

* [ ] 实现连锁版数据同步（down\* 12张表 + FTP传输）

#### Phase 4: 系统管理与报表（第9-10个月）

* [ ] 实现用户权限系统（user\_infor/groups/rules/mnu + 菜单权限控制）

* [ ] 实现系统设置（配置/备份恢复/升级/注册）

* [ ] 实现报表中心（30+报表模板 + DevExpress Reports/FastReport）

* [ ] 实现打印预览系统（FlowDocument + DocumentViewer）

* [ ] 实现Excel导出功能（NPOI/EPPlus + 进度条）

#### Phase 5: 测试与上线（第11-12个月）

* [ ] 全面功能测试（200+功能点回归测试，对比原PB系统）

* [ ] 性能测试（并发用户/大数据量/长时间运行稳定性）

* [ ] 安全测试（SQL注入/XSS/权限越权/数据泄露扫描）

* [ ] UAT用户验收测试（邀请实际业务用户试用1-2周）

* [ ] 数据并行运行验证（新旧系统同时运行，对比数据一致性）

* [ ] 生产环境部署（MSIX打包/ClickOnce发布/安装包制作）

* [ ] 用户培训（操作手册/视频教程/现场培训）

* [ ] 正式上线切换（停用旧PB系统，启用WPF新系统）

## MODIFIED Requirements

无（本次为新规划任务，基于之前Svelte方案的全面升级）

## REMOVED Requirements

无
