# Tasks

- [ ] Task 1: WPF技术栈选型与架构设计文档
  - [ ] SubTask 1.1: 编写WPF vs WinUI 3 vs Web对比报告（突出大数据量性能优势）
  - [ ] SubTask 1.2: 设计MVVM+分层架构图（Views/ViewModels/Services/Repository/Data）
  - [ ] SubTask 1.3: 制定DevExpress组件选型和PB控件映射规范（20+控件类型）
  - [ ] SubTask 1.4: 设计数据库连接方案和Entity映射规则（95张表→Dapper/EF Core）
  - [ ] SubTask 1.5: 输出完整的技术架构设计文档和性能基准测试方案

- [ ] Task 2: 功能清单完整性映射（200+功能点）
  - [ ] SubTask 2.1: 整理进销存管理45+功能点详细规格（采购12/销售18/库存15）
  - [ ] SubTask 2.2: 整理客户关系管理25+功能点详细规格
  - [ ] SubTask 2.3: ~~整理维修厂管理50+功能点详细规格~~ 【已移除维修模块】
  - [ ] SubTask 2.4: 整理财务管理30+功能点详细规格（账户10/收支8/欠款6/凭证2）【已移除在线支付4个功能】
  - [ ] SubTask 2.5: 整理会员借还/系统管理/报表55+功能点详细规格
  - [ ] SubTask 2.6: 生成完整功能清单矩阵表（原PB窗口→新WPF Window/UserControl映射）

- [ ] Task 3: UI界面还原设计方案（285个界面→WPF）
  - [ ] SubTask 3.1: 设计MDI主框架布局（MainWindow + DevExpress DXDockingManager）
  - [ ] SubTask 3.2: 设计10个核心业务界面的详细XAML原型和交互规格（含完整代码示例）
    - [ ] LoginWindow.xaml（登录界面，420×320居中对话框）
    - [ ] MainWindow.xaml（MDI主框架，菜单栏+工具栏+状态栏+文档区）
    - [ ] SellOrderWindow.xaml（销售开单，Master-Detail+虚拟滚动DataGrid）
    - [ ] PurchaseOrderWindow.xaml（采购管理）
    - [ ] PartSelectorWindow.xaml（配件选择器，左Tree右Grid+搜索）
    - [ ] ClientFormWindow.xaml（客户档案，Tab标签页布局）
    - [ ] RepairReceiveWindow.xaml（维修接车，三标签页复杂表单）【已移除】
    - [ ] AccountManageWindow.xaml（账户管理，左右分栏树+明细）
    - [ ] PrintPreviewWindow.xaml（打印预览，FlowDocument所见即所得）
    - [ ] ConfigWindow.xaml（系统设置，多标签页配置界面）
  - [ ] SubTask 3.3: 制定PB控件→WPF/DevExpress组件映射规范（20+控件类型，还原度≥95%）
  - [ ] SubTask 3.4: 设计10个自定义特殊UserControl规格（PartSelector/ClientSelector/BillPreview等）
  - [ ] SubTask 3.5: 整理25+右键菜单的ContextMenu XAML模板定义
  - [ ] SubTask 3.6: 制定视觉风格指南（Windows经典风格/XP主题/MaterialDesign可选）

- [ ] Task 4: 业务逻辑迁移方案（C#实现）
  - [ ] SubTask 4.1: 整理20+核心计算公式C#代码（销售/采购/库存/财务，含CalcService.cs完整代码）【已移除维修相关公式】
  - [ ] SubTask 4.2: 整理18+业务校验规则清单及ValidationService.cs触发条件
  - [ ] SubTask 4.3: 设计2个工作流状态机（SellStatus/MemberCardStatus枚举+转换规则）【已移除RepairStatus】
  - [ ] SubTask 4.4: 整理单据编号生成规则SerialNumberService.cs（XS/CG前缀格式）【已移除XL维修前缀】
  - [ ] SubTask 4.5: 编写关键算法的单元测试用例（计算公式精度测试<0.01元误差）

- [ ] Task 5: 使用逻辑与交互模式保留方案（WPF原生支持）
  - [ ] SubTask 5.1: 整理15+键盘快捷键InputBindings全局绑定清单（F5/F3/Delete/Esc/Ctrl+S/P等）
  - [ ] SubTask 5.2: 整理25+右键菜单ContextMenu XAML定义（销售/配件/客户/数据网格/工具栏场景）
  - [ ] SubTask 5.3: 绘制10个标准操作流程图（销售开单/采购等，与PB 100%一致）【已移除维修接车流程】
  - [ ] SubTask 5.4: 设计消息提示统一规范（MessageBox.Show() 7种场景，与PB一致度100%）
  - [ ] SubTask 5.5: 整理用户权限控制的UI体现方案（菜单灰显/按钮禁用/焦点管理Tab顺序）

- [ ] Task 6: 数据库零改动兼容方案（ODBC/ADO.NET直连qipei，兼容原PB的ODBC连接方式）
  - [ ] SubTask 6.1: 制定95张表的Entity类生成规则（Dapper [Table]/[Column]注解，含PartData/BillSell示例代码）
  - [ ] SubTask 6.2: 设计appsettings.json数据库连接配置和安全策略（ODBC DSN + SqlClient双模式支持/连接池/超时/重试/加密）
  - [ ] SubTask 6.3: 创建只读视图SQL脚本和兼容层（v_sell_detail_with_client等报表视图）
  - [ ] SubTask 6.4: 制定数据安全措施7项（加密/权限隔离/审计/备份/并发控制/本地安全/配置DPAPI加密）
  - [ ] SubTask 6.5: 设计并发控制和事务处理方案（TransactionScope乐观锁/UPDLOCK悲观锁）

- [ ] Task 7: 大数据量性能优化专项方案（⭐核心优势）
  - [ ] SubTask 7.1: DevExpress DataGrid虚拟滚动配置（VirtualizationMode=Row，内存节省99.85%）
  - [ ] SubTask 7.2: ADO.NET异步查询优化（Dapper QueryAsync + Task.Run后台线程不阻塞UI）
  - [ ] SubTask 7.3: 分页加载策略（PagingDataSource模式，每页50行，恒定内存占用）
  - [ ] SubTask 7.4: SQL Server索引优化建议（高频查询字段、LIKE模糊搜索、拼音索引namePy）
  - [ ] SubTask 7.5: 性能指标SLA制定（配件列表10万行<300ms vs Vue的3-5s，操作响应<50ms）

- [ ] Task 8: 分阶段实施路线图（12个月计划）
  - [ ] SubTask 8.1: Phase 1详细任务分解（基础设施搭建，第1-2月，10个子任务：脚手架/DI/登录/MDI框架/通用组件/快捷键/主题）
  - [ ] SubTask 8.2: Phase 2详细任务分解（核心业务模块，第3-5月，6个子任务：配件/客户供应商/销售/采购/库存/财务）
  - [ ] SubTask 8.3: Phase 3详细任务分解（特色业务模块，第6-8月，3个子任务：会员/借还/连锁同步）【已移除维修50+功能】
  - [ ] SubTask 8.4: Phase 4详细任务分解（系统管理与报表，第9-10月，5个子任务：权限/设置/备份/报表30+/打印/Excel导出）
  - [ ] SubTask 8.5: Phase 5详细任务分解（测试与上线，第11-12月，8个子任务：功能回归/性能测试/安全扫描/UAT验收/并行验证/部署打包/培训/切换）
  - [ ] SubTask 8.6: 制定里程碑检查点和交付物清单（每个Phase结束时的可演示版本）

- [ ] Task 9: 风险评估与应对策略（WPF桌面端特有风险）
  - [ ] SubTask 9.1: 识别技术风险（DevExpress授权成本/.NET 8兼容性/WINUI 3生态/大数据量极端场景）
  - [ ] SubTask 9.2: 识别业务风险（用户接受度（XP风格vs现代风格）/功能遗漏/逻辑差异/培训成本）
  - [ ] SubTask 9.3: 制定回滚策略（保留原PB系统作为备用，双系统并行运行过渡期）
  - [ ] SubTask 9.4: 估算资源需求（人力：2-3名.NET开发师+1名UI设计师；时间：12个月；硬件：开发机+测试SQL Server；成本：DevExpress授权~$2000/人/年或开源替代）

# Task Dependencies

- [Task 1] 是基础，应首先完成（WPF技术选型决定一切）
- [Task 2] 可以与 [Task 1] 并行（基于已有db_analysis_report.md即可开始）
- [Task 3] 依赖 [Task 1]，需要确定DevExpress组件库才能设计UI原型
- [Task 4] 可以与 [Task 2, 3] 并行（基于db_analysis_report.md + 原PB代码分析）
- [Task 5] 依赖 [Task 3]（UI设计决定交互模式和焦点管理）
- [Task 6] 可以独立并行（基于数据库结构报告，与UI无关）
- **[Task 7] 大数据量性能优化是核心任务**，应与[Task 3, 6]并行（这是选择WPF的主要原因）
- [Task 8] 依赖 [Task 2-7] 的全部输出
- [Task 9] 最后执行，综合所有任务的风险点
