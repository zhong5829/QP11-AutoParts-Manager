# DataWindow源文件深度分析 Spec

## Why

`comdatawindow`（~100个.srd文件）和`datawindow`（~250个.srd文件）包含了原PB11系统所有DataWindow的完整定义——SQL查询、列映射、UI布局、下拉数据窗口引用、条件格式、计算字段等。这些文件是理解原系统业务逻辑和UI还原的**最权威数据源**，比PBL反编译代码更精确。需要系统分析这些文件，提取关键信息，与当前WPF实现进行差距对比。

## What Changes

- 对350+个.srd文件进行分类编目（按业务模块分组）
- 提取所有SQL查询语句，确认数据库表结构、JOIN关系、WHERE条件
- 提取所有dddw（下拉数据窗口）引用关系，确认关联字段
- 提取UI布局信息（列可见性、顺序、宽度、格式、条件颜色）
- 提取计算字段和汇总逻辑
- 与当前WPF实现进行差距分析，识别遗漏功能

## Impact

- Affected specs: refactor-to-modern-stack
- Affected code: QP11.Core/Entities, QP11.Data/Repositories, QP11.Wpf/Views
- 关键发现：部分数据库表和字段在当前WPF实体中缺失

## ADDED Requirements

### Requirement: DataWindow文件分类编目

系统 SHALL 将350+个.srd文件按业务模块分类：

#### comdatawindow（公共/共享DataWindow，~100个文件）

**销售模块（15个）**:
- d_sell_query — 销售明细查询（detail_sell JOIN bill_sell JOIN client_infor JOIN work_infor）
- d_sell_his, d_sell_his_1, d_sell_his_11, d_sell_his_mf, d_sell_his_x1 — 销售历史（多种筛选维度）
- d_sell_pop — 销售弹窗选择
- d_bill_sell_tot — 销售单汇总
- d_detail_sell_list — 销售明细列表
- d_detail_sell_his_danhao — 按单号查销售历史
- d_detail_sell_jhxs — 进货销售对比
- d_detail_sell_tot — 销售明细汇总

**采购模块（15个）**:
- d_buy_query — 采购明细查询（detail_buy JOIN bill_buy JOIN supplier_infor JOIN work_infor）
- d_buy_his, d_buy_his1, d_buy_his_1, d_buy_his_x, d_buy_his_1_x — 采购历史
- d_buy_edit, d_buy_edit_pl, d_buy_edit_pl_cclb, d_buy_edit_pl_part — 采购编辑
- d_buy_pop — 采购弹窗选择
- d_buy_sell_query — 进销对比查询

**库存/配件模块（15个）**:
- d_part_list — 配件库存列表（part_stock JOIN part_data，含sell_use/name_py/cartype_py/part_bzq/part_bzrq）
- d_part_data_save — 配件数据保存
- d_part_stock_excel — 库存Excel导出
- d_part_data_excel — 配件Excel导出
- d_place_edit, d_place_edit2 — 库位编辑
- d_place_list — 库位列表
- d_place_part_cf, d_place_part_cfd — 库位配件拆分
- d_place_part_hb, d_place_part_hbd, d_place_part_hbd1 — 库位配件合并
- d_place_pd — 库位盘点
- d_place_query, d_place_query_pl — 库位查询
- d_place_sell_query — 库位销售查询

**客户/供应商模块（8个）**:
- d_client_excel — 客户Excel导出（client_infor全字段：name/jyfw/linkman/tel/fax/mobile/address/zip/bank/tax/note）
- d_supplier_excel — 供应商Excel导出（supplier_infor全字段：name/linkman/tel/fax/mobile/address/zip/credit/bank/tax/class/level/name_py）
- d_dddw_client, d_dddw_client_sell — 客户下拉
- d_dddw_supplier — 供应商下拉
- d_dddw_khmc_cp — 客户名称车牌下拉
- d_pop_client, d_pop_supplier — 客户/供应商弹窗

**财务模块（10个）**:
- d_arrearage, d_arrearage_det13, d_arrearage_det14 — 欠款明细
- d_arrearage_total — 欠款汇总
- d_bill_check — 单据核对
- d_pop_account, d_pop_account_yunfei — 账户弹窗（含运费）
- d_pop_arrear — 欠款弹窗

**下拉数据窗口（dddw，20+个）**:
- d_dddw_part_cclb — 配件出厂类型下拉
- d_dddw_part_classma — 配件分类下拉
- d_dddw_part_gg — 配件规格下拉
- d_dddw_part_th — 配件图号下拉
- d_dddw_peijian_class — 配件类别下拉
- d_dddw_place3 — 库位下拉
- d_dddw_wuliu — 物流下拉
- d_dddw_carname, d_dddw_cartype, d_dddw_unit — 车系/车型/单位下拉

**报表/统计模块（8个）**:
- d_tot_jxc — 进销存汇总
- d_tot_sell — 销售汇总
- d_top_client — 客户排行
- d_top_sell — 销售排行
- d_top_user — 用户排行
- d_bill_sell_tot — 销售单汇总
- d_car_mark_query — 车牌查询

**系统/工具模块（10个）**:
- d_qpt_desktop — 桌面快捷方式
- d_search_set, d_search_set_value, d_search_set_invalue, d_search_setcont — 搜索设置
- d_date — 日期选择
- d_cm_status_bar — 状态栏
- d_sys_log — 系统日志
- d_user_list — 用户列表
- d_worker_list — 员工列表
- d_wuliu_list, d_wuliu_query — 物流列表/查询
- d_print_tm — 条码打印
- d_tempdbf — 临时数据

**会员/借还模块（6个）**:
- xl_hygladd — 会员管理添加
- xl_hyglcx111 — 会员管理查询
- xl_jsdy1_fd ~ xl_jsdy5_fd — 结算单打印模板
- xl_jssk — 结算收款
- xl_kcz — 库存查
- xl_kcz_hyk — 会员卡库存查
- xl_part_list — 配件列表

**维修模块（已移除，5个）**:
- ksxc_jcwxlist — 快速修车检查列表
- xl_wxll_history — 维修历史
- dw_brow_hx_paper_size, dw_list_local_size — 打印纸张设置
- xl_ksxc_add — 快速修车添加

**其他（6个）**:
- d_dd_dwcol — 数据窗口列下拉
- d_detail_buy_back — 采购退货
- d_detail_buy_back_his1 — 采购退货历史
- d_detail_buy_list_his — 采购明细历史
- d_detail_jhdh_list — 进货到货明细
- d_ent_classes_set, d_ent_client_query, d_ent_edit_place, d_ent_place_query, d_ent_set_hide — 企业设置
- d_hyk_js_dh — 会员卡结算单号
- d_jhdh_query — 进货到货查询
- d_order_query — 订单查询
- d_part_jhdh — 配件进货到货
- d_updatefile — 更新文件
- d_wuliu_query — 物流查询
- reg1 — 注册

#### datawindow（主窗口DataWindow，~250个文件）

**销售开单/编辑（20+个）**:
- d_sell_edit — 销售编辑表单（Freeform，含cartype/car_mark输入）
- d_bill_sell — 销售主单（Freeform，sn/client/worker/datetime/total/bill_total/discount_rate/memo/flag/type，dddw:client/worker）
- d_bill_sell_list — 销售单列表
- d_detail_sell_list — 销售明细列表
- d_sell_print, d_sell_print_fp, d_sell_print_jb, d_sell_print_no — 销售打印（多种格式）
- d_sell_print_170622, d_sell_print_bak070622, d_sell_print_jb_170622 — 打印备份版本
- d_pop_sell, d_pop_sell1, d_pop_sell2 — 销售弹窗

**采购开单/编辑（20+个）**:
- d_buy_edit_150621, d_buy_edit_171216, d_buy_edit_bak, d_buy_edit_bak0522 — 采购编辑（多版本）
- d_bill_buy — 采购主单（Freeform，sn/supplier/worker/datetime/invoice/total/memo/cash/checks/arrear/zhifubao/weixin/yunfei/flag/type，dddw:supplier/worker）
- d_bill_buy_list, d_bill_buy_list9 — 采购单列表
- d_detail_buy_list — 采购明细列表
- d_detail_buy_back, d_detail_buy_back_his — 采购退货
- d_buy_balance — 采购结算
- d_buy_print — 采购打印
- d_pop_buy — 采购弹窗

**进货到货/订单（10+个）**:
- d_bill_jhdh — 进货到货主单（Freeform，sn/supplier/worker/operator/total/datetime/memo/flag，dddw:supplier/worker）
- d_bill_jhdh_list — 进货到货列表
- d_detail_jhdh — 进货到货明细
- d_detail_jhdh_list — 进货到货明细列表
- d_part_jhdh_b — 配件进货到货
- d_bill_order_list — 订单列表
- d_detail_order_list, d_detail_order_list_bk — 订单明细
- d_order_query — 订单查询
- d_dhbill, d_dhdata, d_dhdatafax1, d_dhtemp — 到货单/数据/传真

**配件数据（15+个）**:
- d_part_data — 配件数据（Freeform编辑）
- d_part_data_list, d_part_data_net — 配件数据列表
- d_part_stock_save — 库存保存
- d_part_up — 配件上传
- d_part_choose, d_partdata_choose — 配件选择
- d_part_ck — 配件出库
- d_part_list7 — 配件列表
- d_pop_part, d_pop_part_data_bak — 配件弹窗

**客户管理（5+个）**:
- d_client_list, d_client_list1 — 客户列表
- d_client_query, d_client_query1 — 客户查询
- d_pop_client — 客户弹窗

**供应商管理（5+个）**:
- d_supplier_list, d_supplier_list1 — 供应商列表
- d_supplier_query — 供应商查询
- d_pop_supplier, d_pop_supplier_jhjl, d_pop_supplier_jhjl_q — 供应商弹窗（含进货记录）

**财务/账户（5+个）**:
- d_account — 账户流水（Grid，含收入/支出/收款方式/操作员/余额计算）
- d_pay_list — 付款列表
- d_pop_account_bak171227 — 账户弹窗

**库存操作（15+个）**:
- d_data_bj, d_data_bj_kc — 报价数据（含库存）
- d_data_bsd, d_data_bsd_kc — 报损数据（含库存）
- d_data_ry, d_data_ry_dos, d_data_ry_kc, d_data_ry_kc_dos — 入库数据（含库存/DOS版）
- d_data_io1, d_data_io2 — 出入库数据
- d_pd — 盘点
- d_place_edit3 — 库位编辑
- d_place_list2, d_place_list_top — 库位列表
- d_place_sell_query1 — 库位销售查询

**借还管理（10+个）**:
- d_borrow_edit — 借用编辑
- d_bill_borrow_list — 借用单列表
- d_bill_lend_list, d_bill_lend_list1 — 借出列表
- d_detail_borrow_list, d_detail_borrow_back — 借用明细/归还
- d_detail_lend_list — 借出明细
- d_lend_check, d_lend_query — 借出审核/查询
- xl_gjgl, xl_gjgladd, xl_gjgladd1~3, xl_gjgljl — 工具管理

**会员管理（10+个）**:
- xl_hygladd1 — 会员添加
- xl_hyglcx1, xl_hyglcx11 — 会员查询
- xl_hygllist — 会员列表
- xl_hykc — 会员卡库存
- xl_klb, xl_klblist — 卡类别
- d_hykc, d_hykc1~3 — 会员卡库存查
- d_hykcfl — 会员卡分类
- d_hykcsj — 会员卡数据

**报损/报废（5+个）**:
- d_baosun_check — 报损审核
- d_bill_baosun_list — 报损单列表
- d_detail_baosun_list — 报损明细列表

**报表/统计（15+个）**:
- d_tot_jxc1 — 进销存汇总
- d_tot_buy — 采购汇总
- d_tot_sell — 销售汇总
- d_tot_part — 配件汇总
- d_top_client — 客户排行
- d_top_sell — 销售排行
- d_top_user — 用户排行
- d_top_work — 员工排行
- xlcx_bb1, xlcx_bb2, xlcx_bb6 — 报表
- d_car_mark_list, d_car_mark_query8 — 车牌列表/查询
- d_xsddata, d_xsddy, d_xsddy1, d_xsddy9 — 销售订单数据/打印
- d_xsd — 销售单

**维修模块（已移除，80+个）**:
- xl_jcda, xl_jcda1 — 接车档案
- xl_jcwxdy, xl_jcwxdy1~3 — 维修单打印
- xl_jcwxlist — 维修检查列表
- xl_wxg — 维修工
- xl_wxlb, xl_wxlblist — 维修类别
- xl_wxpgadd, xl_wxpgadd1 — 维修评估添加
- xl_wxpgdy, xl_wxpgdy1,3~5 — 维修评估打印
- xl_wxxmadd, xl_wxxmcllx, xl_wxxmcllx1 — 维修项目
- xl_wxxmcx, xl_wxxmcx1~2,9 — 维修项目查询
- xl_wxxmedit — 维修项目编辑
- xl_wxxmlb, xl_wxxmlb1 — 维修项目列表
- xl_wxxmmx, xl_wxxmmx1 — 维修项目明细
- xl_xmhs, xl_xmhs1 — 项目核算
- xl_xmhsdy, xl_xmhsdy1~3 — 项目核算打印
- xl_xmhspj, xl_xmhspj1~5 — 项目核算评价
- xl_xmhspjadd, xl_xmhspjadd1~3 — 项目核算评价添加
- xl_xmmxadd, xl_xmmxadd8 — 项目明细添加
- xl_bxgs, xl_bxgslist, xl_bxgslist1 — 保险管理
- xl_sbgs, xl_sbgslist, xl_sbgslist1 — 申报管理
- xl_bzadd, xl_bzlist, xl_bzlist1 — 标准添加/列表
- xl_lladd, xl_lljl, xl_lljl1, xl_lllist, xl_lllistedit, xl_llsave — 领料
- xl_pgmx, xl_pgmx1 — 评估明细
- xl_pjcx1 — 配件查询
- xl_jsdy, xl_jsdy1~4 — 结算单打印
- xl_jskmfxm, xl_jslist, xl_jsmfxm, xl_jspj, xl_jsxm — 结算
- xl_jzlist — 结账列表
- xl_klbmfgsf — 卡类别免费公式
- xl_klbmfxmlist, xl_klbmfxmlist1 — 卡类别免费项目
- xl_kmfxm, xl_kmfxm1 — 免费项目
- xl_wxlldy2~5 — 维修领料打印
- xl_toqpfax — 询价传真
- xlcx_ll, xlcx_pg, xlcx_pjmx, xlcx_pjmx1 — 查询
- xlcx_sy, xlcx_syy — 使用查询
- xlcx_wxd, xlcx_wxxm, xlcx_wxxm1 — 维修查询
- xlcx_ysk, xlcx_yybb — 应收款/月报表
- ksxc_jcwxlist — 快速修车

**系统/工具（15+个）**:
- d_desktop — 桌面快捷方式（Label格式，参数as_username，表desktop）
- d_basic, d_basic_list, d_basic_listaaaa — 基础数据
- d_mnu — 菜单
- d_edit_info — 编辑信息
- d_find — 查找
- d_query_public, d_query_infor — 公共查询
- d_quick_input, d_quick_temp — 快速输入
- d_select_area — 区域选择
- d_warning_edit, d_warning_query — 预警设置/查询
- d_business_choose, d_business_choose1, d_business_infor — 业务选择
- d_sys_log — 系统日志
- d_user_list — 用户列表
- d_worker_list1, d_worker_query — 员工列表/查询
- d_print_field — 打印字段
- d_dddw_field_choose, d_dddw_field_choose1 — 字段选择下拉
- d_dddw_price_choose — 价格选择下拉
- d_ent_updatebase_table, d_ent_set_hide — 企业设置
- reg, reg2, reg5 — 注册

**传真/物流（15+个）**:
- d_fax_add, d_fax_add1 — 传真添加
- d_fax_bill, d_fax_bill_lm — 传真单据
- d_fax_list, d_fax_list2~4, d_fax_list31, d_fax_listlm — 传真列表
- d_fax_print — 传真打印
- d_fax_temp — 传真模板
- d_dxsj — 询价
- d_lm, d_lmcj, d_lmcj1 — 联盟
- d_lmcy, d_lmcy1, d_lmcy2 — 联盟成员
- d_lmhxkc — 联盟库存

**下拉数据窗口（dddw，25+个）**:
- d_dddw_address — 地址下拉
- d_dddw_area — 区域下拉
- d_dddw_b_jc1, d_dddw_b_jc_b — 报价基础下拉
- d_dddw_b_type — 业务类型下拉
- d_dddw_b_username, d_dddw_b_username1, d_dddw_b_username_b — 用户名下拉
- d_dddw_car_mark — 车牌下拉
- d_dddw_carname — 车系下拉
- d_dddw_cartype — 车型下拉
- d_dddw_cdpm — 产地品牌下拉
- d_dddw_city — 城市下拉
- d_dddw_class1 — 分类下拉
- d_dddw_groups — 分组下拉
- d_dddw_jc — 基础下拉
- d_dddw_jyxm — 经营项目下拉
- d_dddw_name — 名称下拉
- d_dddw_partno — 配件编号下拉
- d_dddw_place, d_dddw_place2 — 库位下拉
- d_dddw_prov — 省份下拉
- d_dddw_supplier — 供应商下拉
- d_dddw_unit — 单位下拉
- d_dddw_worker — 员工下拉

**其他（10+个）**:
- a_mess, a_mess1, a_mess2 — 消息
- d_b_disp — B显示
- d_bj, d_bj2, d_bj4, d_bj6 — 报价
- d_bs, d_bs_edit — 报损
- d_shop, d_shop_down, d_shop_down_b — 商城
- d_suit, d_suit_in — 套装
- d_xxfk — 信息反馈
- xxfk2, xxfk3 — 信息反馈
- d_wjcx, d_wjxz, d_wjxz1, d_wjxz2 — 文件操作
- d_friend_tree — 朋友树
- fsdt, fsdt1, fstd — 分店
- ggc — 公共查询
- hykcpd — 会员卡盘点
- r_bj1, r_bj2, r_bj3 — 报价报表
- r_wj — 文件报表

### Requirement: SQL查询与数据库表结构提取

系统 SHALL 从所有.srd文件中提取SQL查询，确认以下关键信息：

#### 核心表结构发现

**detail_sell表**（销售明细）:
- sn, partid, partno, name, amount, amount2(退货数量), price(售价), bill_price(开票价)
- cartype, car_mark, memo, datetime, unit, stotal(售价总额), btotal(开票总额)
- id, tsn, type(是否急件:0普通/1急件), place, flag, cb(成本)
- part_th(图号), part_gg(规格), part_cclb(出厂类别)

**bill_sell表**（销售主单）:
- sn(PK), client, worker, operator, checkno(支票号)
- total(总额), bill_total(开票总额), discount_rate(折扣率)
- total_payment(实收总额), bill_payment(开票实收总额)
- cash(现金), checks(支票), arrear(欠款)
- datetime, memo, flag, type

**detail_buy表**（采购明细）:
- partno, name, amount, unit, carname, cartype
- inprice(进价), intotal(进货总额), pfprice(批发价), lsprice(零售价)
- place, memo, partid, sn, id, datetime, class, type

**bill_buy表**（采购主单）:
- sn(PK), supplier, worker, operator, invoice(发票号), total
- datetime, memo, cash, checks, arrear
- zhifubao(支付宝), weixin(微信), yunfei(运费), flag, type

**bill_jhdh表**（进货到货主单）:
- sn(PK), supplier, worker, operator, total, datetime, memo, flag

**part_stock表**（库存）:
- partid, place, amount, lsprice(零售价), pfprice(批发价), sell_use(销售次数)

**part_data表**（配件数据）:
- partid, partno, name, cartype, carname, unit, class, area, inprice, memo
- isck, part_th, part_gg, part_cclb, name_py, cartype_py, part_bzq(保质期), part_bzrq(保质日期)

**account表**（账户）:
- id(PK), name, sn, charge, flag(1=收入/0=支出), type, operator, memo, datetime, bz

**client_infor表**（客户）:
- cid, name, jyfw(经营范围), linkman, tel, fax, mobile, address, zip, bank, tax, note

**supplier_infor表**（供应商）:
- sid, name, linkman, tel, fax, mobile, address, zip, credit(信用额度), bank, tax, class, level, name_py

**desktop表**（桌面快捷方式）:
- code(PK), name, buildtime, memo, username(PK)

### Requirement: dddw（下拉数据窗口）引用关系

系统 SHALL 提取所有dddw引用，确认下拉数据来源：

| dddw名称 | 数据列 | 显示列 | 数据源表 |
|-----------|--------|--------|----------|
| d_dddw_client | cid | name | client_infor |
| d_dddw_client_sell | cid | name | client_infor |
| d_dddw_supplier | sid | name | supplier_infor |
| d_dddw_worker | workid | name | work_infor |
| d_dddw_carname | - | - | part_data(DISTINCT carname) |
| d_dddw_cartype | - | - | part_data(DISTINCT cartype) |
| d_dddw_unit | - | - | part_data(DISTINCT unit) |
| d_dddw_class1 | - | - | CLASSES(CLASS_TYPE='0001') |
| d_dddw_place | - | - | part_data(DISTINCT place) |
| d_dddw_area | - | - | area/part_data(DISTINCT area) |
| d_dddw_part_cclb | - | - | CLASSES(出厂类别) |
| d_dddw_part_th | - | - | part_data(DISTINCT part_th) |
| d_dddw_part_gg | - | - | part_data(DISTINCT part_gg) |
| d_dddw_car_mark | - | - | car_mark表 |
| d_dddw_groups | - | - | user_infor(groups) |

### Requirement: UI布局关键发现

系统 SHALL 从.srd文件中提取关键UI布局信息：

1. **d_sell_query（销售查询Grid）**:
   - 可见列：单号/配件编号/配件名称/客户名称/日期/售价/售价总额/开票总额/退货数量/单位/车型/业务员/车牌号/库位/是否急件/图号/规格/出厂类别/备注
   - 隐藏列：partid/id/tsn/flag/cb
   - 条件颜色：`if(flag=2, Rgb(255,0,0), 0)` — 退货行红色显示
   - 汇总：sum(stotal), sum(btotal), sum(amount)
   - 排序：sn D（单号降序）
   - 过滤：amount>0

2. **d_buy_query（采购查询Grid）**:
   - 可见列：单号/日期/配件编号/配件名称/单位/车系/车型/供应商/采购员/数量/进价/进货总额/批发价/零售价/库位/分类/备注/是否急件/状态
   - 隐藏列：partid/id
   - 状态值：进货1/退货2/未审核0
   - 汇总：sum(intotal)
   - 排序：sn A（单号升序）

3. **d_part_list（配件列表Grid）**:
   - 可见列：配件编号/配件名称/出厂类别/车型/车系/单位/分类/区域品牌/库存数量/批发价/零售价/规格/库位/图号/销售次数/保质期/保质日期/备注
   - 隐藏列：partid/inprice/isck/name_py/cartype_py
   - 条件颜色：`if(isck>0, rgb(0,0,255), 0)` — 停用配件蓝色显示
   - 排序：sell_use D（销售次数降序）
   - **关键发现**：part_stock表有sell_use字段（销售次数），part_data有part_bzq/part_bzrq字段

4. **d_bill_sell（销售主单Freeform）**:
   - 布局：标签+输入框垂直排列
   - 单号(displayonly)/日期(日历选择)/客户(dddw)/业务员(dddw)/支票号/总额(displayonly)/开票总额(displayonly)/备注
   - 隐藏字段：折扣率/实收总额/开票实收总额/现金/支票/欠款/操作员/flag/type

5. **d_bill_buy（采购主单Freeform）**:
   - 布局：标签+输入框垂直排列
   - 单号(displayonly)/日期(日历选择)/供应商(dddw)/采购员(dddw)/发票号/总额/备注
   - **关键发现**：bill_buy表有zhifubao(支付宝)/weixin(微信)/yunfei(运费)字段

6. **d_account（账户Grid）**:
   - SQL使用CASE WHEN：`case flag when 1 then charge else 0 end` as charge(收入), `case flag when 0 then charge else 0 end` as charge2(支出)
   - 汇总：sum(charge), sum(charge2), charge-charge2(余额)
   - 排序：datetime D, sn D

7. **d_desktop（桌面快捷方式Label）**:
   - 参数：as_username（按用户筛选）
   - 表：desktop(code, name, buildtime, memo, username)
   - 布局：Label格式，3行×2列

### Requirement: 与当前WPF实现的差距分析

系统 SHALL 识别以下差距：

#### 数据库字段缺失（当前Entity未映射）

1. **detail_sell表**:
   - amount2（退货数量）— 当前Entity可能缺失
   - cb（成本）— 当前Entity可能缺失
   - part_th/part_gg/part_cclb — 当前Entity可能缺失

2. **bill_sell表**:
   - discount_rate（折扣率）— 当前Entity可能缺失
   - total_payment/bill_payment（实收金额）— 当前Entity可能缺失
   - cash/checks/arrear（现金/支票/欠款）— 当前Entity可能缺失
   - checkno（支票号）— 当前Entity可能缺失

3. **bill_buy表**:
   - zhifubao/weixin/yunfei（支付宝/微信/运费）— 当前Entity可能缺失
   - invoice（发票号）— 当前Entity可能缺失

4. **part_stock表**:
   - sell_use（销售次数）— 当前Entity缺失

5. **part_data表**:
   - part_bzq/part_bzrq（保质期/保质日期）— 当前Entity缺失

6. **account表**:
   - bz（标志）— 当前Entity可能缺失

7. **desktop表**:
   - 整个表在当前WPF中未实现

#### 功能模块缺失

1. **进货到货管理**（bill_jhdh/detail_jhdh）— 当前WPF可能未实现
2. **报损管理**（baosun）— 当前WPF可能未实现
3. **报价管理**（d_bj系列）— 当前WPF可能未实现
4. **传真管理**（d_fax系列）— 当前WPF可能未实现
5. **商城管理**（d_shop系列）— 当前WPF可能未实现
6. **联盟管理**（d_lm系列）— 当前WPF可能未实现
7. **套装管理**（d_suit系列）— 当前WPF可能未实现
8. **桌面快捷方式**（desktop表）— 当前WPF可能未实现
9. **信息反馈**（d_xxfk系列）— 当前WPF可能未实现
10. **分店管理**（fsdt/fstd）— 当前WPF可能未实现
11. **公共查询**（d_query_public）— 当前WPF可能未实现
12. **快速输入**（d_quick_input）— 当前WPF可能未实现
13. **预警设置**（d_warning_edit/query）— 当前WPF可能未实现
14. **打印字段选择**（d_print_field/dddw_field_choose）— 当前WPF可能未实现

#### UI细节缺失

1. **条件颜色**：退货行红色(flag=2)、停用配件蓝色(isck>0) — 当前WPF可能未实现
2. **Grid汇总行**：sum(stotal)/sum(btotal)/sum(amount) — 当前WPF可能未实现
3. **排序默认值**：销售按单号降序、采购按单号升序、配件按销售次数降序
4. **过滤条件**：销售查询默认amount>0
5. **dddw下拉**：部分下拉数据窗口在当前WPF中用ComboBox+DISTINCT查询替代，但可能缺少CLASSES字典表的数据

## MODIFIED Requirements

### Requirement: 实体类补充

基于DataWindow分析发现，以下实体类需要补充字段：

1. **DetailSell** — 补充 amount2/cb/part_th/part_gg/part_cclb
2. **BillSell** — 补充 discount_rate/total_payment/bill_payment/cash/checks/arrear/checkno
3. **BillBuy** — 补充 zhifubao/weixin/yunfei/invoice
4. **PartStock** — 补充 sell_use
5. **PartData** — 补充 part_bzq/part_bzrq
6. **Account** — 补充 bz
7. 新增 **Desktop** 实体

## REMOVED Requirements

### Requirement: 维修模块DataWindow分析
**Reason**: 用户已明确移除维修相关功能
**Migration**: 维修模块80+个.srd文件仅作记录，不纳入WPF实现范围
