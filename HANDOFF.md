# HANDOFF 交接文档

## 项目概述
QP11 汽配管理系统（WPF + .NET 8，自包含发布，免预装环境）

## 本会话完成的任务

### 1. 桌面导航工作台改造（多次迭代）
- **今日销售单据**：客户编码列改为客户名称，SQL `LEFT JOIN client_infor` 取 `name`
- **移除**：库存预警、应收款两个 KPI 卡片
- **新增今日配件销售排行**：右侧 DataGrid 显示 TOP 10 配件（排名/编号/名称/车型/销量/金额/库存），SQL 按 `detail_sell` 聚合销量，`LEFT JOIN part_stock` 取实时库存
- **UI 调整**：两栏宽度从 `6*/4*` 改为 `5*/5*`，排行各列宽重新设计（35/85/*/75/50/80/50），新增车型列

### 2. 打包 v2.2.9
- 版本号：v2.2.8 → v2.2.9
- 安装包路径：`F:\qp11\InstallPackage\QP11Setup_v2.2.9.exe`
- 同步更新：`QP11.Wpf.csproj` 第 28 行、`QP11_Setup.iss` 第 7 行
- 修改记录已更新到 `修改记录.md`

## 当前状态
- 所有修改均已编译通过（0 错误）
- 安装包已打包产出

## 踩过的坑（绝对不要再踩）

### 1. SQL 加 JOIN 时要处理字段歧义
给 `SellRepository.GetListAsync` 加 `LEFT JOIN client_infor` 时，`client_infor` 表也有 `name`、`sn` 等字段，与 `bill_sell` 冲突。**必须给所有字段加表名前缀**（如 `bill_sell.sn`、`bill_sell.datetime`），否则 SQL 报歧义错误。

### 2. 变更 `GetListAsync` 前先确认所有调用方
该方法在 `SellBalanceWindow` 等地方也有调用。加字段不影响它们（只查金额字段），但**改函数签名或删字段会炸**。修改前先全局搜索调用方。

### 3. 工作台移除 KPI 时要同步更新构造函数
`DesktopControl` 构造函数如果注入了 `IArrearageRepository`，移除后要同步删掉构造函数参数，且所有实例化 `DesktopControl` 的地方（`MainWindow.xaml.cs`）也要删掉对应参数。**VS 编译错误会提醒**，但最好统一改完后一次性编译验证。

### 4. 多语言资源文件打包巨大
自包含发布会把 `cs/de/es/fr/it/ja/ko/pl/pt-BR/ru/tr/zh-Hans/zh-Hant` 所有语言资源打进安装包，这是 .NET 8 自包含的默认行为，启动时会在 `\` 下创建语言文件夹。**不要手动删 publish 目录下的语言文件夹**，否则 ISCC 报文件缺失。可以在 csproj 中加 `SatelliteResourceLanguages` 属性限制，但当前未做优化。

### 5. Inno Setup 已有警告非新增
打包时 ISCC 有 3 个警告：
- `OnlyBelowVersion 6.1` 的 quicklaunchicon 任务（新系统不处理）
- `PrivilegesRequired=admin` 与 per-user 区域共存
- `ResultCode` 变量未使用
以上均为既有问题，**不要尝试修复**，已正常运行多年。

## 下一步可能的计划
- 无明确待办事项。本次工作台改造已全部完成并打包。

## 修改记录.md 位置
`F:\qp11\修改记录.md` — 所有修改均有详细记录，包括每步操作、修改文件、SQL 变更。

## 打包命令备忘
```powershell
# 清理旧发布
Remove-Item -Recurse -Force F:\qp11\Publish\*.*

# 发布
dotnet publish F:\qp11\src\QP11.Wpf\QP11.Wpf.csproj -c Release -o F:\qp11\Publish

# 编译安装包
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" F:\qp11\QP11_Setup.iss
```