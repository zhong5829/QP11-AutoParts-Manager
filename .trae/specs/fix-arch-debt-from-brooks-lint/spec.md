# QP11 架构审计与技术债务修复 Spec

## Why

Brooks-Lint 三轮审计（Health Dashboard 68分 → Architecture Audit 55分 → Tech Debt 52分）揭示 QP11 项目存在系统性架构腐化和技术债务：静态工厂 `DatabaseFactory.Create()` 耦合 69 个文件穿透所有分层；"创建销售单"业务逻辑在 Service/ViewModel/Controller 三处独立实现导致数据不一致；30+ 核心实体全部为贫血模型；整个项目零测试覆盖。需要按优先级逐步修复，在不影响业务逻辑的前提下恢复分层约束、消除知识重复、打开测试接缝。

## What Changes

- 引入 `IDbConnectionFactory` 接口替代静态 `DatabaseFactory`，通过 DI 注入到 Service/Repository/Controller
- WPF 层移除对 QP11.Data 项目的直接引用，ViewModel 中的原始 SQL 查询迁移到 Repository/Service 层
- SellController 和 SellViewModel 委托 SellService.CreateSellOrderAsync 执行核心事务，消除"创建销售单"三处重复实现
- BuyController 和 BuyViewModel 同样委托 BuyService 执行核心事务
- Flag 从 `int?` 改为 `BillFlag` 枚举，Repository SQL 参数化 flag 值
- 核心实体添加行为方法（CalculateTotal, ApplyFlag, ValidateDiscount 等）
- BaseRepository 中移除 NotImplementedException 的 FindAsync 和无调用者的 GetPagedAsync
- 从解决方案中移除空壳 QP11.Models 项目

## Impact

- Affected specs: 销售开单流程、采购开单流程、WebAPI 订单接口
- Affected code:
  - QP11.Data/Infrastructure/DatabaseFactory.cs → 新增 IDbConnectionFactory 接口
  - QP11.Services/SellService.cs, BuyService.cs, FinanceService.cs 等 6 个 Service → 改为构造函数注入 IDbConnectionFactory
  - QP11.WebApi/Controllers/SellController.cs, BuyController.cs 等 4 个 Controller → 委托 Service 执行事务
  - QP11.Wpf/ViewModels/SellViewModel.cs, BuyViewModel.cs → 委托 Service 执行事务
  - QP11.Wpf/ViewModels/*.cs 29 个 ViewModel → 移除 DatabaseFactory.Create() 调用
  - QP11.Core/Entities/BillSell.cs, DetailSell.cs 等 → 添加行为方法、Flag 类型改为枚举
  - QP11.Core/Constants/BusinessConstants.cs → 新增 BillFlag 枚举
  - QP11.Data/Repositories/BaseRepository.cs → 移除 FindAsync/GetPagedAsync
  - QP11.Data/Repositories/ArrearageRepository.cs 等 → SQL 参数化 flag
  - QP11.Wpf/QP11.Wpf.csproj → 移除 QP11.Data 项目引用

## ADDED Requirements

### Requirement: 数据库连接抽象与依赖注入

系统 SHALL 通过 `IDbConnectionFactory` 接口提供数据库连接，取代静态 `DatabaseFactory.Create()` 调用。

#### Scenario: Service 通过 DI 获取数据库连接
- **WHEN** SellService 需要执行数据库操作
- **THEN** 通过构造函数注入的 `IDbConnectionFactory` 创建连接，而非调用 `DatabaseFactory.Create()`

#### Scenario: 单元测试可替换数据库连接
- **WHEN** 对 SellService.CreateSellOrderAsync 编写单元测试
- **THEN** 可注入内存数据库或 Mock 的 IDbConnectionFactory，无需启动真实 SQL Server

### Requirement: WPF 层不直接引用 Data 层

系统 SHALL 确保 WPF 层（ViewModel/View）不直接引用 QP11.Data 项目，所有数据访问通过 Service 层接口完成。

#### Scenario: ViewModel 保存销售单
- **WHEN** 用户在 SellViewModel 中点击保存
- **THEN** 调用 `ISellService.CreateSellOrderAsync()` 或 `ISellService.UpdateSellOrderAsync()`，而非直接 `new UnitOfWork()` 或 `DatabaseFactory.Create()`

#### Scenario: ViewModel 查询数据
- **WHEN** SellViewModel 需要查询客户/业务员/销售单列表
- **THEN** 调用对应的 Service/Repository 接口方法，而非直接执行原始 SQL

### Requirement: 销售开单业务逻辑单一入口

系统 SHALL 确保销售开单的核心事务逻辑仅在 SellService.CreateSellOrderAsync 中实现，SellController 和 SellViewModel 必须委托调用。

#### Scenario: WebAPI 创建销售订单
- **WHEN** SellController.CreateOrder 接收 API 请求
- **THEN** 调用 `_sellService.CreateSellOrderAsync()` 执行事务，Controller 仅负责参数映射和响应格式化

#### Scenario: WPF 客户端保存销售单
- **WHEN** SellViewModel.SaveBillAsync 保存新建销售单
- **THEN** 委托 `_sellService.CreateSellOrderAsync()` 执行核心事务；对于编辑模式，委托 `_sellService.UpdateSellOrderAsync()`；ViewModel 仅处理 UI 状态和编辑模式差异

### Requirement: 采购开单业务逻辑单一入口

系统 SHALL 确保采购开单的核心事务逻辑仅在 BuyService 中实现，BuyController 和 BuyViewModel 必须委托调用。

#### Scenario: WebAPI 创建采购订单
- **WHEN** BuyController 接收 API 请求
- **THEN** 调用 `_buyService` 执行事务

#### Scenario: WPF 客户端保存采购单
- **WHEN** BuyViewModel.SaveBillAsync 保存采购单
- **THEN** 委托 BuyService 执行核心事务

### Requirement: 单据状态使用枚举类型

系统 SHALL 使用 `BillFlag` 枚举替代 `int?` 表示单据状态，消除魔法数字。

#### Scenario: 设置销售单状态
- **WHEN** 创建销售单时设置 Flag
- **THEN** 使用 `BillFlag.Confirmed` 而非硬编码 `1`

#### Scenario: Repository SQL 查询状态
- **WHEN** ArrearageRepository 按状态查询欠款
- **THEN** SQL 中通过参数传入 `(int)BillFlag.Confirmed`，而非硬编码 `flag=2`

### Requirement: 核心实体添加行为方法

系统 SHALL 将散落在 Service/ViewModel 中的核心业务逻辑封装到实体方法中。

#### Scenario: 计算销售单总额
- **WHEN** 需要计算销售单原价总额和折后总额
- **THEN** 调用 `BillSell.CalculateTotal(List<DetailSell> details)` 方法

#### Scenario: 设置明细行 Flag
- **WHEN** 创建退货单时需要设置 DetailSell.Flag
- **THEN** 调用 `DetailSell.ApplyFlag(bool isReturn)` 方法，flag=2（退货）或 flag=1（销售）

#### Scenario: 验证客户折扣率
- **WHEN** 销售开单时验证客户折扣率是否超出限制
- **THEN** 调用 `ClientInfor.ValidateDiscount(decimal requestedDiscount)` 方法

### Requirement: 清理死代码和空项目

系统 SHALL 移除无实现的方法和空项目。

#### Scenario: BaseRepository 清理
- **WHEN** 编译项目
- **THEN** BaseRepository 中不存在抛出 NotImplementedException 的 FindAsync 方法

#### Scenario: QP11.Models 项目
- **WHEN** 打开解决方案
- **THEN** 不存在空的 QP11.Models 项目

## MODIFIED Requirements

### Requirement: 依赖注入注册方式
原要求：App.xaml.cs 中 ServiceCollection 注册为接口→实现类映射
修改为：App.xaml.cs 中还需注册 `IDbConnectionFactory` → `DbConnectionFactory`（实现类），所有需要数据库连接的类通过构造函数注入 `IDbConnectionFactory`

### Requirement: ViewModel 数据访问
原要求：SellControl.xaml.cs 不再直接 new Repository 或调用 DatabaseFactory
修改为：所有 ViewModel 和 View 代码中不得出现 `DatabaseFactory.Create()` 或 `new UnitOfWork()` 调用，统一通过注入的 Service 接口访问数据

## REMOVED Requirements

无
