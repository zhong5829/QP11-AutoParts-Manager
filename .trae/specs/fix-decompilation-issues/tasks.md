# Tasks

- [x] Task 1: 修复主应用对象 qpxt.sra 的反编译问题
  - [x] SubTask 1.1: 修复 global variables 中的 GBK 乱码字符串（qtitle[], ysf[], tpf_l[], tpf_r[], shome, publ_wjts, publ_bjts, publ_dhts, gs_noauth, gs_daohang_title 等）
  - [x] SubTask 1.2: 修复 open 事件中的乱码字符串（toolbarframetitle, toolbarpopmenutext, messagebox 提示文字等）
  - [x] SubTask 1.3: 修复 open 事件中 EXIT 后紧跟 CONTINUE 的逻辑错误
  - [x] SubTask 1.4: 修复 systemerror 事件中的乱码字符串
  - [x] SubTask 1.5: 清理反编译器伪注释（如 `//string commandline`）

- [x] Task 2: 修复 base 目录下的文件反编译问题
  - [x] SubTask 2.1: 修复 n_base.sru 中的 SHU_ERROR:2.0070_FLAG 标记
  - [x] SubTask 2.2: 修复 m_main.srm 中的菜单项名称乱码和 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 2.3: 修复 m_main_150322.srm 中的同类问题
  - [x] SubTask 2.4: 修复 gf_dwcolumnwidth.srf 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 2.5: 修复 w_print_preview_sell.srw 及其变体文件中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 2.6: 修复 w_error_trap.srw 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 2.7: 修复 u_treeview.sru 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 2.8: 修复 nvo_db_update.sru 中的 DEMO_SCRIPT_LIMIT 截断和 LABEL_KENSHU 标签
  - [x] SubTask 2.9: 清理 w_base.srw 中的反编译器伪注释

- [x] Task 3: 修复 qpxt 目录下的文件反编译问题
  - [x] SubTask 3.1: 修复 w_main.srw 中的 DEMO_SCRIPT_PASSWORD_LIMIT、乱码字符串和 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 3.2: 修复 w_check.srw 中的乱码字符串
  - [x] SubTask 3.3: 修复 n_coolmenu.sru 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 3.4: 修复 n_share.sru 和 n_base.sru 中的 SHU_ERROR 标记

- [x] Task 4: 修复 class 目录下的文件反编译问题
  - [x] SubTask 4.1: 修复 n_qyqms_connectservice.sru 中的 DEMO_SCRIPT_LIMIT 截断、LABEL_KENSHU 标签和逻辑错误（autocommit 分支）

- [x] Task 5: 修复 comclass 目录下的文件反编译问题
  - [x] SubTask 5.1: 修复 u_dw_graph.sru 中的 9 处 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 5.2: 修复 u_button.sru 中的 2 处 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 5.3: 修复 n_cst_fileinfo.sru 中的 5 处 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 5.4: 修复 n_printer.sru 中的 1 处 DEMO_SCRIPT_LIMIT 截断和 13 处 LABEL_KENSHU 标签

- [x] Task 6: 修复 comfunction 目录下的文件反编译问题
  - [x] SubTask 6.1: 修复 f_checkdwvalid.srf 和 f_checkdwvalid_gd.srf 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 6.2: 修复 f_auto_ins_head.srf 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 6.3: 修复 f_geterrorinfo.srf 和 f_get_sn.srf 中的 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 6.4: 修复 f_setdropdown.srf 中的 DEMO_SCRIPT_LIMIT 截断

- [x] Task 7: 修复 comstruction 目录下的文件反编译问题
  - [x] SubTask 7.1: 修复 st_account.srs 中的 SHU_ERROR:2.0070_FLAG 标记

- [x] Task 8: 修复 comwindow 目录下的文件反编译问题（约 60+ 个文件含 SHU_ERROR）
  - [x] SubTask 8.1: 修复 w_xl_ksxc*.srw 系列文件（6个文件，共18处 SHU_ERROR）
  - [x] SubTask 8.2: 修复 w_sell_balance*.srw 和 w_sell_edit.srw 系列文件（共13处 SHU_ERROR）
  - [x] SubTask 8.3: 修复 w_data_excel*.srw 系列文件（共13处 SHU_ERROR）
  - [x] SubTask 8.4: 修复 w_xc_jcwx*.srw 系列文件（共13处 SHU_ERROR）
  - [x] SubTask 8.5: 修复 w_db_jcwx*.srw 系列文件（共10处 SHU_ERROR）
  - [x] SubTask 8.6: 修复其余 comwindow 目录下的 SHU_ERROR 文件

- [x] Task 9: 修复 dw2xls 目录下的文件反编译问题
  - [x] SubTask 9.1: 修复 n_cst_dw2excel.sru 中的 12 处 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 9.2: 修复 n_xls_worksheet.sru 中的 5 处 DEMO_SCRIPT_LIMIT 截断
  - [x] SubTask 9.3: 修复 n_xls_format.sru, n_xls_cell.sru, n_cst_sst.sru 中的截断

- [x] Task 10: 修复 toolbar 目录下的文件反编译问题
  - [x] SubTask 10.1: 修复 w_updown.srw 中的 2 处 DEMO_SCRIPT_LIMIT 和 14 处 LABEL_KENSHU
  - [x] SubTask 10.2: 修复 w_updoen1.srw 中的 1 处 DEMO_SCRIPT_LIMIT 和 12 处 LABEL_KENSHU
  - [x] SubTask 10.3: 修复 n_coolmenu.sru, n_ras_pro.sru, n_share.sru, n_base.sru 中的 SHU_ERROR 标记

- [x] Task 11: 修复 function 目录下的文件反编译问题
  - [x] SubTask 11.1: 修复 f_sqlqd.srf 和 f_savetoexcel.srf 中的 DEMO_SCRIPT_LIMIT 截断

- [x] Task 12: 修复 menu 目录下的文件反编译问题
  - [x] SubTask 12.1: 修复 m_sell_13.srm, m_jhdh.srm, m_baosun.srm 中的 DEMO_SCRIPT_LIMIT 截断

- [x] Task 13: 修复 windows 目录下的文件反编译问题
  - [x] SubTask 13.1: 修复 w_reg.srw 中的 DEMO_SCRIPT_PASSWORD_LIMIT
  - [x] SubTask 13.2: 修复 w_sj.srw 中的 12 处 LABEL_KENSHU 标签
  - [x] SubTask 13.3: 修复 windows 目录下所有文件的乱码字符串

- [x] Task 14: 全局清理反编译器伪代码
  - [x] SubTask 14.1: 移除所有文件中的 `$PBExportComments$Export By Shu<KenShu@163.net>` 行
  - [x] SubTask 14.2: 移除所有函数/事件体中的冗余参数注释（如 `//string as_xxx`）
  - [x] SubTask 14.3: 移除所有反编译器签名注释（如 `//close (none) returns (none)`）

- [x] Task 15: 生成项目分析报告
  - [x] SubTask 15.1: 整理项目架构和模块划分
  - [x] SubTask 15.2: 整理业务域和功能模块
  - [x] SubTask 15.3: 整理外部依赖（DLL、数据库连接等）
  - [x] SubTask 15.4: 整理数据模型和表结构

# Task Dependencies

- [Task 1] 是基础任务，应优先完成，因为 qpxt.sra 是应用入口
- [Task 2] 依赖 [Task 1]，base 目录包含基础类
- [Task 3] 依赖 [Task 2]，qpxt 目录依赖 base 目录
- [Task 4-12] 相互独立，可以并行处理
- [Task 13] 依赖 [Task 3]，windows 目录下的窗口引用 qpxt 目录的对象
- [Task 14] 应在所有修复任务完成后执行，作为全局清理
- [Task 15] 应在所有修复任务完成后执行，基于修复后的代码进行分析
