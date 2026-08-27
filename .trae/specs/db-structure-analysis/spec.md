# 数据库结构详细分析 Spec

## Why
用户已通过 VS 连接到 qipei 数据库（192.168.83.128:8829），需要将数据库中 95 张表的完整结构信息进行深度分析、整理分类，并输出为一份清晰可读的中文分析报告文件。

## What Changes
- 基于 `db_structure.txt` 中已有的原始表结构数据（从 SQL Server INFORMATION_SCHEMA 查询获取）
- 对 95 张表进行业务域分类和详细字段分析
- 输出一份完整的中文数据库分析报告文件 `db_analysis_report.md`
- 包含：表清单、字段详情、表间关系推断、业务域划分、数据字典

## Impact
- 仅生成分析报告文件，不修改任何源码
- 输入: `db_structure.txt`（已有）
- 输出: `db_analysis_report.md`（新建）

## ADDED Requirements
### Requirement: 数据库结构分析报告
系统 SHALL 生成一份完整的 qipei 数据库中文分析报告，包含以下内容：

#### Scenario: 报告完整性
- **WHEN** 分析脚本执行完成
- **THEN** 输出文件应包含：
  1. 数据库总览（表总数、业务领域概述）
  2. 按业务模块分组的全部 95 张表及其完整字段定义
  3. 核心表之间的关联关系分析（基于外键命名约定推断）
  4. 关键枚举字段的含义说明（flag/type/btype 等标志位）
  5. 数据字典（字段名到中文含义的映射）

### Requirement: 业务域分类
报告 SHALL 将 95 张表按以下业务域分组：
- 配件基础数据（part_* 系列）
- 进销存单据（bill_*/detail_* 系列）
- 客户供应商（client_infor/supplier_infor/work_infor 等）
- 财务管理（account/pays/arrearage/voucher 等）
- 维修管理（xl_* 系列）
- 连锁网络版扩展（down*/BILLFAX/qpfax/xsd 等）
- 系统管理（user_infor/sys_log/serialnumber 等）
- 其他辅助表

### Requirement: 字段级分析
每张表的字段分析 SHALL 包含：
- 字段名、数据类型、长度
- 是否允许空值、默认值
- 字段中文含义注释
- 主键/外键标识（基于命名约定推断）
