# QP11重构方案检查清单 (WPF/.NET 8版)

## 技术栈与架构设计
- [ ] 技术选型对比报告已完成（WPF vs WinUI 3 vs MAUI vs Electron）
- [ ] 系统架构图已绘制（MVVM四层架构：View→ViewModel→Service→Repository→Database）
- [ ] 数据库连接方案已确定（Dapper + ADO.NET直连qipei零改动）
- [ ] Entity映射规则已制定（95张表→95个实体类，[Table]/[Column]注解）

## 功能完整性保证
- [ ] 进销存管理45+功能点已完整列出（采购12/销售18/库存15）
- [ ] 客户关系管理25+功能点已完整列出
- [ ] ~~维修厂管理50+功能点已完整列出~~ 【已移除维修模块】
- [ ] 财务管理30+功能点已完整列出（账户10/收支8/欠款6/凭证2）【已移除在线支付4个功能】
- [ ] 会员借还/系统管理/报表55+功能点已完整列出
- [ ] 原PB窗口→新WPF窗口映射矩阵表已完成（约250个窗口，已移除维修模块50+窗口）

## UI界面还原设计（WPF原生控件+DevExpress）
- [ ] MDI主框架布局方案已完成（DXDockingManager多文档界面）
- [ ] 10个核心界面XAML代码示例已完成：
  - [ ] LoginWindow.xaml（登录窗口420×320，IsDefault/IsCancel按钮）
  - [ ] MainWindow.xaml（MDI主窗口：菜单栏+工具栏+状态栏+工作区）
  - [ ] SellOrderWindow.xaml（销售开单主从布局，VirtualizationMode="Row" GridControl）
  - [ ] PurchaseWindow.xaml（采购入库单）
  - [ ] PartSelectorWindow.xaml（配件选择器，虚拟滚动10万行<300ms）
  - [ ] ClientManagerWindow.xaml（客户档案管理）
  - [ ] RepairReceiveWindow.xaml（维修接车单）【已移除】
  - [ ] AccountManageWindow.xaml（账户管理）
  - [ ] PrintPreviewWindow.xaml（打印预览对话框）
  - [ ] SystemSettingsWindow.xaml（系统设置）
- [ ] PB控件→WPF/DevExpress映射表已完成（20+控件类型，90-100%保真度）
- [ ] DevExpress自定义模板规格已完成（DataGridTemplateSelector等）
- [ ] 25+右键菜单交互设计已完成（ContextMenu + InputBindings）
- [ ] 视觉风格指南已完成（颜色/字体/间距/图标规范匹配原系统）

## 大数据量性能优化（核心优势）
- [ ] 虚拟滚动方案已完成（GridControl VirtualizationMode="Row"，仅渲染~50可见行）
- [ ] 性能SLA指标已定义：
  - [ ] 配件列表10万行加载 <300ms（vs Vue的3-5秒）
  - [ ] 销售明细5000行 <200ms响应
  - [ ] 内存占用节省99.85%（分页+虚拟化）
- [ ] 异步数据加载方案已完成（async/await + Task.Run）
- [ ] 数据库查询优化策略已完成（索引利用/分页SQL/只读必要字段）
- [ ] DXDataGrid大数据最佳实践配置已完成

## 业务逻辑迁移（C#实现）
- [ ] 20+核心计算公式C#代码已完成：
  - [ ] CalcService.CalculateLineSubtotal()（小计计算）
  - [ ] CalcService.CalculateSellOrderSummary()（销售汇总）
  - [ ] CalcService.ValidateDiscountRate()（折扣率校验0-100%）
  - [ ] ~~RepairSettlementResult结算计算（人工费+材料费-优惠）~~ 【已移除】
  - [ ] 库存预警算法（安全库存/订货点判断）
  - [ ] 应收应付余额计算
  - [ ] 会员积分/储值消耗规则
- [ ] 18+业务校验规则清单已完成（触发条件+错误提示+MessageBox.Show）
- [ ] 2个工作流状态机C#枚举类已设计：【已移除RepairStatus】
  - [ ] SellStatus（草稿→确认→部分收款→完成→作废）
  - [ ] MemberCardStatus（正常→挂失→冻结→注销）
- [ ] 单据编号生成服务已完成（SerialNumberService.GenerateSN：XS/CG前缀+日期+序号）【已移除XL维修前缀】
- [ ] 关键算法流程图和伪代码已完成

## 使用逻辑保留（WPF原生支持）
- [ ] 15+键盘快捷键InputBindings清单已完成：
  - [ ] F5保存 / F3新增 / Delete删除 / Esc关闭
  - [ ] Ctrl+N新建 / Ctrl+S保存 / Ctrl+P打印
  - [ ] Enter跳下一字段 / Tab顺序导航
- [ ] 25+右键菜单上下文场景定义已完成（ContextMenu XAML资源字典）
- [ ] 10个标准操作流程图已完成（销售开单/采购入库等核心流程）【已移除维修接车流程】
- [ ] 消息提示统一规范已完成（MessageBox.Show匹配PB的MessageBox）
- [ ] 用户权限控制UI体现方案已完成（菜单灰化/按钮禁用/Visible绑定）

## 数据库兼容方案（零迁移）
- [ ] 95张表的Entity类生成规则已完成（命名规范/类型映射/注解示例）
- [ ] 数据库连接配置appsettings.json已完成（ODBC DSN + SqlClient双模式支持，兼容原PB的ODBC连接方式）
- [ ] Dapper Repository基类已完成（CRUD泛型方法）
- [ ] 只读视图和兼容层SQL脚本已准备（如需要）
- [ ] 数据安全措施5项已完成（加密/权限/审计/备份/并发事务）
- [ ] 并发控制和事务处理方案已完成（TransactionScope/IDbTransaction）

## 项目工程结构
- [ ] 解决方案结构已设计（QP11.sln包含9个项目）
- [ ] NuGet依赖包清单已完成（DevExpress.Wpf/Dapper/System.Data.Odbc/Microsoft.Data.SqlClient等）【已添加ODBC支持】
- [ ] 目录结构规范已完成（Views/ViewModels/Services/Repositories/Models/Helpers）
- [ ] 配置文件模板已完成（appsettings.json/app.config）

## 实施路线图（12个月5阶段）
- [ ] Phase 1任务分解完成（基础设施搭建，第1-2月）：
  - [ ] 创建解决方案和9个项目
  - [ ] 安装配置DevExpress/Dapper等NuGet包
  - [ ] 设计数据库连接层（BaseRepository<T>）
  - [ ] 生成95个Entity实体类
  - [ ] 实现登录模块（LoginWindow + AuthService）
  - [ ] 搭建MDI主框架（MainWindow + DXDockingManager）
  - [ ] 基础设施单元测试
- [ ] Phase 2任务分解完成（进销存核心，第3-5月）：
  - [ ] 配件选择器（PartSelectorWindow，10万行虚拟滚动优化）
  - [ ] 销售开单模块（SellOrderWindow + CalcService）
  - [ ] 采购入库模块（PurchaseWindow）
  - [ ] 库存管理模块（InventoryQuery/Adjustment/Alert）
  - [ ] 销售退货/采购退货模块
  - [ ] 进销存集成测试
- [ ] Phase 3任务分解完成（会员/借还/连锁同步，第6-8月）【已移除维修厂业务】：
  - [ ] ~~维修接车模块（RepairReceiveWindow + WorkflowService）~~
  - [ ] ~~维修项目管理（ProjectAdd/Modify/Delete）~~
  - [ ] ~~维修领料模块（MaterialRequisition）~~
  - [ ] ~~维修结算模块（SettlementCalculation + PrintPreview）~~
  - [ ] 会员卡管理模块（MemberCard CRUD + Recharge/Consume）
  - [ ] 借还管理模块（Borrow/Lend CRUD + Settlement）
  - [ ] 连锁版数据同步模块（FTP Download/Upload）
- [ ] Phase 4任务分解完成（财务与系统管理，第9-10月）：
  - [ ] 账户管理模块（Account CRUD + Transfer/Adjustment）
  - [ ] 收支记账模块（IncomeExpenseEntry）
  - [ ] 应收应付模块（AR/AP Query/Payment）
  - [ ] 系统设置模块（Operator/DataDict/Backup）【已移除软件授权/系统注册功能】
  - [ ] 权限控制模块（RolePermission + MenuAuthorization）
  - [ ] 报表模块（RDLC/DevExpress Reports）
- [ ] Phase 5任务分解完成（测试与上线，第11-12月）：
  - [ ] 全系统集成测试（200+功能点回归）
  - [ ] 大数据性能压测（10万行配件列表/5000行销售明细）
  - [ ] UI保真度验收（与原PB系统逐屏对比）
  - [ ] 用户操作培训（快捷键/操作流程手册）
  - [ ] 数据迁移验证（对比原库数据一致性）
  - [ ] 生产环境部署（MSIX打包/ClickOnce发布）

## 风险评估
- [ ] 技术风险识别完成（DevExpress授权成本/学习曲线/数据一致性/性能瓶颈）
- [ ] 业务风险识别完成（功能遗漏/逻辑差异/用户接受度/并行运行期）
- [ ] 风险应对预案和回滚策略已完成（Feature Toggle/灰度发布/快速回滚机制）
- [ ] 资源需求估算完成（人力/.NET开发经验/时间/成本/硬件配置要求）

## 综合输出物
- [ ] 重构方案主报告spec.md已整合所有内容（✅已完成1800+行）
- [ ] 任务分解tasks.md已完成（9大任务40+子任务）（✅已完成）
- [ ] 执行摘要和关键决策点已编写（为何选WPF/Vue失败原因/性能优势）
- [ ] 代码示例完整性验证（XAML/C#/Entity/Configuration）
- [ ] 决策建议和下一步行动方案已提供（等待用户审批后开始实施）
