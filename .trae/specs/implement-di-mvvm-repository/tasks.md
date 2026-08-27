# Tasks

- [x] Task 1: Core 层新增具体仓储接口
  - [x] SubTask 1.1: 创建 ISellRepository.cs（继承 IRepository<BillSell>，含 GetBySnAsync/GetDetailsAsync/InsertBillAsync/InsertDetailsAsync/DeleteDetailsAsync/UpdateBillStatusAsync/UpdateMemoAsync/GetListAsync）
  - [x] SubTask 1.2: 创建 IPartRepository.cs（继承 IRepository<PartData>，含 GetStockListAsync/GetStockListAdvancedAsync/GetStockByIdAsync/DecreaseStockAsync/IncreaseStockAsync/SearchAsync）
  - [x] SubTask 1.3: 创建 IClientRepository.cs、ISupplierRepository.cs、IAccountRepository.cs、IPaysRepository.cs（含 GetByAccountAsync）、IArrearageRepository.cs（含 GetClientArrearTotalAsync）、IMemberCardRepository.cs（含 ConsumeAsync）、IBorrowRepository.cs、IUserRepository.cs、ISysLogRepository.cs（含 GetListAsync）
- [x] Task 2: Core 层新增服务接口
  - [x] SubTask 2.1: 创建 IValidationService.cs（含 ValidateStockAsync/ValidateClientCreditAsync/ValidateDiscountRate/ValidateAmount/ValidateRequired/ValidateDateNotFuture）
  - [x] SubTask 2.2: 创建 ISellService.cs（含 CreateSellOrderAsync/VoidSellOrderAsync）
  - [x] SubTask 2.3: 创建 IBuyService.cs、IFinanceService.cs
- [x] Task 3: Data 层仓储实现新接口并修复连接管理
  - [x] SubTask 3.1: SellRepository 实现 ISellRepository 接口声明
  - [x] SubTask 3.2: PartRepository 实现 IPartRepository 接口声明
  - [x] SubTask 3.3: 其余 Repository 实现对应接口声明（ClientRepository/SupplierRepository/AccountRepository/PaysRepository/ArrearageRepository/MemberCardRepository/BorrowRepository/UserRepository/SysLogRepository）
  - [x] SubTask 3.4: 修复 BaseRepository 连接释放模式——InsertAsync/UpdateAsync/DeleteAsync 改为 try-finally 确保 Dispose
- [x] Task 4: Services 层重构为构造函数注入
  - [x] SubTask 4.1: ValidationService 改为构造函数注入 IPartRepository/IClientRepository/IArrearageRepository，实现 IValidationService
  - [x] SubTask 4.2: SellService 改为构造函数注入 ISellRepository/IPartRepository/IArrearageRepository/IMemberCardRepository/IValidationService/ISerialNumberService，实现 ISellService
  - [x] SubTask 4.3: BuyService 改为构造函数注入，实现 IBuyService
  - [x] SubTask 4.4: FinanceService 改为构造函数注入，实现 IFinanceService
  - [x] SubTask 4.5: AuthService 实现 IAuthService（已是，确认无需改动）
  - [x] SubTask 4.6: CalcService 实现 ICalcService（已是，确认无需改动）
  - [x] SubTask 4.7: SerialNumberService 实现 ISerialNumberService（已是，确认无需改动）
- [x] Task 5: DI 容器注册方式变更
  - [x] SubTask 5.1: 修改 App.xaml.cs 中 ServiceCollection 注册——从 `services.AddTransient<SellRepository>()` 改为 `services.AddTransient<ISellRepository, SellRepository>()` 等接口映射
  - [x] SubTask 5.2: 确认所有通过 ServiceProvider 解析的地方仍能正常工作
- [x] Task 6: SellControl 业务逻辑提取到 SellViewModel
  - [x] SubTask 6.1: 创建 SellViewModel.cs，继承 BaseViewModel，包含 Details 集合、计算属性、业务方法
  - [x] SubTask 6.2: 将 SellControl.SaveBill 的数据库操作逻辑移入 SellViewModel.SaveBillAsync
  - [x] SubTask 6.3: 将 SellControl.VoidSelectedBill 逻辑移入 SellViewModel.VoidBillAsync
  - [x] SubTask 6.4: 将 SellControl 的查询/加载逻辑移入 SellViewModel 对应方法
  - [x] SubTask 6.5: 修改 SellControl.xaml.cs 通过构造函数注入 SellViewModel，事件处理器调用 ViewModel 方法
  - [x] SubTask 6.6: 修改 MainWindow/SellQueryControl 中 new SellControl() 改为注入 ViewModel
- [x] Task 7: 补充空 catch 的日志记录
  - [x] SubTask 7.1: SellControl.xaml.cs 中所有 `catch { }` 补充 `Serilog.Log.Warning(ex, ...)`
  - [x] SubTask 7.2: LoginWindow.xaml.cs 中 catch 块补充日志
  - [x] SubTask 7.3: 其他 View 中明显的空 catch 补充日志（共28个文件）
- [x] Task 8: 编译验证与功能回归
  - [x] SubTask 8.1: 执行 `dotnet build` 确认零编译错误 ✅
  - [ ] SubTask 8.2: 启动应用验证登录→主界面→销售开单→保存→查询→作废全流程正常（需手动验证）

# Task Dependencies

- [Task 1] 和 [Task 2] 可并行，无依赖
- [Task 3] 依赖 [Task 1]（仓储接口定义后才能实现）
- [Task 4] 依赖 [Task 1, 2]（服务需要注入仓储接口和服务接口）
- [Task 5] 依赖 [Task 1, 2, 3, 4]（所有接口和实现就绪后才能注册）
- [Task 6] 依赖 [Task 4, 5]（ViewModel 需要注入的服务已就绪）
- [Task 7] 可与 [Task 6] 并行
- [Task 8] 依赖所有其他任务完成
