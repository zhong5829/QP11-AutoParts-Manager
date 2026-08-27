# QP11 新架构深度性能/并发/潜在问题分析 Spec

## Why

QP11 系统已完成从 PowerBuilder 11 到 WPF/.NET 8 的架构迁移，代码覆盖了 Core（实体/接口）、Data（仓储/基础设施）、Services（业务逻辑）、Wpf（UI）四层。但在快速迁移过程中，积累了大量性能瓶颈、并发安全隐患和潜在 Bug，需要在生产上线前系统性地识别和修复，避免数据不一致、界面卡顿、安全漏洞等生产事故。

## What Changes

- 识别并记录四层架构中所有性能瓶颈、并发隐患和潜在 Bug
- 按严重程度分级（P0 致命 / P1 严重 / P2 中等 / P3 轻微）
- 为每个问题提供修复方案和验证标准
- **不涉及代码修改**，仅产出分析报告和修复任务清单

## Impact

- 受影响代码：QP11.Core（30个实体+3个接口）、QP11.Data（3个基础设施+27个仓储）、QP11.Services（9个服务）、QP11.Wpf（60+个视图/控件）
- 受影响能力：数据一致性、并发安全、UI响应速度、系统安全性

---

## ADDED Requirements

### Requirement: Core 层（实体/接口）性能与并发分析

系统 SHALL 对 Core 层的 30 个实体类和 3 个接口定义进行深度分析，识别以下问题：

#### Scenario: 实体层致命问题识别

- **WHEN** 审查所有实体类的字段定义、类型映射和约束注解
- **THEN** 识别出以下致命问题（P0）：

| # | 问题 | 位置 | 风险 |
|---|------|------|------|
| C-P0-1 | 库存扣减无并发控制 | `PartStock.Amount`, `PartBatch.Remain` 无 RowVersion/Timestamp | 多用户同时销售导致超卖 |
| C-P0-2 | 序列号竞态条件 | `CodeRule.CurrentSeq` 无原子递增机制 | 并发生成重复编号 |
| C-P0-3 | 图片二进制存储 | `PartData.Picture`, `CarMark.Picture` 为 byte[] | 列表查询加载全量图片，内存爆炸 |
| C-P0-4 | string? 可空主键 | `BillSell.Sn`, `ClientInfor.Cid` 等 6 个实体 | 主键为 NULL 导致数据完整性破坏 |

#### Scenario: 实体层严重问题识别（P1）

- **WHEN** 审查实体类的索引、类型一致性和设计模式
- **THEN** 识别出以下严重问题：

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| C-P1-1 | 全部查询字段缺少 [Index] 标注 | 所有 30 个实体 | 高频查询全表扫描 |
| C-P1-2 | 金额/数量类型不一致 | `DetailSell.Amount`(long?) vs `PartBatch.Amount`(decimal?) vs `PartStock.Amount`(long?) | 精度丢失或隐式转换错误 |
| C-P1-3 | 软删除标志不统一 | DEL='1' / del='Y' / flag=-1 / zt='停用' 四种方式 | 查询遗漏或误判 |
| C-P1-4 | 全部实体缺少导航属性 | 所有外键字段 | 无法使用关联加载，N+1 查询 |
| C-P1-5 | 属性名与列名语义不匹配 | `Arrearage.Cid`→bid, `Pays.Je`→pay, `Account.Je`→charge | 开发理解错误导致 Bug |
| C-P1-6 | `IRepository<T>.FindAsync` 返回 IEnumerable 而非 IQueryable | IRepository.cs | 过滤在内存执行而非数据库端 |
| C-P1-7 | `IRepository<T>.GetAllAsync` 无分页限制 | IRepository.cs | 大数据量表全量加载 OOM |

#### Scenario: 实体层代码异味识别（P2-P3）

- **WHEN** 审查代码规范和设计模式
- **THEN** 识别出：明细实体高度重复（DetailBuy/DetailSell/DetailBaosun/DetailJhdh）、ClientInfor 与 SupplierInfor 高度重复、一个文件多个类型、拼音缩写字段可读性差、ISerialNumberService 12 个方法接口膨胀、PagedResult.TotalPages 除零风险

---

### Requirement: Data 层（仓储/基础设施）性能与并发分析

系统 SHALL 对 Data 层的 3 个基础设施组件和 27 个仓储类进行深度分析：

#### Scenario: 基础设施层致命问题识别

- **WHEN** 审查 DatabaseFactory、OdbcCompatConnection、ColumnAttributeTypeMapper
- **THEN** 识别出以下问题：

| # | 问题 | 位置 | 风险 |
|---|------|------|------|
| D-P0-1 | OdbcCompatCommand 重复参数名处理错误 | OdbcCompatConnection.cs:194-217 | 同一参数出现多次时第二个 ? 传入 DBNull，查询逻辑错误 |
| D-P0-2 | OdbcCompatCommand @@系统变量误匹配 | OdbcCompatConnection.cs:166 | @@ROWCOUNT 等被错误替换为 ? |
| D-P0-3 | PartRepository MAX+1 自增ID | PartRepository.cs:70,232 | 并发插入主键冲突或数据覆盖 |
| D-P0-4 | 全部单据类 Repository 无事务支持 | Sell/Buy/Baosun/Jhdh/Quotation Repository | 单据+明细部分写入导致数据不一致 |

#### Scenario: 仓储层严重问题识别（P1）

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| D-P1-1 | 库存扣减无负数检查 | PartRepository.DecreaseStockAsync | 库存为负数 |
| D-P1-2 | 余额/欠款更新非原子 | AccountRepository, ArrearageRepository, MemberCardRepository | 并发更新丢失 |
| D-P1-3 | CodeRule 序号并发冲突 | CodeRuleRepository.UpdateAsync | 重复编号 |
| D-P1-4 | PaysRepository.GetByAccountAsync 参数未使用 | PaysRepository.cs:16-24 | accountId 传入但 SQL 中未使用，返回全部记录 |
| D-P1-5 | CarMarkRepository.LogicDeleteAsync 实为物理删除 | CarMarkRepository.cs:56-60 | 方法名与行为不一致 |
| D-P1-6 | BaseRepository.IsIdentityKey 永远返回 true | BaseRepository.cs:121-123 | 非自增主键实体无法插入主键值 |
| D-P1-7 | SellRepository.GetListAsync 未过滤已删除 | SellRepository.cs:35 | flag=-1 的记录仍出现在列表 |
| D-P1-8 | SysLogRepository.GetListAsync 无分页 | SysLogRepository.cs:16-26 | 日志表大数据量 OOM |
| D-P1-9 | OdbcCompatCommand 每次正则解析 SQL | OdbcCompatConnection.cs:166 | 高频查询性能损耗 |
| D-P1-10 | BaseRepository ROW_NUMBER 分页性能差 | BaseRepository.cs:50-56 | 大表排序编号开销大 |

---

### Requirement: Services 层（业务逻辑）性能与并发分析

系统 SHALL 对 Services 层的 9 个服务类进行深度分析：

#### Scenario: 服务层致命问题识别

| # | 问题 | 位置 | 风险 |
|---|------|------|------|
| S-P0-1 | SellService.CreateSellOrderAsync 无事务 | SellService.cs | 单据+明细+库存+欠款+会员卡部分写入 |
| S-P0-2 | BuyService.CreateBuyOrderAsync 无事务 | BuyService.cs | 单据+明细+欠款部分写入 |
| S-P0-3 | BuyService.ConfirmStockInAsync 无事务 | BuyService.cs | 状态已更新但库存未增加 |
| S-P0-4 | SerialNumberService.GenerateSN 竞态条件 | SerialNumberService.cs | Read-Modify-Write 无锁，并发生成重复编号 |
| S-P0-5 | FinanceService 收款/付款无事务 | FinanceService.cs | 余额已更新但记录未插入，账目不平 |
| S-P0-6 | PermissionService 权限加载失败默认放行 | PermissionService.cs:HasPermission | !_permissionsLoaded 时 return true |

#### Scenario: 服务层严重问题识别（P1）

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| S-P1-1 | ValidationService.ValidateStockAsync 只验证存在性不验证库存量 | ValidationService.cs | 库存不足仍可销售 |
| S-P1-2 | SellService 作废不处理欠款和会员卡退款 | SellService.VoidSellOrderAsync | 财务数据不一致 |
| S-P1-3 | BuyService.ConfirmStockInAsync 缺少状态校验 | BuyService.cs | 重复入库 |
| S-P1-4 | SellService.CreateSellOrderAsync 逐条插入+逐条扣减 | SellService.cs | N 条明细需 2N+3 次数据库连接 |
| S-P1-5 | AuthService 使用 MD5 哈希密码 | AuthService.cs | MD5 易受碰撞攻击和彩虹表攻击 |
| S-P1-6 | PermissionService 线程不安全 | PermissionService.cs | 多线程读写 UserGroups/Permissions 竞态 |
| S-P1-7 | SerialNumberService 单次生成 5-6 次数据库查询 | SerialNumberService.cs | 性能低下 |
| S-P1-8 | ExportService 使用 XSSFWorkbook DOM 模式 | ExportService.cs | 大数据量导出内存溢出 |
| S-P1-9 | CalcService 折扣率语义混淆 | CalcService.cs | discountRate 含义不一致易出错 |
| S-P1-10 | FinanceService.Pays 实体属性映射不匹配 | FinanceService.cs | AccountId/Type/Memo 字段未写入数据库 |

---

### Requirement: WPF 层（UI）性能与并发分析

系统 SHALL 对 WPF 层的 App、ViewModel、Helpers、Services 和 60+ 个 View 进行深度分析：

#### Scenario: WPF 层致命问题识别

| # | 问题 | 位置 | 风险 |
|---|------|------|------|
| W-P0-1 | SellControl.SaveBill 无事务 | SellControl.xaml.cs | 单据+库存部分写入 |
| W-P0-2 | BuyControl.SettleBill 无事务 | BuyControl.xaml.cs | 状态+库存+新配件部分写入 |
| W-P0-3 | SellControl.LoadPartList 无条件加载全量库存 | SellControl.xaml.cs | 数万条记录直接绑定 DataGrid，UI 卡死 |

#### Scenario: WPF 层严重问题识别（P1）

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| W-P1-1 | MVVM 模式名存实亡 | BaseViewModel 未被使用 | 无法单元测试，View 与逻辑高度耦合 |
| W-P1-2 | DI 容器形同虚设 | App.xaml.cs 注册但未消费 | 所有 Repository/Service 直接 new |
| W-P1-3 | LoginWindow 使用 .Result 同步等待 | LoginWindow.xaml.cs:71 | 潜在死锁 |
| W-P1-4 | SellControl._searchTimer 事件处理器泄漏 | SellControl.xaml.cs:285-291 | 每次 TextChanged 创建新 Timer，Tick 委托累积 |
| W-P1-5 | PartSelectorWindow.LoadPartsAsync 无防抖 | PartSelectorWindow.xaml.cs | 连续输入频繁查询 |
| W-P1-6 | BuyControl.CreateNewPartsAsync 逐条插入 | BuyControl.xaml.cs | 每个新配件两次 INSERT，无事务 |
| W-P1-7 | BuyControl 退货模式库存方向可能错误 | BuyControl.xaml.cs:788 | 采购退货应增加库存但代码减少库存 |
| W-P1-8 | SellOrderWindow 会员卡余额不足未阻止保存 | SellOrderWindow.xaml.cs:259 | 只警告但继续执行，余额可能为负 |
| W-P1-9 | CommandManager.RequerySuggested 全局刷新 | BaseViewModel.cs | 命令数量多时 UI 卡顿 |
| W-P1-10 | ExcelParserService 同步解析阻塞 UI | ExcelParserService.cs | 大文件解析界面卡死 |

---

### Requirement: 跨层系统性问题汇总

系统 SHALL 识别跨层的系统性架构缺陷：

#### Scenario: 系统性致命问题

- **WHEN** 综合分析四层代码的交互模式
- **THEN** 识别出以下跨层致命问题：

| # | 问题 | 涉及层 | 风险 |
|---|------|--------|------|
| X-P0-1 | **全局无事务支持** | Data+Services+Wpf | 所有涉及多表写入的操作均无事务保护，数据一致性无保障 |
| X-P0-2 | **流水号竞态条件** | Core+Data+Services | SerialNumberService + CodeRule + PartRepository 三处均有 Read-Modify-Write 竞态 |
| X-P0-3 | **库存超卖风险** | Core+Data+Services+Wpf | 先验证后扣减的时间窗口 + 无行锁 + 无乐观并发控制 |
| X-P0-4 | **安全漏洞** | Services+Wpf | 权限失败放行 |

#### Scenario: 系统性性能瓶颈

| # | 瓶颈 | 涉及层 | 影响 |
|---|------|--------|------|
| X-Perf-1 | 每次操作新建/释放数据库连接 | Data | 缺少连接池优化，ODBC 模式下尤其严重 |
| X-Perf-2 | SELECT * 查询全量字段 | Data | 图片 byte[] 字段被无意义加载 |
| X-Perf-3 | 无分页的列表查询 | Data+Services+Wpf | 多个 Repository 的 GetAllAsync/GetListAsync 返回全量 |
| X-Perf-4 | 逐条数据库操作（N+1 问题） | Services+Wpf | 明细插入、库存扣减、新配件创建均为逐条 await |
| X-Perf-5 | ODBC 参数转换正则每次执行 | Data | 每次查询都做正则解析和 SQL 重建 |
| X-Perf-6 | UI 线程同步数据库调用 | Wpf | LoginWindow .Result、PartSelectorWindow LoadCategories |

---

## MODIFIED Requirements

无

## REMOVED Requirements

无
