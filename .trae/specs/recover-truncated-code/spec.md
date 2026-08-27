# 补全截断代码规范

## Why

反编译器演示版对脚本长度有限制，导致 338 处函数/事件代码在 `TODO_RECOVER_LIMITED_CODE` 标记处被截断，另有 5 处密码被 `TODO_RECOVER_PASSWORD` 遮蔽。这些截断代码导致 PowerBuilder 导入时出现语法错误（未闭合的 CHOOSE CASE、IF、FOR 等结构），必须补全后才能编译通过。

## What Changes

- **补全 TODO_RECOVER_LIMITED_CODE 截断代码**：根据上下文逻辑推断并补全 338 处截断的函数/事件代码
- **恢复 TODO_RECOVER_PASSWORD 密码**：根据上下文推断或标记 5 处被遮蔽的密码字段
- **确保语法完整性**：所有补全后的代码必须闭合所有控制结构（CHOOSE CASE/END CHOOSE、IF/END IF、FOR/NEXT、DO/LOOP）

## Impact

- Affected code: 100+ 个文件，跨 10 个目录
  - `windows/` — 337 处（100 个文件，最严重）
  - `comwindow/` — 151 处（60 个文件）
  - `base/` — 41 处（12 个文件）
  - `qpxt/` — 29 处（13 个文件）
  - `dw2xls/` — 20 处（5 个文件）
  - `comclass/` — 14 处（4 个文件）
  - `comfunction/` — 6 处（6 个文件）
  - `function/` — 6 处（6 个文件）
  - `toolbar/` — 7 处（6 个文件）
  - `menu/` — 3 处（3 个文件）
  - `commenu/` — 2 处（2 个文件）
  - `class/` — 3 处（1 个文件）

## ADDED Requirements

### Requirement: 截断代码补全

系统 SHALL 补全所有 `TODO_RECOVER_LIMITED_CODE` 标记处的截断代码。

#### Scenario: CHOOSE CASE 截断补全
- **WHEN** CHOOSE CASE 语句在某个 CASE 分支中被截断
- **THEN** 补全剩余的 CASE 分支、CASE ELSE 和 END CHOOSE，并补全后续代码（变量赋值、循环结束、RETURN 等）

#### Scenario: IF 语句截断补全
- **WHEN** IF 语句在条件体中被截断
- **THEN** 补全 END IF 及后续代码

#### Scenario: FOR/DO 循环截断补全
- **WHEN** 循环体在执行过程中被截断
- **THEN** 补全循环体剩余逻辑和 NEXT/LOOP 语句

#### Scenario: 函数整体逻辑补全
- **WHEN** 函数主体大部分被截断（如 f_getpy 只有变量声明和初始化）
- **THEN** 根据函数名、参数和已有代码推断完整逻辑并补全

### Requirement: 密码恢复

系统 SHALL 处理所有 `TODO_RECOVER_PASSWORD` 标记。

#### Scenario: 可推断密码恢复
- **WHEN** 密码上下文有明确线索（如 ODBC 连接字符串中的默认密码 `19801110`）
- **THEN** 恢复为正确密码

#### Scenario: 不可推断密码标记
- **WHEN** 密码无法从上下文推断
- **THEN** 保留 TODO_RECOVER_PASSWORD 标记并添加注释说明

### Requirement: 语法完整性验证

系统 SHALL 确保所有补全后的文件语法正确。

#### Scenario: 控制结构闭合
- **WHEN** 补全完成后
- **THEN** 每个 CHOOSE CASE 有 END CHOOSE，每个 IF 有 END IF，每个 FOR 有 NEXT，每个 DO 有 LOOP

#### Scenario: 函数结束标记
- **WHEN** 补全完成后
- **THEN** 每个函数/事件以 `end function`/`end subroutine`/`end event` 正确结束

## MODIFIED Requirements

### Requirement: 无破坏性修改
补全代码时 SHALL 保持已有代码不变，仅在 `TODO_RECOVER_LIMITED_CODE` 标记位置插入补全代码并移除该标记。

## REMOVED Requirements

无
