# DI + MVVM + 仓储模式架构重构 Spec

## Why

当前 QP11 项目虽然已定义了 `IRepository<T>`、`ICalcService`、`IAuthService` 等接口，DI 容器也注册了服务，但实际代码中所有 Repository/Service 均通过 `new` 直接实例化，接口从未被消费。BaseViewModel 存在但未被任何 View 使用，所有业务逻辑堆积在 Code-Behind 中。需要将这些已有的架构基础设施"接通"，使 DI、MVVM、仓储模式真正生效，同时不改变任何业务逻辑和用户可见行为。

## What Changes

- 在 Core 层为每个具体 Repository 添加接口定义（ISellRepository、IPartRepository 等）
- 在 Core 层为每个 Service 添加接口定义（IValidationService、ISellService 等）
- 重构 Service 层：将 `new Repository()` 改为构造函数注入
- 重构 DI 注册：从注册具体类改为注册接口→实现类映射
- 重构 WPF 关键 View：将 SellControl 的业务逻辑提取到 SellViewModel
- 修复 BaseRepository 连接泄漏：统一 try-finally 释放模式
- 补充空 catch 的日志记录

## Impact

- Affected specs: deep-architecture-analysis (W-P1-1 MVVM名存实亡, W-P1-2 DI容器形同虚设)
- Affected code:
  - QP11.Core/Interfaces/ (新增 ~15 个接口文件)
  - QP11.Data/Repositories/ (实现新接口，修复连接管理)
  - QP11.Services/ (改为构造函数注入)
  - QP11.Wpf/App.xaml.cs (DI 注册方式变更)
  - QP11.Wpf/Views/SellControl.xaml.cs (提取 ViewModel)
  - QP11.Wpf/ViewModels/ (新增 SellViewModel)

## ADDED Requirements

### Requirement: 具体仓储接口定义

系统 SHALL 在 Core 层为每个具体 Repository 定义独立接口，继承自 `IRepository<T>`：

#### Scenario: 接口定义完成

- **WHEN** 开发者查看 QP11.Core/Interfaces 目录
- **THEN** 存在以下接口文件，每个接口包含该 Repository 特有的查询方法签名：

| 接口名 | 继承自 | 特有方法 |
|--------|--------|----------|
| ISellRepository | IRepository<BillSell> | GetBySnAsync, GetDetailsAsync, InsertBillAsync, InsertDetailsAsync, DeleteDetailsAsync, UpdateBillStatusAsync, UpdateMemoAsync, GetListAsync |
| IPartRepository | IRepository<PartData> | GetStockListAsync, GetStockListAdvancedAsync, GetStockByIdAsync, DecreaseStockAsync, IncreaseStockAsync, SearchAsync |
| IClientRepository | IRepository<ClientInfor> | (基础方法足够) |
| ISupplierRepository | IRepository<SupplierInfor> | (基础方法足够) |
| IAccountRepository | IRepository<Account> | (基础方法足够) |
| IPaysRepository | IRepository<Pays> | GetByAccountAsync |
| IArrearageRepository | IRepository<Arrearage> | GetClientArrearTotalAsync |
| IMemberCardRepository | IRepository<MemberCard> | ConsumeAsync |
| IBorrowRepository | IRepository<Borrow> | (基础方法足够) |
| IUserRepository | IRepository<UserInfor> | (基础方法足够) |
| ISysLogRepository | IRepository<SysLog> | GetListAsync |

### Requirement: 服务层接口定义与依赖注入重构

系统 SHALL 为每个 Service 定义接口，并改为构造函数注入：

#### Scenario: Service 使用构造函数注入

- **WHEN** 查看 SellService 的构造函数
- **THEN** 所有依赖通过构造函数参数注入，不再使用 `new` 实例化：

```csharp
public class SellService : ISellService
{
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IMemberCardRepository _memberRepo;
    private readonly IValidationService _validator;
    private readonly ISerialNumberService _snService;

    public SellService(
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IArrearageRepository arrearRepo,
        IMemberCardRepository memberRepo,
        IValidationService validator,
        ISerialNumberService snService)
    { ... }
}
```

#### Scenario: ValidationService 使用构造函数注入

- **WHEN** 查看 ValidationService 的构造函数
- **THEN** 依赖通过构造函数注入，不再 `new PartRepository()` 等

### Requirement: DI 容器注册方式变更

系统 SHALL 将 App.xaml.cs 中的 DI 注册从具体类注册改为接口→实现类映射：

#### Scenario: DI 注册使用接口映射

- **WHEN** 查看 App.xaml.cs 的 ServiceCollection 配置
- **THEN** 注册方式为：

```csharp
// 仓储
services.AddTransient<ISellRepository, SellRepository>();
services.AddTransient<IPartRepository, PartRepository>();
// ... 其他仓储

// 服务
services.AddTransient<IValidationService, ValidationService>();
services.AddTransient<ICalcService, CalcService>();
services.AddTransient<IAuthService, AuthService>();
services.AddTransient<ISerialNumberService, SerialNumberService>();
services.AddTransient<ISellService, SellService>();
services.AddTransient<IBuyService, BuyService>();
services.AddTransient<IFinanceService, FinanceService>();
```

### Requirement: SellControl 业务逻辑提取到 SellViewModel

系统 SHALL 将 SellControl.xaml.cs 中的核心业务逻辑提取到 SellViewModel，View 只负责 UI 交互：

#### Scenario: SellViewModel 承担业务逻辑

- **WHEN** 查看 SellViewModel.cs
- **THEN** 包含以下命令和属性：
  - SaveBillCommand：保存销售单逻辑（从 SellControl.SaveBill 提取）
  - VoidBillCommand：作废销售单逻辑（从 SellControl.VoidSelectedBill 提取）
  - LoadBillForEditCommand：加载编辑逻辑
  - SearchBillsCommand：查询逻辑
  - Details：ObservableCollection<SellControlItem>
  - Total/BillTotal/Arrear 等计算属性

#### Scenario: SellControl 只做 UI 交互

- **WHEN** 查看 SellControl.xaml.cs
- **THEN** 代码只包含：
  - InitializeComponent 和 UI 初始化
  - 事件处理器调用 ViewModel 的 Command
  - UI 特有逻辑（焦点管理、弹窗位置等）
  - 不再直接 new Repository 或调用 DatabaseFactory

### Requirement: BaseRepository 连接管理修复

系统 SHALL 统一 BaseRepository 及其子类的连接释放模式，消除泄漏风险：

#### Scenario: 连接始终正确释放

- **WHEN** Repository 方法执行过程中抛出异常
- **THEN** 自行创建的数据库连接在 finally 块中被释放，不依赖 `if (transaction == null) db.Dispose()` 这种异常时不执行的写法

### Requirement: 空 catch 补充日志

系统 SHALL 将所有 `catch { }` 和 `catch (Exception ex) { MessageBox.Show(...) }` 补充 Serilog 日志记录：

#### Scenario: 异常不再被静默吞掉

- **WHEN** SellControl.LoadDropdowns 中发生异常
- **THEN** 异常被记录到 Serilog 日志，而不是被空 catch 吞掉

## MODIFIED Requirements

无

## REMOVED Requirements

无
