# Tasks

- [ ] Task 1: 补充缺失的Entity字段映射（基于DataWindow分析）
  - [ ] SubTask 1.1: DetailSell实体补充 amount2/cb/part_th/part_gg/part_cclb 字段
  - [ ] SubTask 1.2: BillSell实体补充 discount_rate/total_payment/bill_payment/cash/checks/arrear/checkno 字段
  - [ ] SubTask 1.3: BillBuy实体补充 zhifubao/weixin/yunfei/invoice 字段
  - [ ] SubTask 1.4: PartStock实体补充 sell_use 字段
  - [ ] SubTask 1.5: PartData实体补充 part_bzq/part_bzrq 字段
  - [ ] SubTask 1.6: Account实体补充 bz 字段
  - [ ] SubTask 1.7: 新增 Desktop 实体（desktop表：code/name/buildtime/memo/username）
  - [ ] SubTask 1.8: 更新对应Repository的SQL查询，包含新增字段
  - [ ] SubTask 1.9: 更新DapperTypeMapper.Register注册

- [ ] Task 2: 补充销售模块UI细节（基于d_sell_query/d_bill_sell分析）
  - [ ] SubTask 2.1: SellQueryWindow添加条件颜色——退货行(flag=2)红色显示
  - [ ] SubTask 2.2: SellQueryWindow添加汇总行——sum(stotal)/sum(btotal)/sum(amount)
  - [ ] SubTask 2.3: SellQueryWindow默认排序——单号降序
  - [ ] SubTask 2.4: SellQueryWindow默认过滤——amount>0
  - [ ] SubTask 2.5: SellEditWindow补充字段——折扣率/实收/现金/支票/欠款/支票号

- [ ] Task 3: 补充采购模块UI细节（基于d_buy_query/d_bill_buy分析）
  - [ ] SubTask 3.1: BuyQueryWindow添加汇总行——sum(intotal)
  - [ ] SubTask 3.2: BuyQueryWindow默认排序——单号升序
  - [ ] SubTask 3.3: BuyEditWindow补充字段——支付宝/微信/运费/发票号

- [ ] Task 4: 补充库存/配件模块UI细节（基于d_part_list分析）
  - [ ] SubTask 4.1: InventoryWindow添加条件颜色——停用配件(isck>0)蓝色显示
  - [ ] SubTask 4.2: InventoryWindow默认排序——sell_use降序（销售次数）
  - [ ] SubTask 4.3: InventoryWindow添加列——出厂类别/规格/图号/销售次数/保质期/保质日期
  - [ ] SubTask 4.4: PartEditWindow补充字段——保质期(part_bzq)/保质日期(part_bzrq)

- [ ] Task 5: 补充财务模块UI细节（基于d_account分析）
  - [ ] SubTask 5.1: AccountWindow添加收入/支出分列显示（CASE WHEN flag逻辑）
  - [ ] SubTask 5.2: AccountWindow添加汇总行——sum(收入)/sum(支出)/余额
  - [ ] SubTask 5.3: AccountWindow默认排序——datetime D, sn D

- [ ] Task 6: 实现缺失的功能模块（基于DataWindow发现）
  - [ ] SubTask 6.1: 进货到货管理（bill_jhdh/detail_jhdh）— JhdhWindow + JhdhRepository
  - [ ] SubTask 6.2: 报损管理（baosun）— BaosunWindow + BaosunRepository
  - [ ] SubTask 6.3: 预警设置（d_warning_edit/query）— WarningWindow
  - [ ] SubTask 6.4: 桌面快捷方式（desktop表）— DesktopWidget

- [ ] Task 7: 补充dddw下拉数据窗口（基于dddw引用分析）
  - [ ] SubTask 7.1: 确认所有ComboBox下拉数据源与原dddw一致
  - [ ] SubTask 7.2: 补充CLASSES字典表下拉（出厂类别part_cclb等CLASS_TYPE分组）
  - [ ] SubTask 7.3: 补充车牌下拉（d_dddw_car_mark → car_mark表）

# Task Dependencies

- [Task 1] 是基础，应首先完成（Entity字段映射影响所有后续任务）
- [Task 2-5] 可以并行（各模块UI补充互不依赖）
- [Task 6] 依赖 [Task 1]（新模块需要先创建Entity和Repository）
- [Task 7] 可以与 [Task 2-5] 并行（下拉数据源独立于UI布局）
