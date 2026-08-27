# 百分百还原老项目UI验收检查清单

## MDI主框架
- [x] MainWindow为MDI框架，子窗口在Tab/Docking面板内打开（非独立弹出）
- [x] 菜单栏10个顶级菜单与PB m_main完全一致
- [x] 菜单"进销存管理"子菜单包含：采购进货/采购查询/采购退货/计划订货/销售开单/销售查询/快捷开单/销售退货/查看库存/仓库盘点/库存预警/单据打印发送/报损出库/借货出库/还货入库
- [x] 菜单"财务管理"子菜单包含：应付款/应收款/收款/现金账/银行账/支付宝账/微信账/运费账/日结账
- [x] 菜单"业务查询"子菜单包含：采购明细/计划明细/销售明细/销售汇总/进销存报表/营业报表/销售排行/客户排行/职员排行
- [x] 菜单"基础数据"子菜单包含：客户管理/供应商管理/员工管理/配件管理/品牌管理/物流公司/库位管理
- [x] 工具栏F1-F12按钮与当前活动子窗口联动（F1=新增/F2=编辑/F3=查询/F4=删除/F5=保存/F6=结算/F7=打印/F8=退货/F9=取消/F12=关闭）
- [x] 状态栏显示：操作员/权限组/连接状态图标/服务器状态/日期时间
- [x] 菜单权限控制：根据mnu表auth字段控制菜单项可见性/启用状态（PermissionService + ApplyMenuPermissions递归遍历）

## 桌面快捷方式导航
- [x] 左侧6组GroupBox共24个快捷按钮（进销存/销售/仓库/查询/财务/高级功能）
- [x] 右侧DataGrid显示desktop表数据，双击打开功能
- [x] 右键菜单：编辑/删除/刷新/按名称排序/按时间排序
- [x] 登录后自动打开桌面导航窗口作为首个Tab页

## 销售开单窗口
- [x] 双模式切换：开单模式（dw_query+dw_part+dw_detail+dw_bill+图片）↔ 查询模式（dw_bill_list→dw_bill+dw_detail）
- [x] 开单模式：查询条件区→配件列表→双击选中→弹出SellEditDialog→写入明细
- [x] 查询模式：单据列表点击行自动加载单头+明细
- [x] 工具栏F1-F12映射正确
- [x] 顶部模块导航按钮栏完整（17个导航按钮）
- [x] 退货模式：F8切换退货模式，红色指示器，数量为负，flag=2，库存回补
- [x] 配件查询条件区支持拼音搜索+匹配模式（精确/左匹配/右匹配/包含）

## 销售编辑弹窗
- [x] 模态对话框（CenterOwner, NoResize）
- [x] 左侧：客户查询+配件编号/名称+历史价格提示+车牌号/车型+数量+零售/批发切换+单价+开票单价+自动匹配
- [x] 右侧：销售历史列表（双击选价格）+采购历史列表（只读）
- [x] 零售=lsprice/批发=pfprice切换正确
- [x] 单价变更自动同步开票单价
- [x] 自动匹配客户历史价格功能

## 采购管理窗口
- [x] 左右布局：左侧dw_bill+dw_detail，右侧dw_bill_list
- [x] RadioButton状态切换：未结算(flag=0)/已结算(flag=1)/退货(flag=2)
- [x] Master-Detail联动：点击dw_bill_list行→自动加载dw_bill+dw_detail
- [x] 工具栏F1-F12映射正确
- [x] 式样新增/批量新增/采购转销售按钮
- [x] 退货模式：F8切换退货模式，红色指示器，退货数量可编辑，flag=2，库存扣减

## 采购编辑弹窗
- [x] 模态对话框
- [x] Freeform布局包含所有字段（partno/name/carname/cartype/unit/area/class/place/inprice/amount/lsprice/pfprice/memo/part_th/part_gg/part_cclb）
- [x] DDDW下拉联动：partno变更自动填充所有字段
- [x] 配件选择：选择按钮打开PartSelectorWindow
- [x] 搜索模式切换：精确/模糊/精确匹配
- [x] 配件图片显示和上传
- [x] 底部采购历史列表

## 账目管理窗口
- [x] Master-Detail布局：上方账目列表+下方明细列表
- [x] 明细自动切换：名称含"采购"→d_detail_buy_list，其他→d_detail_sell_list
- [x] rowfocuschanged联动正确
- [x] 工具栏按钮完整

## 应收应付款窗口
- [x] 左右Master-Detail布局：左侧客户列表+右侧欠款明细
- [x] 搜索框拼音+名称实时过滤
- [x] 按钮区完整（明细/收款/打印对账单/全选/确认到账/对账查询）
- [x] 收款逻辑正确（INSERT pays + account，更新charge）
- [x] 模式参数正确（1=应付款/2=应收款/3=代收款）

## 基础数据窗口
- [x] 弹出窗口（popup!风格）
- [x] RadioButton类别切换（配件/车型/品牌号/区域/单位）
- [x] 动态SQL（SELECT DISTINCT [字段] FROM part_data）
- [x] 可编辑Grid + 自动拼音生成 + 保存UPDATE part_data

## 报损管理窗口
- [x] 双模式切换（开单↔查询）
- [x] 开单模式：dw_query+dw_part+dw_detail+图片
- [x] 查询模式：dw_bill_list（flag=3的bill_sell记录）
- [x] 工具栏F1-F12映射正确
- [x] 报损结算：保存bill_sell flag=3 + detail_sell + 扣减库存

## 借货管理窗口
- [x] 左右Master-Detail布局
- [x] RadioButton状态切换（在借flag=3/归还flag=4）
- [x] 归还模式：dw_back替代dw_detail，编辑归还数量
- [x] 工具栏F1-F12映射正确

## 查询窗口
- [x] SellQueryWindow为独立查询窗口+查询条件弹窗
- [x] BuyQueryWindow为独立查询窗口+查询条件弹窗
- [x] 条件颜色：退货行红色（flag=2）
- [x] 汇总行：sum(stotal)/sum(btotal)/sum(amount)

## 登录窗口
- [x] 布局：顶部蓝色装饰+标题+左侧汽车图片+右侧操作员下拉+密码+确认/取消
- [x] 汽车图片定时切换（1.5秒）
- [x] 操作员下拉框从user_infor加载

## 通用交互模式
- [x] Enter→Tab跳转在所有编辑窗口生效
- [x] 拼音搜索在所有搜索框生效（name_py/cartype_py匹配）
- [x] DDDW下拉模式：BuyEditDialog中8个ComboBox可编辑+实时过滤+选择自动填充
- [x] 关闭保护：未保存修改时弹出确认对话框
- [x] resize自适应：窗口大小改变时DataGrid尺寸动态调整
- [x] 配件图片显示：SellControl选中配件时从part_data读取picture显示

## BUG修复
- [x] SellEditWindow保存使用UpdateAsync而非InsertBillAsync
- [x] BuyEditWindow保存使用UpdateAsync而非InsertBillAsync
- [x] 快捷键绑定有效（F5/F3/F9/Esc不再为{x:Null}）
- [x] SellQueryWindow/BuyQueryWindow双击查看详情已实现
- [x] 导出Excel功能已实现
