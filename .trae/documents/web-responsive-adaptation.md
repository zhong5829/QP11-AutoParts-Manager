# Web 端响应式适配改造（电脑 / 平板 / 手机）

## Context

Web 端是 [QP11.WebApi/wwwroot](file:///f:/qp11/src/QP11.WebApi/wwwroot) 下由 `QP11.WebApi` 托管的单页应用（登录 + 「销售开单」「单据查询」两个 Tab + 两个弹窗），页脚标注 "Ver 13.1 Web"。

当前适配不完整：

- [style.css](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css#L9) 中 `.app { max-width: 960px; margin: 0 auto; }` 固定成一条窄栏，大屏显示器上大量留白；
- 媒体查询只有两处：[style.css#L275-L286](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css#L274-L290)（≤650px）与 [style.css#L287-L290](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css#L287-L290)（≥800px），平板档位缺位；
- 三个列表都是固定列数的宽表格（配件 7 列、明细 8 列、单据 7 列、单据详情 9 列），手机上只能靠横向滚动看价格列。

**目标**：一套样式自动适配电脑（全宽铺满）、平板（保留表格）、手机（列表转卡片 + 底部固定操作栏），并保持现有交互行为完全不变。

**用户已确认的三项决策**：手机端「表格转卡片」；桌面「全宽铺满」；移动端增强只做「底部固定操作栏」（不做弹窗全屏化、不改触控尺寸、不隐藏次要列）。

## 关键现状（已核实）

- 列表行全部由 [app.js](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L393-L402) 用 `innerHTML` 拼固定顺序的 `<tr><td>` 生成，行点击用**内联 `onclick`**（`onPartClick(this)`、`showBillDetail(...)`）并从 `row.dataset` 取值；明细行删除按钮为 `onclick="removeDetail(i)"`。
- [app.js](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js) 里**没有**任何 `matchMedia` / `innerWidth` / `resize` / `orientation` / `style.width` 逻辑，也**没有** `cells[]`、`children[]`、`nth-child` 之类依赖单元格顺序的代码；行上**没有** `data-label` 属性。
- 空态行是 `<tr><td colspan="N" class="empty-hint">`（[app.js#L389](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L389)、[#L854](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L854)、[#L1035](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L1035)、[#L1165](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L1165)）。
- 单据详情弹窗内的明细表同样使用 `.data-table` 类，9 列，外层已有 `.table-wrap`（[app.js#L1156-L1162](file:///f:/qp11/src/QP11.WebApi/wwwroot/app.js#L1156-L1162)）。
- 弹窗内「销售历史 / 采购历史」用的是独立的 `.dlg-tbl` 类，不参与本次卡片化。
- 操作按钮区 `.actions` 位于 `#tab-sell` 内部（[index.html#L157-L161](file:///f:/qp11/src/QP11.WebApi/wwwroot/index.html#L157-L161)），Tab 隐藏时不会显示，天然适合做「仅销售开单页」的底部固定栏。

**结论：本轮改造是纯 CSS 的，`app.js` 一行不用改，`index.html` 只改一行版本号。**

## 实施方案

### 1. 断点体系（三档）

| 档位 | 宽度 | 布局 |
| --- | --- | --- |
| 手机 | `≤650px` | 网格单栏/两栏 + 三个列表卡片化 + 底部固定操作栏 |
| 平板 | `651–1023px` | 保留表格（必要时横向滚动）；单头信息 3 列 |
| 桌面 | `≥1024px` | 容器全宽铺满；单头信息 6 列 |

替换现有 `@media (min-width: 800px)`（[style.css#L287-L290](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css#L287-L290)），新增 `651–1023px` 档。

### 2. 桌面全宽铺满

- [style.css#L9](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css#L9) `.app`：去掉 `max-width: 960px` 与 `margin: 0 auto`，改为占满窗口。
- `.header` 与 `.tab-content` 的左右内边距统一到 16px，使标题与卡片左右对齐；`.tab-content` 底部预留内边距（见第 4 点）。
- 表格随容器铺满；`.bh-grid` 在 `≥1024px` 维持 6 列（沿用原 `min-width:800px` 的列定义）。

### 3. 手机端表格转卡片（`≤650px`，纯 CSS）

统一作用于 `.data-table`，再按表分别用 `nth-child` 补字段名（行上没有 `data-label`，因此标签文字写在 CSS 里）：

- 结构：`.table-wrap { overflow: visible; }`；`.data-table thead { display: none; }`；`.data-table, tbody, tr, td { display: block; width: 100%; }`；覆盖 `.data-table` 的 `white-space: nowrap` 为 `normal`。
- 卡片外观：`tbody tr` 加边框、圆角、内边距、行间距；`td` 用 `display: flex; justify-content: space-between; gap: 10px;` 让「字段名在左、值在右」。
- 字段名：`td:nth-child(n)::before { content: "编号"; color: #888; font-weight: 400; }`，四张表的映射如下：

  | 表 | 选择器 | 列（按 nth-child 顺序） |
  | --- | --- | --- |
  | 配件库存 | `#partsTable` | 编号 / 名称 / 车型 / 库存 / 零售价 / 批发价 / 仓位 |
  | 销售明细 | `#detailTable` | 编号 / 名称 / 数量 / 单价 / 小计 / 车型 / 备注 /（操作列不加标签，删除按钮居右） |
  | 单据查询 | `#billTable` | 单号 / 日期 / 客户 / 业务员 / 总额 / 开票总额 / 状态 |
  | 单据详情弹窗 | `.modal-body .data-table` | 编号 / 名称 / 数量 / 单价 / 开票单价 / 小计 / 车型 / 车牌 / 备注 |

- 空态：`td.empty-hint { justify-content: center; }` 且 `td.empty-hint::before { content: none; }`（`colspan` 在 block 模式下不生效，无需处理）。
- 保留现有数字列右对齐/配色规则（`#billTable` 第 6 列蓝色加粗、配件零售/批发价红色等），在 block 模式下改为对 `td` 生效即可。

### 4. 手机端底部固定操作栏（`≤650px`）

- `.actions { position: fixed; left: 0; right: 0; bottom: 0; margin: 0; padding: 8px 16px calc(8px + env(safe-area-inset-bottom)); background: #fff; border-top: 1px solid #eee; z-index: 90; }`（低于 sticky 头部 `z-index: 100`）。
- `.tab-content` 底部内边距增大（约 88px），避免被固定栏遮挡。
- `.actions` 只在 `#tab-sell` 内，切到「单据查询」时随 Tab 隐藏，无需额外 JS。

### 5. 平板档（`651–1023px`）

- `.bh-grid` 与 `#tab-query .query-row` 收敛为更易点选的两三列排布；`.bh-memo-full` 相应调整 `grid-column: span`。
- 三个列表保留表格与表头，窄平板下由现有 `.table-wrap { overflow-x: auto; }` 承担横向滚动。
- 销售编辑弹窗维持两栏（左栏 280px 固定 + 右栏自适应，右栏历史表沿用 `.table-wrap-sm` 横向滚动）。

### 6. 缓存版本号

[index.html#L7](file:///f:/qp11/src/QP11.WebApi/wwwroot/index.html#L7) 的 `style.css?v=1.0.0` 提升为 `v=1.1.0`，确保浏览器不会命中旧样式缓存（`app.js` 不动，其版本号保持不变）。

### 7. 明确不做

- 不改 `app.js` 任何逻辑（行渲染、事件绑定、数据集属性全部保持原样）；
- 不引入 Bootstrap/Tailwind 等框架或 CDN 依赖（该页面由安装目录静态托管，需离线可用）；
- 不做弹窗全屏化、不改触控热区尺寸、不隐藏次要列、不改 `viewport` meta、不动弹窗内 `.dlg-tbl` 历史表。

## 涉及文件

| 文件 | 改动 |
| --- | --- |
| [src/QP11.WebApi/wwwroot/style.css](file:///f:/qp11/src/QP11.WebApi/wwwroot/style.css) | 主要改动：断点体系、全宽、卡片化、底部固定栏 |
| [src/QP11.WebApi/wwwroot/index.html](file:///f:/qp11/src/QP11.WebApi/wwwroot/index.html) | 仅第 7 行 CSS 版本号 `1.0.0` → `1.1.0` |

## 验证

1. 启动：`dotnet run --project src/QP11.WebApi`（Development 配置，地址 `http://localhost:5071`），或通过 WPF 内嵌的 Web 服务入口打开。
2. 用浏览器 DevTools 设备模拟三种尺寸逐项检查：
   - **桌面 1920×1080 / 1440×900**：内容铺满窗口、无 960px 窄栏；单头信息 6 列一行；表格不溢出。
   - **平板 768×1024（竖）/ 1024×768（横）**：表格行列完整、必要时可横向滚动；单头信息 3 列；弹窗两栏正常。
   - **手机 375×667 / 390×844**：三张列表均为卡片、字段名与值左右对齐；底部「清空重填 / 确认开单」固定悬浮不遮挡内容；安全区留白正常。
3. 功能回归（重点确认 CSS 改动没影响交互）：
   - 配件列表：单击选中、库存为 0 时打开只读弹窗、废品仓二次确认、「隐藏废品仓」过滤后列表仍可点；
   - 销售明细：加明细、删除明细按钮、合计与开票合计联动；
   - 确认开单 → 切到「单据查询」：列表点击打开单据详情弹窗（9 列明细卡片化后可读）、分页按钮可用；
   - 登录遮罩、Toast 提示在三种尺寸下定位正常。
4. 建议用浏览器自动化在 375px / 768px / 1440px 三档各截一张图，与改动前对比确认无布局错乱。