# Tasks

- [x] Task 1: MDI主框架重构（MainWindow）
  - [x] SubTask 1.1: 重构MainWindow为MDI框架，使用Tab控件承载子窗口（替代独立弹出）
  - [x] SubTask 1.2: 实现菜单栏10个顶级菜单（注册与登录/进销存管理/财务管理/业务查询/基础数据/高级功能/传真系统/系统管理/会员管理/退出系统），子菜单项与PB m_main完全一致
  - [x] SubTask 1.3: 实现工具栏F1-F12按钮，Command绑定到当前活动子窗口的对应事件
  - [x] SubTask 1.4: 实现状态栏（操作员/权限组/连接状态图标/服务器状态/日期时间）
  - [x] SubTask 1.5: 实现菜单权限控制（根据mnu表auth字段控制菜单项可见性/启用状态）
  - [x] SubTask 1.6: 实现子窗口管理（打开/关闭/切换/排列），菜单点击在Tab中打开对应子窗口

- [x] Task 2: 桌面快捷方式导航窗口（w_desktop还原）
  - [x] SubTask 2.1: 创建DesktopWindow，左侧6组GroupBox共24个快捷按钮（进销存/销售/仓库/查询/财务/高级功能）
  - [x] SubTask 2.2: 右侧DataGrid显示desktop表数据（图标/名称/编码/时间），双击打开功能
  - [x] SubTask 2.3: 实现右键菜单（编辑/删除/刷新/按名称排序/按时间排序）
  - [x] SubTask 2.4: 实现按钮Tag值→菜单树递归查找→触发对应功能
  - [x] SubTask 2.5: 登录后自动打开桌面导航窗口作为首个Tab页

- [x] Task 3: 销售开单窗口重构（w_sell还原）
  - [x] SubTask 3.1: 创建SellWindow，实现双模式切换（开单模式↔查询模式）
  - [x] SubTask 3.2: 开单模式布局：dw_query(查询条件)+dw_part(配件列表)+dw_detail(销售明细)+dw_bill(单头)+图片
  - [x] SubTask 3.3: 查询模式布局：dw_bill_list(单据列表)→点击行自动加载dw_bill+dw_detail
  - [x] SubTask 3.4: 实现工具栏快捷键F1-F12映射（新增/编辑/查询/删除/保存/结算/打印/退货/取消/历史/关闭）
  - [ ] SubTask 3.5: 实现顶部模块导航按钮栏（采购/计划/退货/查询/销售/快速/退货/查询/预售/仓位/盘点/预警/报损/借货/归还/计算器/记事本/锁屏）
  - [ ] SubTask 3.6: 实现退货模式（dw_back替代dw_detail，amount2可编辑）
  - [ ] SubTask 3.7: 实现配件查询条件区（partno/name/cartype/memo/place/class，支持拼音搜索，qflag查询方式切换）

- [ ] Task 4: 销售编辑弹窗还原（w_sell_edit）
  - [ ] SubTask 4.1: 创建SellEditDialog模态对话框（CenterOwner, NoResize）
  - [ ] SubTask 4.2: 左侧布局：客户查询+配件编号/名称显示+历史价格提示+车牌号/车型输入+数量+零售/批发切换+单价+开票单价+自动匹配历史价格
  - [ ] SubTask 4.3: 右侧布局：销售历史列表（双击选价格）+采购历史列表（只读参考）
  - [ ] SubTask 4.4: 实现价格逻辑（零售=lsprice/批发=pfprice/单价变更自动同步开票单价）
  - [ ] SubTask 4.5: 实现自动匹配客户历史价格（cbx_1勾选时查detail_sell最后一次价格）
  - [ ] SubTask 4.6: 实现st_sell_edit结构体参数传递（输入/输出）

- [x] Task 5: 采购管理窗口重构（w_buy还原）
  - [x] SubTask 5.1: 创建BuyWindow，左侧dw_bill+dw_detail，右侧dw_bill_list
  - [x] SubTask 5.2: 实现RadioButton状态切换（未结算rb_1/已结算rb_2/退货rb_3）
  - [x] SubTask 5.3: 实现Master-Detail联动（dw_bill_list点击→ue_view→检索dw_bill+dw_detail）
  - [x] SubTask 5.4: 实现工具栏快捷键F1-F12映射
  - [x] SubTask 5.5: 实现特殊按钮（式样新增cb_1/批量新增cb_3/采购转销售cb_2）
  - [ ] SubTask 5.6: 实现退货模式（dw_back替代dw_detail，amount2可编辑）

- [ ] Task 6: 采购编辑弹窗还原（w_buy_edit）
  - [ ] SubTask 6.1: 创建BuyEditDialog模态对话框
  - [ ] SubTask 6.2: dw_1 Freeform布局（partno/name/carname/cartype/unit/area/class/place/inprice/amount/lsprice/pfprice/memo/part_th/part_gg/part_cclb）
  - [ ] SubTask 6.3: 实现DDDW下拉联动（partno变更自动填充所有字段）
  - [ ] SubTask 6.4: 实现配件选择（双击partno打开PartSelectorWindow）
  - [ ] SubTask 6.5: 实现搜索模式切换（精确/模糊/精确匹配RadioButton）
  - [ ] SubTask 6.6: 实现配件图片显示和上传
  - [ ] SubTask 6.7: 底部采购历史列表（d_buy_his）

- [x] Task 7: 账目管理窗口重构（w_account还原）
  - [x] SubTask 7.1: 重构AccountWindow为Master-Detail布局（上方dw_1账目列表+下方dw_3明细列表）
  - [x] SubTask 7.2: 实现明细自动切换（名称含"采购"→d_detail_buy_list，其他→d_detail_sell_list）
  - [x] SubTask 7.3: 实现rowfocuschanged联动（点击账目行→查询关联采购/销售明细）
  - [x] SubTask 7.4: 实现工具栏（F1新增/F3删除/F6刷新/F7打印/编辑/查询/导出Excel）

- [x] Task 8: 应收应付款窗口重构（w_arrear还原）
  - [x] SubTask 8.1: 重构ArrearageWindow为左右Master-Detail布局（左侧客户列表+右侧欠款明细）
  - [x] SubTask 8.2: 实现搜索框拼音+名称实时过滤
  - [x] SubTask 8.3: 实现按钮区（明细/收款/打印对账单/全选/确认到账/对账查询）
  - [x] SubTask 8.4: 实现收款逻辑（INSERT INTO pays + account，更新charge）
  - [x] SubTask 8.5: 实现模式参数（ls_flag=1应付款/2应收款/3代收款）

- [x] Task 9: 基础数据窗口还原（w_basic）
  - [x] SubTask 9.1: 创建BasicDataWindow弹出窗口
  - [x] SubTask 9.2: 实现RadioButton类别切换（配件/车型/品牌号/区域/单位）
  - [x] SubTask 9.3: 实现动态SQL（SELECT DISTINCT [字段] FROM part_data）
  - [x] SubTask 9.4: 实现可编辑Grid + 自动拼音生成 + 保存时UPDATE part_data

- [x] Task 10: 报损管理窗口重构（w_baosun还原）
  - [x] SubTask 10.1: 重构BaosunWindow为双模式切换（开单↔查询）
  - [x] SubTask 10.2: 开单模式：dw_query+dw_part+dw_detail+图片
  - [x] SubTask 10.3: 查询模式：dw_bill_list（flag=3的bill_sell记录）
  - [x] SubTask 10.4: 实现工具栏F1-F12映射
  - [x] SubTask 10.5: 实现报损结算（保存bill_sell flag=3 + detail_sell + 扣减库存）

- [x] Task 11: 借货管理窗口重构（w_borrow还原）
  - [x] SubTask 11.1: 重构BorrowWindow为左右Master-Detail布局
  - [x] SubTask 11.2: 实现RadioButton状态切换（在借flag=3/归还flag=4）
  - [x] SubTask 11.3: 实现归还模式（dw_back替代dw_detail，编辑归还数量）
  - [x] SubTask 11.4: 实现工具栏F1-F12映射

- [x] Task 12: 查询窗口重构
  - [x] SubTask 12.1: 重构SellQueryWindow为独立查询窗口（dw_1只读Grid + w_sell_pop查询条件弹窗）
  - [x] SubTask 12.2: 重构BuyQueryWindow为独立查询窗口（dw_1只读Grid + w_pop_buy查询条件弹窗）
  - [x] SubTask 12.3: 实现查询条件弹窗（日期范围/单号/客户/供应商/业务员/状态等）
  - [x] SubTask 12.4: 实现条件颜色（退货行红色flag=2）和汇总行

- [x] Task 13: 登录窗口还原（w_check）
  - [x] SubTask 13.1: 重构LoginWindow布局（顶部蓝色装饰+标题+左侧汽车图片+右侧操作员下拉+密码+确认/取消）
  - [x] SubTask 13.2: 实现汽车图片定时切换（1.5秒，car0-3.jpg）
  - [x] SubTask 13.3: 实现操作员下拉框从user_infor加载

- [x] Task 14: 通用交互模式还原
  - [x] SubTask 14.1: 实现Enter→Tab跳转（所有编辑窗口）
  - [x] SubTask 14.2: 实现拼音搜索（所有搜索框支持name_py/cartype_py匹配）
  - [ ] SubTask 14.3: 实现DDDW下拉数据窗口模式（ComboBox可编辑+实时过滤+双击选择）
  - [x] SubTask 14.4: 实现关闭保护（未保存修改时弹出确认对话框）
  - [x] SubTask 14.5: 实现resize自适应（窗口大小改变时动态调整DataGrid尺寸）
  - [ ] SubTask 14.6: 实现配件图片显示（选中配件时从part_data读取picture显示）

- [x] Task 15: 修复现有BUG
  - [x] SubTask 15.1: 修复SellEditWindow保存使用InsertBillAsync而非UpdateAsync
  - [x] SubTask 15.2: 修复BuyEditWindow保存使用InsertBillAsync而非UpdateAsync
  - [x] SubTask 15.3: 修复快捷键绑定无效（InputBindings Command={x:Null}）
  - [x] SubTask 15.4: 修复SellQueryWindow/BuyQueryWindow双击查看详情未实现
  - [x] SubTask 15.5: 修复导出Excel功能未实现

# Task Dependencies

- [Task 1] 是基础，必须首先完成（MDI框架决定所有子窗口的承载方式）
- [Task 2] 依赖 [Task 1]（桌面窗口作为MDI子窗口打开）
- [Task 3-6] 依赖 [Task 1]（销售/采购窗口作为MDI子窗口），[Task 3] 和 [Task 5] 可并行
- [Task 7-8] 依赖 [Task 1]，可并行
- [Task 9-11] 依赖 [Task 1]，可并行
- [Task 12] 依赖 [Task 1]，可并行
- [Task 13] 独立，可与 [Task 1] 并行
- [Task 14] 贯穿所有任务，应在各窗口实现时同步完成
- [Task 15] 可立即开始，不依赖其他任务

# Remaining Items (Phase 2)

- Task 4: 销售编辑弹窗还原（w_sell_edit）— 需要完整的模态对话框，含客户查询/历史价格/零售批发切换
- Task 6: 采购编辑弹窗还原（w_buy_edit）— 需要完整的Freeform布局，含DDDW下拉联动/配件选择/图片上传
- SubTask 3.5: 销售开单顶部模块导航按钮栏
- SubTask 3.6-3.7: 销售退货模式 + 配件查询条件区拼音搜索
- SubTask 5.6: 采购退货模式
- SubTask 14.3: DDDW下拉数据窗口模式
- SubTask 14.6: 配件图片显示
