# Tasks — QP11 架构问题修复任务清单

按优先级排序：P0 致命 → P1 严重 → P2 中等 → P3 轻微

---

## P0 致命问题（数据一致性/安全漏洞，必须立即修复）

- [x] Task 1: 引入全局事务支持框架
  - [x] SubTask 1.1: 在 QP11.Data 中创建 UnitOfWork 模式（IUnitOfWork 接口 + 实现，封装 IDbTransaction）
  - [x] SubTask 1.2: 修改 DatabaseFactory 支持事务连接（CreateWithTransaction 方法）
  - [x] SubTask 1.3: 修改 BaseRepository 支持接收外部事务（可选 IDbTransaction 参数）
  - [x] SubTask 1.4: 在 SellService.CreateSellOrderAsync 中包裹事务（单据+明细+库存+欠款+会员卡）
  - [x] SubTask 1.5: 在 BuyService.CreateBuyOrderAsync 中包裹事务（单据+明细+欠款）
  - [x] SubTask 1.6: 在 BuyService.ConfirmStockInAsync 中包裹事务（状态+库存）
  - [x] SubTask 1.7: 在 FinanceService.ReceivePaymentAsync/PaySupplierAsync 中包裹事务（余额+记录）
  - [x] SubTask 1.8: 在 SellControl.SaveBill 中包裹事务
  - [x] SubTask 1.9: 在 BuyControl.SettleBill 中包裹事务
  - [x] SubTask 1.10: 在 SellService.VoidSellOrderAsync 中包裹事务，并补充欠款删除和会员卡退款逻辑

- [x] Task 2: 修复流水号竞态条件
  - [x] SubTask 2.1: 重写 SerialNumberService.GenerateSN 使用 `UPDATE ... OUTPUT` 原子操作
  - [x] SubTask 2.2: 修复 PartRepository MAX+1 自增ID，改用 IDENTITY 列或 OUTPUT 子句
  - [x] SubTask 2.3: 修复 CodeRuleRepository 序号并发冲突，使用原子递增 SQL

- [x] Task 3: 修复库存超卖风险
  - [x] SubTask 3.1: 为 PartStock 实体添加 RowVersion/Timestamp 字段实现乐观并发控制
  - [x] SubTask 3.2: 修改 PartRepository.DecreaseStockAsync 添加负数检查（`WHERE amount >= @Qty`）
  - [x] SubTask 3.3: 修改 ValidationService.ValidateStockAsync 真正验证库存量（而非仅验证存在性）
  - [x] SubTask 3.4: 在库存扣减 SQL 中使用 `WITH (UPDLOCK)` 悲观锁或乐观锁 WHERE 条件

- [x] Task 4: 修复安全漏洞
  - [x] SubTask 4.1: 修改 PermissionService.HasPermission 权限加载失败时返回 false（而非 true）

- [x] Task 5: 修复 OdbcCompatCommand 参数转换 Bug
  - [x] SubTask 5.1: 修复重复参数名处理（同一参数出现多次时正确映射所有 ? 占位符）
  - [x] SubTask 5.2: 修复 @@系统变量误匹配（排除 @@ROWCOUNT、@@IDENTITY 等）
  - [x] SubTask 5.3: 添加 SQL 解析缓存，避免每次查询都做正则解析

---

## P1 严重问题（功能缺陷/性能瓶颈，短期修复）

- [x] Task 6: 修复 Data 层 Bug
  - [x] SubTask 6.1: 修复 PaysRepository.GetByAccountAsync 添加 `WHERE account_id = @AccountId` 条件
  - [x] SubTask 6.2: 修复 CarMarkRepository.LogicDeleteAsync 改为真正的逻辑删除（UPDATE del 字段）
  - [x] SubTask 6.3: 修复 BaseRepository.IsIdentityKey 改为检测 [DatabaseGenerated] 特性
  - [x] SubTask 6.4: 修复 SellRepository.GetListAsync 添加 `WHERE flag != -1` 过滤已删除记录
  - [x] SubTask 6.5: 修复 SysLogRepository.GetListAsync 添加分页支持
  - [x] SubTask 6.6: 修复 AccountRepository/ArrearageRepository/MemberCardRepository 余额更新改为原子增量（`charge = charge + @Delta`）

- [x] Task 7: 修复 Services 层问题
  - [x] SubTask 7.1: 修复 BuyService.ConfirmStockInAsync 添加状态校验（防止重复入库）
  - [x] SubTask 7.2: 修复 FinanceService.Pays 实体属性映射（确保 AccountId/Type/Memo 写入数据库）
  - [x] SubTask 7.3: 优化 SerialNumberService.GenerateSN 减少数据库查询次数（合并 SELECT COUNT + SELECT 为一次操作）
  - [x] SubTask 7.4: 修复 CalcService 折扣率语义统一（明确 discountRate 为"支付比例"）
  - [x] SubTask 7.5: 优化 SellService/BuyService 批量操作（明细批量插入、库存批量更新）

- [x] Task 8: 修复 WPF 层问题
  - [x] SubTask 8.1: 修复 LoginWindow.LoadUsers 将 `.Result` 改为 `await`
  - [x] SubTask 8.2: 修复 SellControl._searchTimer 事件处理器泄漏（复用 Timer 而非每次新建）
  - [x] SubTask 8.3: 修复 PartSelectorWindow.LoadPartsAsync 添加防抖（DispatcherTimer 延迟 300ms）
  - [x] SubTask 8.4: 修复 BuyControl 退货模式库存方向（确认业务逻辑后修正 Increase/Decrease）
  - [x] SubTask 8.5: 修复 SellOrderWindow 会员卡余额不足时阻止保存
  - [x] SubTask 8.6: 修复 SellControl.LoadPartList 添加默认分页或最小搜索条件
  - [x] SubTask 8.7: 修复 BuyControl.CreateNewPartsAsync 改为批量插入+事务

- [x] Task 9: 性能优化 — 数据库查询
  - [x] SubTask 9.1: 所有 Repository 的 SELECT * 改为指定列查询（排除 Picture 等 byte[] 字段）
  - [x] SubTask 9.2: BaseRepository 分页查询改用 OFFSET FETCH（SQL Server 2012+）
  - [ ] SubTask 9.3: 为高频查询字段添加数据库索引（part_data.partno/name/namePy、bill_sell.datetime/flag、client_infor.name/namePy 等）
  - [x] SubTask 9.4: PartRepository.GetStockListAsync 返回强类型替代 dynamic

---

## P2 中等问题（架构改进，中期优化）

- [ ] Task 10: 重构 DI 和 MVVM 架构
  - [ ] SubTask 10.1: 重构 App.xaml.cs DI 注册，所有 Repository/Service 通过构造函数注入
  - [ ] SubTask 10.2: 为核心 View（SellControl/BuyControl/PartSelectorWindow 等）创建 ViewModel
  - [ ] SubTask 10.3: 将 View code-behind 中的业务逻辑迁移到 ViewModel
  - [ ] SubTask 10.4: 替换 CommandManager.RequerySuggested 为显式 RaiseCanExecuteChanged

- [ ] Task 11: 统一 Core 层设计规范
  - [ ] SubTask 11.1: 统一软删除标志（DEL 字段：'0'=正常, '1'=已删, NULL=正常）
  - [ ] SubTask 11.2: 统一金额类型为 decimal（修改 DetailSell.Amount 从 long? 改为 decimal?）
  - [ ] SubTask 11.3: 为明细实体提取 BillDetailBase 基类
  - [ ] SubTask 11.4: 为 ClientInfor/SupplierInfor 提取 BusinessPartner 基类
  - [ ] SubTask 11.5: 修复属性名与列名语义不匹配（Arrearage.Cid→bid 等）
  - [ ] SubTask 11.6: 重构 ISerialNumberService 接口（12 个方法合并为参数化设计）

- [ ] Task 12: 图片存储优化
  - [ ] SubTask 12.1: 将 PartData.Picture 和 CarMark.Picture 从 byte[] 迁移到文件存储
  - [ ] SubTask 12.2: 数据库只保留图片路径引用
  - [ ] SubTask 12.3: 添加图片缓存机制（内存缓存 + 磁盘缓存）

- [ ] Task 13: ExportService 优化
  - [ ] SubTask 13.1: 将 XSSFWorkbook 替换为 SXSSFWorkbook（流式写入）
  - [ ] SubTask 13.2: 添加导出进度报告
  - [ ] SubTask 13.3: 添加文件保存路径选择对话框

---

## P3 轻微问题（代码质量，长期改进）

- [ ] Task 14: 代码质量改进
  - [ ] SubTask 14.1: 拆分一个文件多个类型（BillBaosun.cs、BillJhdh.cs、Quotation.cs、ICalcService.cs、SellModels.cs）
  - [ ] SubTask 14.2: 添加 PagedResult.TotalPages 除零防护
  - [ ] SubTask 14.3: 统一异常类添加序列化构造函数和结构化错误信息
  - [ ] SubTask 14.4: 拼音缩写字段添加 XML 注释说明
  - [ ] SubTask 14.5: CloseProtectionHelper 添加实际的 Closing 事件拦截逻辑
  - [ ] SubTask 14.6: EnterToTabHelper 修正逻辑（在 TextBox 中也将 Enter 转为 Tab）
  - [ ] SubTask 14.7: PrintSettingsService 添加异常日志和文件权限检查

---

# Task Dependencies

- [Task 1] 是基础，所有涉及事务的修复都依赖它
- [Task 2] 可独立执行，不依赖其他任务
- [Task 3] 依赖 [Task 1]（库存扣减需要在事务内执行）
- [Task 4] 可独立执行
- [Task 5] 可独立执行
- [Task 6-9] 可并行执行，但 [Task 6.6] 依赖 [Task 1]（余额原子更新需要事务上下文）
- [Task 10] 依赖 [Task 1-9] 完成（架构重构应在 Bug 修复后进行）
- [Task 11-13] 可并行执行，互不依赖
- [Task 14] 优先级最低，可随时执行
