# QP11 架构深度分析 — 验证检查清单

## P0 致命问题验证

### 事务支持
- [ ] IUnitOfWork 接口和实现已创建，支持 BeginTransaction/Commit/Rollback
- [ ] DatabaseFactory.CreateWithTransaction 方法可用
- [ ] BaseRepository 所有写操作支持可选 IDbTransaction 参数
- [ ] SellService.CreateSellOrderAsync 在单事务内完成单据+明细+库存+欠款+会员卡操作
- [ ] BuyService.CreateBuyOrderAsync 在单事务内完成单据+明细+欠款操作
- [ ] BuyService.ConfirmStockInAsync 在单事务内完成状态+库存操作
- [ ] FinanceService 收款/付款在单事务内完成余额+记录操作
- [ ] SellControl.SaveBill 在单事务内完成
- [ ] BuyControl.SettleBill 在单事务内完成
- [ ] SellService.VoidSellOrderAsync 在单事务内完成状态+库存+欠款删除+会员卡退款

### 流水号竞态
- [ ] SerialNumberService.GenerateSN 使用 UPDATE OUTPUT 原子操作，并发测试无重复编号
- [ ] PartRepository 插入不再使用 MAX+1，改用 IDENTITY 或 OUTPUT 子句
- [ ] CodeRuleRepository 序号递增使用原子 SQL，并发测试无重复

### 库存超卖
- [ ] PartStock 实体已添加 RowVersion/Timestamp 字段
- [ ] PartRepository.DecreaseStockAsync 包含 `WHERE amount >= @Qty` 条件，库存不足时返回 0
- [ ] ValidationService.ValidateStockAsync 真正验证库存量 >= 需求数量
- [ ] 并发销售同一配件测试：两个请求同时销售，库存正确扣减且不为负

### 安全漏洞
- [ ] PermissionService.HasPermission 在 _permissionsLoaded=false 时返回 false

### OdbcCompatCommand Bug
- [ ] SQL 中同一参数出现多次时，所有 ? 占位符都正确映射到参数值
- [ ] @@ROWCOUNT、@@IDENTITY 等系统变量不被错误替换为 ?
- [ ] SQL 解析结果有缓存，相同 SQL 不重复正则解析

---

## P1 严重问题验证

### Data 层 Bug 修复
- [ ] PaysRepository.GetByAccountAsync 返回结果已按 account_id 过滤
- [ ] CarMarkRepository.LogicDeleteAsync 执行 UPDATE 而非 DELETE
- [ ] BaseRepository.IsIdentityKey 对非自增主键实体返回 false
- [ ] SellRepository.GetListAsync 不包含 flag=-1 的记录
- [ ] SysLogRepository.GetListAsync 支持分页参数
- [ ] AccountRepository/ArrearageRepository/MemberCardRepository 余额更新使用原子增量

### Services 层修复
- [ ] BuyService.ConfirmStockInAsync 检查单据当前状态，已确认的不重复入库
- [ ] FinanceService.Pays 的 AccountId/Type/Memo 字段正确写入数据库
- [ ] SerialNumberService.GenerateSN 单次生成数据库查询 <= 2 次
- [ ] CalcService 折扣率语义文档化，所有调用方一致使用"支付比例"
- [ ] SellService/BuyService 明细批量插入，库存批量更新

### WPF 层修复
- [ ] LoginWindow.LoadUsers 使用 await 而非 .Result
- [ ] SellControl 搜索 Timer 复用单一实例，无事件处理器累积
- [ ] PartSelectorWindow 搜索有 300ms 防抖
- [ ] BuyControl 退货模式库存方向正确（采购退货增加库存）
- [ ] SellOrderWindow 会员卡余额不足时阻止保存并提示
- [ ] SellControl.LoadPartList 有默认分页或最小搜索条件保护
- [ ] BuyControl.CreateNewPartsAsync 使用批量插入+事务

### 性能优化
- [ ] 所有 Repository 查询排除 Picture 等 byte[] 字段
- [ ] BaseRepository 分页使用 OFFSET FETCH 替代 ROW_NUMBER
- [ ] 高频查询字段已有数据库索引（partno/name/namePy/datetime/flag 等）
- [ ] PartRepository.GetStockListAsync 返回强类型

---

## P2 中等问题验证

### DI 和 MVVM
- [ ] 所有 Repository/Service 通过 DI 容器注入，无直接 new
- [ ] SellControl/BuyControl 有独立 ViewModel
- [ ] View code-behind 仅包含 UI 交互逻辑
- [ ] RelayCommand 使用显式 RaiseCanExecuteChanged 替代 CommandManager

### Core 层规范
- [ ] 软删除标志统一（DEL 字段语义一致）
- [ ] 金额/数量类型统一为 decimal
- [ ] 明细实体有 BillDetailBase 基类
- [ ] ClientInfor/SupplierInfor 有 BusinessPartner 基类
- [ ] 属性名与列名语义匹配（无 Cid→bid 等混淆）
- [ ] ISerialNumberService 接口方法 <= 3 个

### 图片存储
- [ ] PartData.Picture 和 CarMark.Picture 不再存储 byte[]
- [ ] 图片存储在文件系统，数据库保留路径
- [ ] 图片有内存缓存+磁盘缓存机制

### ExportService
- [ ] 使用 SXSSFWorkbook 流式写入，10 万行导出内存 < 200MB
- [ ] 导出过程有进度报告
- [ ] 用户可选择保存路径

---

## P3 轻微问题验证

- [ ] 一个文件一个类型（无多类文件）
- [ ] PagedResult.TotalPages 对 PageSize=0 有防护
- [ ] 异常类支持序列化，包含结构化错误信息
- [ ] 拼音缩写字段有 XML 注释
- [ ] CloseProtectionHelper 实际拦截窗口关闭
- [ ] EnterToTabHelper 在 TextBox 中将 Enter 转为 Tab
- [ ] PrintSettingsService 有异常日志和文件权限检查
