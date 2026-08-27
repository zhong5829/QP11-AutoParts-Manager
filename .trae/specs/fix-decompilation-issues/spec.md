# PowerBuilder 反编译源代码修复规范

## Why

该项目是使用 Shu 反编译器（KenShu@163.net）从 PowerBuilder 11 PBD 文件反编译出来的汽配通（QP110）汽配管理系统源代码。反编译器为演示版本，存在严重的脚本截断、编码损坏、密码遮蔽和重复定义等问题，导致大量文件无法直接重新导入 PowerBuilder 或正常编译运行。需要系统性地识别和修复这些问题，使源代码恢复可用状态。

## What Changes

- **修复编码问题**：将所有文件中的 GBK 乱码中文字符还原为正确的中文文本
- **清理反编译器标记**：移除 `SHU_ERROR:DEMO_SCRIPT_LIMIT.`、`SHU_ERROR:DEMO_SCRIPT_PASSWORD_LIMIT`、`SHU_ERROR:2.0070_FLAG` 等反编译器生成的错误标记
- **补全截断脚本**：对因演示版限制被截断的函数/事件脚本进行逻辑补全
- **清理反编译器伪代码**：移除冗余的注释参数声明（如 `//string commandline`）、反编译器签名注释（如 `//close (none) returns (none)`）、`LABEL_KENSHU` 标签等
- **修复逻辑错误**：修正反编译器引入的逻辑错误（如 EXIT 后紧跟 CONTINUE）
- **处理密码遮蔽**：恢复被 `SHU_ERROR:DEMO_SCRIPT_PASSWORD_LIMIT` 替换的密码字段

## Impact

- Affected specs: 全部 10 个目录下的源文件
- Affected code:
  - `qpxt/` - 主应用对象（1个 .sra, 多个 .srw/.sru/.srm）
  - `base/` - 基础类和窗口（约40个文件）
  - `class/` - 业务类（约35个文件）
  - `comclass/` - 公共工具类（约15个文件）
  - `comfunction/` - 公共函数（约30个文件）
  - `commenu/` - 公共菜单（约30个文件）
  - `comstruction/` - 公共结构体（约10个文件）
  - `comwindow/` - 公共窗口（约90个文件）
  - `dw2xls/` - DataWindow转Excel库（约25个文件）
  - `function/` - 应用函数（约35个文件）
  - `menu/` - 应用菜单（约70个文件）
  - `toolbar/` - 工具栏库（约25个文件）
  - `windows/` - 应用窗口（约200个文件）

## ADDED Requirements

### Requirement: 编码修复

系统 SHALL 将所有源文件中的 GBK 乱码字符还原为正确的中文文本。

#### Scenario: 菜单项名称乱码修复
- **WHEN** 菜单文件（.srm）中包含乱码的菜单项名称（如 `m_qpŵǼ`）
- **THEN** 根据上下文和业务逻辑还原为正确的中文名称（如 `m_qp汽配登记`）

#### Scenario: 字符串字面量乱码修复
- **WHEN** 代码中的字符串字面量包含乱码（如 messagebox 中的提示文字）
- **THEN** 根据上下文还原为正确的中文文本

#### Scenario: 窗口标题乱码修复
- **WHEN** 窗口的 title 属性包含乱码
- **THEN** 根据上下文还原为正确的中文标题

### Requirement: 反编译器标记清理

系统 SHALL 移除所有由 Shu 反编译器生成的错误标记和伪代码。

#### Scenario: DEMO_SCRIPT_LIMIT 标记处理
- **WHEN** 函数/事件脚本以 `//SHU_ERROR:DEMO_SCRIPT_LIMIT.` 结尾
- **THEN** 标记该函数为"脚本截断"，需要根据业务逻辑补全缺失的代码

#### Scenario: DEMO_SCRIPT_PASSWORD_LIMIT 标记处理
- **WHEN** 代码中出现 `SHU_ERROR:DEMO_SCRIPT_PASSWORD_LIMIT`
- **THEN** 标记该位置为"密码被遮蔽"，需要根据上下文推断或标记为待确认

#### Scenario: SHU_ERROR:2.0070_FLAG 标记处理
- **WHEN** 结构体或对象中出现 `/*SHU_ERROR:2.0070_FLAG*/` 注释
- **THEN** 移除该注释，该注释仅表示存在重复定义的警告

#### Scenario: 冗余参数注释清理
- **WHEN** 函数/事件体中出现注释形式的参数声明（如 `//string as_language`）
- **THEN** 移除这些冗余的参数注释

#### Scenario: 反编译器签名注释清理
- **WHEN** 出现反编译器生成的签名注释（如 `//close (none) returns (none)`、`//Public function xxx returns yyy`）
- **THEN** 移除这些反编译器伪注释

#### Scenario: LABEL_KENSHU 标签处理
- **WHEN** 代码中出现 `LABEL_KENSHU_N` 形式的标签
- **THEN** 将 GOTO LABEL_KENSHU 语句重构为正常的条件控制流

### Requirement: 逻辑错误修复

系统 SHALL 修复反编译器引入的逻辑错误。

#### Scenario: EXIT 后紧跟 CONTINUE 修复
- **WHEN** 循环中出现 EXIT 后紧跟 CONTINUE 的无意义代码
- **THEN** 移除不可达的 CONTINUE 语句

#### Scenario: 不合理的 CHOOSE CASE 分支修复
- **WHEN** CHOOSE CASE 中所有分支都执行相同操作（如 n_qyqms_connectservice.of_getconnection 中的 autocommit 设置）
- **THEN** 根据业务逻辑修正各分支的正确行为

### Requirement: 项目分析文档

系统 SHALL 提供完整的项目分析报告。

#### Scenario: 项目结构分析
- **WHEN** 分析完成后
- **THEN** 生成包含项目架构、模块划分、业务域、依赖关系等信息的分析报告

## MODIFIED Requirements

### Requirement: 文件格式合规性

所有修复后的文件 SHALL 符合 PowerBuilder 导入格式要求，包含正确的 `$PBExportHeader$` 行和对象定义结构。

## REMOVED Requirements

### Requirement: 无
**Reason**: 初始规范，无移除项
**Migration**: 无
