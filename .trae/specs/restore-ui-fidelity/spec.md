# 百分百还原老项目UI界面 Spec

## Why

当前WPF新项目与老PB11项目在UI布局、交互模式、导航结构上存在根本性差异。老项目使用PB特有的Master-Detail双模式切换（开单/查询）+ 工具栏快捷键驱动 + MDI多文档窗口架构，而新项目采用了独立弹出窗口 + 扁平列表 + 无MVVM的Code-Behind模式，导致操作流程、使用习惯、功能完整性均与老项目不一致。需要逐模块详细分析差距并100%还原。

## What Changes

- **BREAKING**: 重构MainWindow为MDI框架，内嵌子窗口而非独立弹出
- **BREAKING**: 重构销售/采购/报损/借还窗口为双模式切换（开单模式↔查询模式）
- **BREAKING**: 实现PB风格的Master-Detail联动（列表→单头→明细）
- **BREAKING**: 重构编辑窗口为response!（模态对话框）模式，传递结构体参数
- 新增桌面快捷方式导航窗口（w_desktop）
- 重构菜单结构为10个顶级菜单（与PB一致）
- 实现F1-F12快捷键完整映射
- 实现工具栏按钮与当前活动窗口联动
- 实现状态栏完整信息（操作员/权限/连接状态/日期时间）
- 修复SellEditWindow/BuyEditWindow保存使用Insert而非Update的严重BUG

## Impact

- Affected specs: refactor-to-modern-stack, analyze-datawindows
- Affected code: QP11.Wpf/Views下所有窗口, MainWindow核心架构
- 核心架构变更：从独立窗口弹出 → MDI内嵌Sheet窗口

## ADDED Requirements

### Requirement: MDI主框架还原（w_main → MainWindow）

系统 SHALL 将MainWindow重构为MDI多文档框架，与PB的w_main一致：

#### 窗口结构
- 顶部：菜单栏（10个顶级菜单）+ 工具栏（F1-F12快捷按钮）
- 中间：MDI客户区域（承载子窗口Sheet）
- 底部：状态栏（操作员/权限组/连接状态/日期时间/版本号）

#### 菜单结构（10个顶级菜单，与PB m_main完全一致）

| 序号 | 菜单文本 | 子菜单项 |
|------|----------|----------|
| 0 | 注册与登录 | QP号登记/商家注册/缴费续费/桌面导航/单据打印设置 |
| 1 | 进销存管理 | 采购进货(11)/采购查询(113)/采购退货(118)/计划订货(12)/销售开单(13)/销售查询(133)/快捷开单(136)/销售退货(138)/查看库存(15)/仓库盘点(151)/库存预警(156)/单据打印发送(16)/报损出库(17)/借货出库(18)/还货入库(19) |
| 2 | 财务管理 | 应付款(21)/应收款(22)/收款(23)/现金账(24)/银行账(25)/支付宝账(26)/微信账(27)/运费账(28)/日结账(29) |
| 3 | 业务查询 | 采购明细(31)/计划明细(32)/销售明细(33)/销售汇总(35)/进销存报表(36)/营业报表(37)/销售排行(38)/客户排行(39)/职员排行(3a) |
| 4 | 基础数据 | 客户管理(41)/供应商管理(42)/员工管理(43)/配件管理(44)/品牌管理(45)/物流公司(46)/库位管理 |
| 5 | 高级功能 | QP110连接(52)/断开QP110(53)/配件上传(55)/质量反馈(58)/短信群发(5c)/VIN查询(5e) |
| 6 | 传真系统 | 发送传真(61)/接收传真(62)（默认隐藏） |
| 7 | 系统管理 | 数据备份(71)/数据恢复(7d)/数据格式化(75)/数据导入导出(76)/操作员管理(72)/修改密码(77)/系统日志(73)/权限管理(78)/打印设置(7e)/系统参数设置/关于 |
| 8 | 会员管理 | 会员管理/会员卡充值 |
| 9 | 退出系统 | 直接退出 |

#### 工具栏按钮（与PB一致）

| 按钮 | 快捷键 | 功能 | 说明 |
|------|--------|------|------|
| 1 | F1 | 新增 | 对应当前活动窗口的ue_add |
| 2 | F2 | 编辑 | 对应当前活动窗口的ue_edit |
| 3 | F3 | 查询 | 对应当前活动窗口的ue_query |
| 4 | F4 | 删除 | 对应当前活动窗口的ue_delete |
| 5 | F5 | 保存 | 对应当前活动窗口的ue_save |
| 6 | F6 | 结算 | 对应当前活动窗口的ue_act |
| 7 | F7 | 打印 | 对应当前活动窗口的ue_print |
| 8 | F8 | 退货 | 对应当前活动窗口的ue_back |
| 9 | F9 | 取消 | 对应当前活动窗口的ue_cancel |
| 11 | F11 | 历史 | 对应当前活动窗口的ue_his_sell |
| 12 | F12 | 关闭 | 关闭当前活动子窗口 |

#### 状态栏（d_cm_status_bar）

| 区域 | 内容 | 对齐 |
|------|------|------|
| 左1 | 系统标题"汽配通汽车配件管理系统" | 左 |
| 左2 | 导航标题（当前模块名） | 左 |
| 中1 | 操作员：XXX | 中 |
| 中2 | 操作员权限：XXX | 中 |
| 右1 | 连接状态图标（link/unlink） | 右 |
| 右2 | 服务器状态文本 | 右 |
| 右3 | 系统日期时间 | 右 |

#### Scenario: MDI框架验证
- **WHEN** 用户启动系统并登录
- **THEN** 主窗口以最大化MDI形式显示，菜单栏10个顶级菜单，工具栏F1-F12按钮，状态栏显示完整信息
- **WHEN** 用户点击菜单"进销存管理→销售开单"
- **THEN** 销售开单窗口作为MDI子窗口(Sheet)在主窗口客户区域内打开

---

### Requirement: 桌面快捷方式导航窗口还原（w_desktop）

系统 SHALL 实现桌面快捷方式导航窗口，与PB的w_desktop一致：

#### 窗口布局
- **左侧区域**（约40%宽度）：6个GroupBox分组，每组2×2按钮
  - 进销存管理：采购进货/采购查询/计划订货/采购退货
  - 销售管理：销售开单/快捷开单/销售查询/销售退货
  - 仓库管理：查看库存/仓库盘点/库存预警/单据打印
  - 业务查询：采购明细/销售明细/进销存报表/营业报表
  - 财务管理：应付款/应收款/现金账/银行账
  - 高级功能：连接QP110/断开QP110/码片修改/VIN查询
- **右侧区域**（约60%宽度）：DataGrid显示桌面图标列表（d_desktop_bmp格式）
  - 列：图标(mnu_bmp)/名称(desktop_name)/编码(desktop_code)/时间(desktop_buildtime)
  - 双击打开对应功能
  - 右键菜单：编辑/删除/刷新/按名称排序/按时间排序
- **底部区域**：WebBrowser控件（可选，显示qp110网页）

#### 按钮导航映射
每个按钮通过Tag值递归查找菜单树中匹配的菜单项，触发其clicked事件，与菜单导航完全一致。

#### 数据源
desktop表：code(PK)/name/buildtime/memo/username(PK)，按当前登录用户筛选。

#### Scenario: 桌面导航验证
- **WHEN** 用户登录后系统自动打开桌面导航窗口
- **THEN** 左侧显示6组24个快捷按钮，右侧显示用户自定义的桌面图标列表
- **WHEN** 用户双击桌面图标或点击快捷按钮
- **THEN** 对应功能窗口作为MDI子窗口打开

---

### Requirement: 销售开单窗口还原（w_sell → SellWindow）

系统 SHALL 将销售相关功能整合为单一窗口的双模式切换，与PB的w_sell一致：

#### 窗口类型
MDI子窗口（Sheet），最大化显示

#### 双模式切换（ue_chg控制）

**模式A：销售开单模式（默认）**
- 布局从上到下：
  1. **dw_query**（查询条件区）：配件编号/名称/车型/备注/仓位/分类输入框，支持拼音搜索
  2. **dw_part**（配件库存列表）：d_part_list Grid，显示配件编号/名称/车型/库存/零售价/批发价等
  3. **dw_detail**（销售明细列表）：d_detail_sell_list Grid，显示已选配件明细
  4. **dw_bill**（单头信息）：d_bill_sell Freeform，单号/日期/客户(dddw)/业务员(dddw)/支票号/折扣率/总额/开票总额/现金/支票/欠款/支付宝/微信/备注
- 右侧：配件图片(p_1)
- 操作流程：dw_query输入条件 → dw_part筛选配件 → 双击/回车选中 → 弹出w_sell_edit编辑数量价格 → 写入dw_detail → dw_bill显示单头

**模式B：销售查询模式**
- 布局：
  1. **dw_bill_list**（已结算单据列表）：d_bill_sell_list Grid
  2. 点击/选中某行 → 自动加载dw_bill（单头）+ dw_detail（明细）
- 联动：dw_bill_list.rowfocuschanged → ue_view → 按sn检索dw_bill和dw_detail

#### 工具栏快捷键映射

| 快捷键 | 事件 | 功能 |
|--------|------|------|
| F1 | ue_add | 销售开单（从dw_part选中配件，弹出w_sell_edit） |
| F2 | ue_edit | 编辑修改（从dw_detail选中行，弹出w_sell_edit） |
| F3 | ue_query | 销售查询（弹出w_sell_pop查询条件窗口，切换到查询模式） |
| F4 | ue_delete | 删除整单 |
| F5 | ue_save | 确认保存（弹出w_sell_balance结算窗口） |
| F6 | ue_quick | 快速开单 |
| F7 | ue_print | 销售打印 |
| F8 | ue_back | 销售退货 |
| F9 | ue_cancel | 取消操作 |
| F11 | ue_his_sell | 销售历史 |
| F12 | ue_close | 关闭 |

#### 顶部快捷按钮栏
与PB一致：采购管理/计划管理/采购退货/采购查询/销售开单/快速开单/销售退货/销售查询/预售价单/仓位查询/仓库盘点/库存预警/报损管理/借货管理/归还管理/计算器/记事本/锁屏

#### 退货模式
- dw_back（d_detail_sell_list，标题改为"退货数量"）替代dw_detail
- 退货数量(amount2)可编辑，不能超过原销售数量

#### Scenario: 销售开单验证
- **WHEN** 用户在开单模式双击dw_part中的配件
- **THEN** 弹出w_sell_edit模态对话框，显示配件编号/名称/数量/单价/开票单价/车牌号/车型/零售/批发切换
- **WHEN** 用户在w_sell_edit确认后
- **THEN** 配件明细写入dw_detail，dw_bill自动更新总额
- **WHEN** 用户按F5保存
- **THEN** 弹出w_sell_balance结算窗口，确认后保存bill_sell+detail_sell+更新库存+更新账户+更新欠款

---

### Requirement: 销售编辑弹窗还原（w_sell_edit → SellEditDialog）

系统 SHALL 实现销售编辑为模态对话框（response!），与PB的w_sell_edit一致：

#### 窗口类型
模态对话框（WindowStartupLocation=CenterOwner, ResizeMode=NoResize）

#### 布局
- **左侧区域**（gb_1分组框）：
  - dw_4：客户查询输入框（带下拉过滤）
  - 配件编号显示（st_10）
  - 配件名称显示（st_11）
  - 历史价格提示（st_4，动态显示/隐藏）
  - dw_1：d_sell_edit Freeform（车牌号car_mark/车型cartype输入）
  - 销售数量输入（em_amount，掩码######）
  - 零售/批发切换（rb_1/rb_2）
  - 销售单价（em_price）
  - 开票单价（em_bill_price，自动同步em_price）
  - 自动匹配客户历史价格（cbx_1）
- **右侧区域**：
  - dw_3：销售历史列表（d_sell_his / d_sell_his_mf），双击选择历史价格
  - dw_2：采购历史列表（d_buy_his），只读参考
- **底部按钮**：确认[&O] / 取消[&C] / 历史售价

#### 数据传递
- 输入：st_sell_edit结构体（partid, amount, price, bill_price, car_mark, cartype, custname, custcode）
- 输出：修改后的st_sell_edit结构体（flag=1确认）

#### 价格逻辑
- 零售：price = lsprice（零售价）
- 批发：price = pfprice（批发价）
- em_price变更时自动同步em_bill_price
- 自动匹配：cbx_1勾选时，根据客户+配件从detail_sell查最后一次价格自动填入

#### Scenario: 销售编辑验证
- **WHEN** 用户在w_sell中F1新增或F2编辑
- **THEN** 弹出w_sell_edit模态对话框，预填配件信息和当前零售价
- **WHEN** 用户切换零售/批发
- **THEN** 单价自动切换为零售价/批发价
- **WHEN** 用户双击销售历史列表中的某行
- **THEN** 自动填入该历史记录的单价和开票单价

---

### Requirement: 采购管理窗口还原（w_buy → BuyWindow）

系统 SHALL 将采购相关功能整合为单一窗口，与PB的w_buy一致：

#### 窗口类型
MDI子窗口（Sheet），最大化显示

#### 布局
- **左侧**：dw_bill（d_bill_buy Freeform单头）+ dw_detail/dw_back（明细）
- **右侧**：dw_bill_list（d_bill_buy_list单据列表）

#### 状态切换（RadioButton）
- rb_1：未结算（flag=0，默认选中）
- rb_2：已结算（flag=1，启用退货按钮）
- rb_3：退货（flag=2）

点击任一RadioButton触发ue_ref刷新列表。

#### Master-Detail联动
- 点击dw_bill_list某行 → ue_view → 按sn检索dw_bill和dw_detail
- dw_bill_list.rowfocuschanged自动触发ue_view

#### 工具栏快捷键映射

| 快捷键 | 事件 | 功能 |
|--------|------|------|
| F1 | ue_add8 | 式样新增（先选供应商，弹出w_buy_edit） |
| F2 | ue_edit | 编辑修改（弹出w_buy_edit4） |
| F3 | ue_query | 采购查询（弹出w_buy_pop） |
| F4 | ue_del | 删除明细行 |
| F5 | ue_save | 保存 |
| F6 | ue_act | 结算（弹出w_buy_balance） |
| F7 | ue_print | 打印 |
| F8 | ue_back | 退货 |
| F9 | ue_cancel | 取消 |
| F12 | ue_close | 关闭 |

#### 特殊按钮
- cb_1：式样新增（ue_add8）
- cb_3：批量新增（ue_add9，打开w_buy_edit_pl）
- cb_2：采购单转销售单（打开w_sell_jhxs）

#### 退货模式
- dw_back（d_detail_buy_back）替代dw_detail
- 退货数量(amount2)可编辑，不能超过库存和原采购数量
- 保存时flag设为2

#### Scenario: 采购管理验证
- **WHEN** 用户点击rb_1"未结算"
- **THEN** dw_bill_list显示所有flag=0的采购单
- **WHEN** 用户点击dw_bill_list中的某行
- **THEN** dw_bill显示该单的单头信息，dw_detail显示该单的明细
- **WHEN** 用户按F1新增
- **THEN** 检查供应商是否已填写，弹出w_buy_edit模态对话框

---

### Requirement: 采购编辑弹窗还原（w_buy_edit → BuyEditDialog）

系统 SHALL 实现采购编辑为模态对话框，与PB的w_buy_edit一致：

#### 布局
- **主编辑区**（gb_2分组框）：
  - dw_1：d_buy_edit Freeform，字段：partno(配件编号,dddw)/name(名称,dddw)/carname(车系,dddw)/cartype(车型,dddw)/unit(单位,dddw)/area(区域,dddw)/class(分类,dddw)/place(仓位,dddw)/inprice(进价)/amount(数量)/lsprice(零售价)/pfprice(批发价)/memo(备注)/part_th(图号)/part_gg(规格)/part_cclb(出厂类别)
  - 搜索模式：精确(rb_1)/模糊(rb_2)/精确匹配(rb_3)
- **右侧**：配件图片(p_1) + 上传图片按钮(cb_4)
- **底部**：dw_2采购历史列表（d_buy_his）
- **底部按钮**：选择(cb_3,打开w_part_choose)/确认[&O](cb_1)/取消[&C](cb_2)/更新库存复选框(cbx_1)

#### DDDW下拉联动
- partno变更时：从part_data+part_stock查询自动填充name/unit/carname/cartype/area/class/place/inprice/lsprice/pfprice
- 双击partno/name列：打开w_part_choose选择配件
- 双击其他下拉列：打开对应的选择窗口

#### Scenario: 采购编辑验证
- **WHEN** 用户在dw_1中输入配件编号
- **THEN** 自动从part_data查询匹配配件，填充所有字段
- **WHEN** 用户双击partno列
- **THEN** 弹出w_part_choose配件选择器

---

### Requirement: 账目管理窗口还原（w_account → AccountWindow）

系统 SHALL 重构账目管理窗口为Master-Detail模式，与PB的w_account一致：

#### 布局
- **上方**：dw_1（d_account Grid）— 账目主列表（只读）
- **下方**：dw_3 — 明细列表（根据主表行自动切换）
  - 名称含"采购"/"应付款" → dataobject = d_detail_buy_list
  - 其他 → dataobject = d_detail_sell_list
- **工具栏**：F1新增/F3删除/F6刷新/F7打印/F12关闭 + 编辑/查询/导出Excel

#### Master-Detail联动
- dw_1.rowfocuschanged → ue_dw2_vie → 根据当前行的sn查询采购/销售明细

#### CRUD流程
- 新增/编辑：弹出w_pop_account（账户编辑弹窗）
- 删除：确认后DELETE FROM account WHERE id=?
- 查询：弹出w_account_query按日期范围+类型筛选

#### Scenario: 账目管理验证
- **WHEN** 用户点击dw_1中的某行
- **THEN** dw_3自动切换为对应的采购/销售明细列表，显示该账户关联的单据明细

---

### Requirement: 应收应付款窗口还原（w_arrear → ArrearageWindow）

系统 SHALL 重构应收应付款窗口为Master-Detail模式，与PB的w_arrear一致：

#### 布局
- **左侧**：dw_1（d_arrearage Grid）— 客户欠款列表（只读，支持分栏滚动）
- **右侧**：dw_2（d_arrearage_det12）— 欠款明细（可编辑金额）
- **顶部**：搜索框(sle_1)，支持拼音(name_py)和名称(name)模糊匹配

#### 按钮区
- 明细(cb_1)：打开w_arrear_detail
- 收款(cb_2)：遍历dw_2选中项，INSERT INTO pays + account
- 打印对账单(cb_3)：复制数据到dw_3，打开打印预览
- 全选(cb_4)：所有行isedit=1, money=pay
- 确认到账(cb_5)：INSERT INTO pays(bz=1)
- 对账查询(cb_7)：打开w_arrear_dz_query

#### Master-Detail联动
- dw_1.clicked → dw_2.retrieve(bid, flag, btype, rq1, rq2)
- 默认日期范围：2000-01-01至当天

#### 模式参数
- ls_flag=1：应付款
- ls_flag=2：应收款
- ls_flag=3：代收款（money字段displayonly）

#### Scenario: 应收应付款验证
- **WHEN** 用户点击左侧客户列表中的某行
- **THEN** 右侧显示该客户的欠款明细，可编辑收款金额
- **WHEN** 用户点击"收款"按钮
- **THEN** 选中项写入pays表和account表，更新charge

---

### Requirement: 基础数据窗口还原（w_basic → BasicDataWindow）

系统 SHALL 实现基础数据窗口为popup!弹出窗口，与PB的w_basic一致：

#### 布局
- **RadioButton类别切换**：
  - rb_1：配件（name字段）
  - rb_2：车型（cartype字段）
  - rb_3：品牌号（carname字段）
  - rb_4：区域（area字段）
  - rb_5：单位（unit字段）
- **搜索框**（sle_1）：实时过滤，支持拼音和名称
- **dw_1**（d_basic_list Grid）：基础数据列表（可编辑）
- **工具栏**：F5刷新/F6保存/F12关闭

#### CRUD流程
- 新增：dw_1.insertrow(0) → 聚焦name列
- 编辑：dw_1.editchanged → 自动生成拼音(name_py) → 标记isedit=1
- 删除：遍历isedit=1的行 → dw_1.deleterow
- 保存：遍历isedit=1的行 → 动态构建UPDATE SQL更新part_data表
- 双击：打开w_basic_edit

#### 查询逻辑
- 每次切换RadioButton：动态构建 `SELECT DISTINCT [字段] FROM part_data`
- sle_1实时过滤：WHERE name LIKE '%keyword%' OR name_py LIKE '%keyword%'

#### Scenario: 基础数据验证
- **WHEN** 用户点击rb_2"车型"
- **THEN** dw_1显示SELECT DISTINCT cartype FROM part_data的结果
- **WHEN** 用户在搜索框输入"大"
- **THEN** dw_1实时过滤显示含"大"的车型记录

---

### Requirement: 报损管理窗口还原（w_baosun → BaosunWindow）

系统 SHALL 重构报损管理窗口为双模式切换，与PB的w_baosun一致：

#### 双模式切换
- **开单模式**：dw_query + dw_part + dw_detail + p_1可见，dw_bill_list隐藏
- **查询模式**：dw_bill_list可见，dw_query + dw_part + p_1隐藏

#### DataWindow控件
- dw_part：d_part_list（配件库存列表）
- dw_query：d_place_sell_query（查询条件）
- dw_detail：d_detail_baosun_list（报损明细）
- dw_back：d_detail_baosun_list（退货明细，可编辑amount2）
- dw_bill：d_bill_sell（单据头）
- dw_bill_list：d_bill_baosun_list（历史单据列表）

#### 工具栏快捷键
与w_sell一致：F1新增/F2编辑/F3查询/F4删除整单/F5保存/F6快速/F7打印/F8退货/F9取消/F11历史/F12关闭

#### 关键发现
报损单使用bill_sell表，flag=3标识报损记录。d_bill_baosun_list的SQL为：`SELECT * FROM bill_sell WHERE flag=3`

#### Scenario: 报损管理验证
- **WHEN** 用户在开单模式双击配件
- **THEN** 弹出w_baosun_edit编辑数量和价格
- **WHEN** 用户按F5保存
- **THEN** 弹出w_baosun_balance结算窗口，确认后保存bill_sell(flag=3)+detail_sell+扣减库存

---

### Requirement: 借货管理窗口还原（w_borrow → BorrowWindow）

系统 SHALL 重构借货管理窗口，与PB的w_borrow一致：

#### 布局
- **左侧**：dw_bill（d_bill_buy Freeform单头）+ dw_detail（d_detail_borrow_list明细）
- **右侧**：dw_bill_list（d_bill_borrow_list单据列表）

#### 状态切换（RadioButton）
- rb_1：在借（flag=3，默认选中）
- rb_3：归还（flag=4）

#### 工具栏快捷键
F1新增/F2编辑/F3查询/F4删除明细/F5保存/F6结算/F7打印/F8归还/F9取消/F12关闭

#### 归还模式
- 选择已结算单据(flag=1) → 加载明细到dw_back → 编辑归还数量 → 保存

#### Scenario: 借货管理验证
- **WHEN** 用户按F1新增
- **THEN** 先输入供应商，弹出w_borrow_edit选择配件
- **WHEN** 用户按F8归还
- **THEN** dw_back替代dw_detail，可编辑归还数量

---

### Requirement: 采购查询窗口还原（w_buy_query → BuyQueryWindow）

系统 SHALL 重构采购查询为独立查询窗口，与PB的w_buy_query一致：

#### 布局
- **dw_1**（d_buy_query Grid）：采购查询结果列表（只读）
- **工具栏**：F3查询/F7打印/F12关闭
- **顶部快捷按钮**：采购明细/计划明细/销售明细/订单明细/销售汇总/进销存/计算器/记事本/锁屏/营业报表/销售排行/客户排行/职员统计

#### 查询条件（w_pop_buy弹窗）
- 日期范围/单号/配件编号/配件名称/车型/车名/分类/供应商/业务员/单据状态

#### Scenario: 采购查询验证
- **WHEN** 用户按F3查询
- **THEN** 弹出w_pop_buy查询条件窗口，确认后dw_1显示查询结果

---

### Requirement: 销售查询窗口还原（w_sell_query → SellQueryWindow）

系统 SHALL 重构销售查询为独立查询窗口，与PB的w_sell_query一致：

#### 布局
- **dw_1**（d_sell_query Grid）：销售查询结果列表（只读）
- 条件颜色：flag=2退货行红色显示
- 汇总行：sum(stotal)/sum(btotal)/sum(amount)
- 默认排序：sn DESC
- 默认过滤：amount>0

#### 查询条件（w_sell_pop弹窗）
- 日期范围/单号范围/客户名称/业务员

#### Scenario: 销售查询验证
- **WHEN** 用户打开销售查询窗口
- **THEN** 自动弹出查询条件窗口，确认后显示结果，退货行红色，底部显示汇总

---

### Requirement: 登录窗口还原（w_check → LoginWindow）

系统 SHALL 重构登录窗口，与PB的w_check一致：

#### 布局
- 顶部装饰矩形（蓝色背景）+ 标题"汽配通汽车配件管理系统--登陆版"
- 左侧：汽车图片（定时切换car0-3.jpg）
- 右侧：操作员下拉框(ddlb_1) + 密码输入框(sle_password) + 确认/取消按钮
- 底部：版本号 + 初始操作员提示

#### 事件
- open：加载用户列表到下拉框，默认选中第一项
- timer(1.5秒)：循环切换汽车图片
- 确认：验证用户名密码(user_infor表)
- 取消：关闭窗口

#### Scenario: 登录验证
- **WHEN** 用户选择操作员并输入密码后点击确认
- **THEN** 验证user_infor表中的用户名密码，通过后进入主界面

---

### Requirement: 通用交互模式还原

系统 SHALL 还原以下PB通用交互模式：

#### 1. Enter→Tab跳转
PB使用keybd_event Win32 API实现Enter键自动跳转到下一个输入控件。WPF中使用KeyboardNavigation.TabNavigation和PreviewKeyDown事件实现。

#### 2. 拼音搜索
所有搜索框支持拼音首字母搜索（name_py/cartype_py字段），输入中文时自动匹配，输入拼音时匹配拼音码。

#### 3. DDDW下拉数据窗口
所有ComboBox下拉框使用DDDW模式：
- 可编辑（IsTextEditable=True）
- 输入时实时过滤下拉列表
- 双击打开完整选择窗口

#### 4. 权限控制
- 菜单项根据mnu表的auth字段控制可见性/启用状态
- 工具栏按钮根据当前操作权限控制启用状态
- 成本价显示需要权限"13a2"

#### 5. 关闭保护
带保存功能的窗口在closequery中检查未保存修改，弹出确认对话框。

#### 6. resize自适应
所有窗口均有resize事件，动态调整DataWindow/DataGrid尺寸。

#### 7. 图片显示
配件选择时从part_data读取picture字段显示在右侧图片控件中。

## MODIFIED Requirements

### Requirement: 修复SellEditWindow/BuyEditWindow保存BUG

当前SellEditWindow.BtnSave_Click和BuyEditWindow.BtnSave_Click使用InsertBillAsync而非UpdateAsync，导致编辑保存时可能重复插入数据。必须修改为：
- 新单据：InsertBillAsync
- 已有单据：UpdateBillAsync

### Requirement: 修复快捷键绑定无效

当前MainWindow的InputBindings中Command绑定为{x:Null}，F5/F3/Esc等快捷键完全无效。必须修改为：
- F5：对应当前活动窗口的SaveCommand
- F3：对应当前活动窗口的AddCommand
- F9：对应当前活动窗口的RefreshCommand
- Esc：关闭当前活动子窗口

## REMOVED Requirements

### Requirement: 独立弹出窗口模式
**Reason**: PB使用MDI Sheet模式，所有业务窗口在主窗口客户区域内打开，而非独立弹出。独立弹出窗口导致窗口管理混乱、工具栏无法联动、快捷键无法传递。
**Migration**: 所有业务窗口改为MDI子窗口（Tab或Docking面板内嵌），通过MainWindow统一管理。

### Requirement: 扁平列表查询模式
**Reason**: PB的查询窗口使用Master-Detail联动（单据列表→单头→明细），而非扁平的单层列表。扁平列表无法查看单据完整信息。
**Migration**: 所有查询窗口改为Master-Detail布局，上方/左方为单据列表，下方/右方为单头+明细。
