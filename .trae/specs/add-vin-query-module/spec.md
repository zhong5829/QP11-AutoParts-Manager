# VIN查询模块 Spec

## Why
当前QP11系统缺少VIN码（车架号）查询功能，门店销售时无法快速根据客户车辆VIN码定位适配配件。微信小程序（配达丰汽配微商城）通过318car SaaS平台提供VIN解码和适配配件数据，需要将此数据源对接到QP11桌面端，实现VIN解码→配件匹配→一键添加到销售明细的完整业务闭环。

## What Changes
- 新增VIN查询独立窗口（非模态Window），采用聊天式UI界面，菜单入口在"进销存管理"下"销售退货"之后
- 新增 `VinDecodeResult`、`VinPartCard` 实体类（Core层）
- 新增 `IVinQueryService` 接口及两个实现：`VinQueryService`（真实318car API）和 `MockVinQueryService`（开发调试）
- 新增 `VinQueryWindow.xaml/.cs` 独立非模态窗口，聊天式UI：底部输入框发送VIN→消息气泡展示车型信息→配件卡片列表→配件详情展开→添加到销售明细
- 修改 `MainWindow.xaml` 新增"VIN查询"菜单项
- 修改 `MainWindow.xaml.cs` 新增菜单路由、VIN窗口单例管理、`GetActiveSellControl()` 方法
- 修改 `SellControl.xaml.cs` 新增 `AddDetailFromVin()` 公开方法供VIN窗口调用
- 修改 `App.xaml.cs` 注册 `IVinQueryService` 到DI容器
- 修改 `appsettings.json` 新增 `VinQuery` 配置节

## Impact
- Affected specs: 销售开单流程（新增从VIN窗口添加配件明细的路径）
- Affected code:
  - `QP11.Core/Entities/` — 新增2个实体
  - `QP11.Core/Interfaces/` — 新增1个接口
  - `QP11.Services/` — 新增2个Service实现
  - `QP11.Wpf/Views/` — 新增VinQueryWindow，修改MainWindow、SellControl
  - `QP11.Wpf/App.xaml.cs` — DI注册
  - `QP11.Wpf/appsettings.json` — 配置

---

## 318car API 接口规范

### 基础信息

| 项目 | 值 |
|------|-----|
| API域名 | `https://mp.318car.com` |
| 鉴权方式 | JWT Bearer Token |
| 通用Headers | `Authorization: Bearer <accessToken>` |
| | `refreshToken: Bearer <refreshToken>` |
| | `Tenant: 226` |
| | `Content-Type: application/json` |
| 图片CDN | `https://pandafunphoto.oss-cn-shanghai.aliyuncs.com` |
| 响应格式 | `{"code":10200,"message":"OK","data":...}` |
| 成功码 | `10200` |

### JWT Token结构

```
accessToken payload:
  { "version":8, "userId":81129, "username":"15781805504", "exp":<timestamp>, "jti":"<uuid>" }

refreshToken payload:
  { "version":8, "userId":81129, "username":"15781805504", "exp":<timestamp>, "jti":"<uuid>", "ati":"<accessToken_jti>" }
```

- accessToken 有效期较短（约1天）
- refreshToken 有效期较长（约30天）
- accessToken 过期后需用 refreshToken 刷新

### API-0: 登录（手机号+短信验证码）

```
步骤1: 发送短信验证码
  GET /app/sms/sendSms?phone={手机号}
  Host: mp.318car.com
  Headers: (无需认证)

步骤2: 短信登录（获取Token）
  POST /app/smsLogin?username={手机号}&smsCode={验证码}
  Host: mp.318car.com
  Headers: (无需认证)
  Response: 返回 accessToken 和 refreshToken
```

**QP11 Token获取方案**：QP11在VIN查询窗口提供登录界面，用户输入手机号→点击发送验证码→输入收到的短信验证码→点击登录→获取accessToken和refreshToken。Token过期后自动刷新或提示重新登录。

### API-1: VIN解码（根据VIN码获取车型信息）

```
POST /app/product/getVehicleByVin?vin={VIN码}&tenantId=226
Content-Type: application/x-www-form-urlencoded (Body为空，VIN在URL query中)

Request Headers:
  Authorization: Bearer <accessToken>
  refreshToken: Bearer <refreshToken>
  Tenant: 226

Response.data:
  {
    "vin": "LSGAR5AL7FH094750",
    "brand": "凯迪拉克",           // 品牌
    "manufacturers": "上海通用",   // 制造商
    "series": "ATS",              // 车系
    "models": "ATS-L",            // 车型
    "chassisCode4": "SA1",        // 底盘码
    "displacementWithT": "2.0T",  // 排量
    "engineModel": "LTG",         // 发动机型号
    "yearRange": "2015-2015",     // 年款范围
    "generation": "1",            // 代数
    "vehicleAttributes": "合资",  // 车辆属性
    "brandImg": "https://...KaiDiLaKe.png",  // 品牌Logo
    "isCommercial": 0,            // 是否商用车
    "productTime": "2015-02-03",  // 生产日期
    "vehicleIds": [               // 车型ID列表（用于API-2查询）
      "65b4a7a4e870e217b937384b",
      "65b4a7a4e870e217b937384a"
    ]
  }
```

**完整VIN查询流程**：`getVehicleByVin(VIN码)` → 拿到vehicleIds和车型信息 → `pageProduct(vin, vehicleIds, 车型信息, 分页参数)` → 拿到配件列表

### API-2: 获取适配配件列表（分页）

```
POST /app/product/user/pageProduct

Request Body:
  {
    "vin": "LSGAR5AL7FH094750",
    "vehicleIds": ["65b4a7a4e870e217b937384b","65b4a7a4e870e217b937384a"],
    "queryType": 5,
    "querySource": 1,
    "brand": "凯迪拉克",
    "manufacturers": "上海通用",
    "series": "ATS",
    "models": "ATS-L",
    "chassisCode4": "SA1",
    "displacementWithT": "2.0T",
    "engineModel": "LTG",
    "yearRange": "2015-2015",
    "generation": "1",
    "vehicleAttributes": "合资",
    "tenantId": 226,
    "isCard": 1,
    "current": 1,           // 页码（从1开始）
    "size": 15,             // 每页条数
    "str": ""               // 搜索关键词（空=全部）
  }

Response.data:
  {
    "total": 55,            // 总配件数
    "pages": 4,             // 总页数
    "current": 1,           // 当前页
    "size": 15,             // 每页大小
    "empowerTenantProductList": [  // 按分类组织的配件列表
      {
        "tenantCategoryId": 30974,
        "categoryName": "前平衡杆",
        "productList": [
          {
            "id": 2831,                    // 配件ID（318car平台）
            "productId": 2831,
            "name": "S-2415L~前平衡杆L",   // 配件名称
            "model": "S-2415L",            // 配件型号
            "imgUrlList": ["https://..."], // 图片URL数组
            "tenantBrandName": "携豹球头摆臂",  // 品牌名
            "tenantCategoryName": "前平衡杆",  // 分类名
            "notes": "凯迪拉克ATS(CA1)[14-]",  // 备注/适用车型
            "unit": "件",                  // 单位
            "producer": "中国",            // 产地
            "price": 35.00,               // 售价（指导价）
            "purchasePrice": 35.00,       // 进货价
            "purchaseGuidePrice": 50.00,  // 进货指导价
            "guidePrice": 35.00,          // 指导价
            "costPrice": 0.00,            // 成本价
            "stock": 0,                   // 318car平台库存
            "weight": 0.00,               // 重量
            "showPrice": 1,               // 是否显示价格
            "installationLocation": null,  // 安装位置
            "productUnitList": [          // 单位换算列表
              {"unit":"件","sort":1,"convertNum":1}
            ]
          }
        ]
      }
    ],
    "empTenantBrandList": [...],       // 品牌列表（可选筛选）
    "empTenantCategoryList": [...],    // 分类树（可选筛选）
    "adaptQueryRecordId": 249382864,   // 本次查询记录ID
    "openDisclaimer": 0,               // 是否显示免责声明
    "disclaimerContent": "..."         // 免责声明内容
  }
```

### API-3: Token刷新

```
POST /app/user/saastoken
Headers:
  Authorization: Bearer <accessToken>
  refreshToken: Bearer <refreshToken>
  Tenant: 226
  Content-Type: application/json

Response: 返回新的 accessToken 和 refreshToken
```

accessToken过期后（HTTP 401），用此接口刷新。若刷新失败，提示用户重新登录。

### Token获取方式

- **主方案（推荐）**：VIN查询窗口内嵌登录界面，手机号+短信验证码登录（`/app/sms/sendSms` → `/app/smsLogin`），自动获取accessToken和refreshToken
- Token过期后，优先调用 `/app/user/saastoken` 自动刷新；刷新失败则提示用户重新登录

### API域名说明

318car平台有两个域名指向同一后端：
- `mp.318car.com` — 微信小程序使用（QP11默认使用此域名）
- `erp.epbao.cn` — 配达丰桌面ERP使用

QP11默认使用 `mp.318car.com`，可通过 `appsettings.json` 的 `VinQuery:ApiBaseUrl` 切换。

---

## ADDED Requirements

### Requirement: VIN查询独立窗口
系统SHALL提供一个独立非模态窗口，用户可在不关闭该窗口的情况下继续操作主程序。

#### Scenario: 打开VIN查询窗口
- **WHEN** 用户点击菜单"进销存管理 → VIN查询"
- **THEN** 打开一个独立非模态Window，主窗口仍可正常操作
- **AND** 底部输入框自动获得焦点

#### Scenario: 防止重复打开
- **WHEN** VIN查询窗口已打开，用户再次点击菜单
- **THEN** 激活已有窗口而非新建

#### Scenario: 关闭主窗口时自动关闭VIN窗口
- **WHEN** 主窗口关闭
- **THEN** VIN查询窗口自动关闭

### Requirement: 聊天式UI界面
系统SHALL采用聊天式UI交互模式，用户在底部输入框输入VIN码，结果以消息气泡形式展示在对话区域。

#### Scenario: VIN输入与发送
- **WHEN** 用户在底部输入框输入VIN码并按Enter或点击发送按钮
- **THEN** VIN码作为用户消息气泡显示在对话区右侧
- **AND** 系统发起VIN解码请求

#### Scenario: 车型信息以气泡回复
- **WHEN** VIN解码成功
- **THEN** 车型信息以系统消息气泡显示在对话区左侧，包含品牌、车系、车型、年款、发动机、排量、底盘码
- **AND** 紧接其后以气泡展示适配配件卡片列表

#### Scenario: VIN格式校验
- **WHEN** 用户输入的VIN码长度不等于17位
- **THEN** 以系统消息气泡提示"VIN码应为17位"且不发起请求

#### Scenario: 对话历史滚动
- **WHEN** 新消息添加到对话区
- **THEN** 对话区自动滚动到最新消息

#### Scenario: 多次查询保留历史
- **WHEN** 用户连续查询多个VIN码
- **THEN** 所有查询的对话历史保留在对话区，可上下滚动查看

#### Scenario: API不可用
- **WHEN** 318car API请求失败或超时
- **THEN** 以系统消息气泡显示错误提示，不崩溃

### Requirement: 318car API对接与Token管理
系统SHALL对接318car SaaS平台API，实现手机号+短信验证码登录和JWT Token管理。

#### Scenario: 首次登录
- **WHEN** 用户首次打开VIN查询窗口且未配置Token
- **THEN** 显示登录界面：手机号输入框 + 发送验证码按钮 + 验证码输入框 + 登录按钮
- **AND** 点击发送验证码调用 `GET /app/sms/sendSms?phone={手机号}`
- **AND** 输入验证码点击登录调用 `POST /app/smsLogin?username={手机号}&smsCode={验证码}`
- **AND** 登录成功后保存accessToken和refreshToken，隐藏登录界面显示查询界面

#### Scenario: 调用VIN解码API
- **WHEN** 用户发送VIN码
- **THEN** 调用 `POST https://mp.318car.com/app/product/getVehicleByVin?vin={VIN码}&tenantId=226` 获取车型信息
- **AND** 请求Header携带 `Authorization: Bearer <accessToken>`、`refreshToken: Bearer <refreshToken>`、`Tenant: 226`
- **AND** VIN码在URL query参数中传递（非Body）

#### Scenario: 调用配件查询API
- **WHEN** VIN解码成功返回vehicleIds
- **THEN** 调用 `POST https://mp.318car.com/app/product/user/pageProduct` 获取适配配件列表
- **AND** 请求Body包含vin、vehicleIds、车型信息、分页参数（current=1, size=15）

#### Scenario: Token自动刷新
- **WHEN** API返回HTTP 401（Token过期）
- **THEN** 用refreshToken调用刷新接口获取新accessToken
- **AND** 用新Token重试原请求
- **AND** 刷新失败时提示"登录已过期，请重新登录"并显示登录界面

#### Scenario: 重新登录
- **WHEN** Token失效且自动刷新失败
- **THEN** VIN查询窗口切换到登录界面，用户重新输入手机号+验证码登录

#### Scenario: 配件分页加载
- **WHEN** 配件总数超过一页（total > size * current）
- **THEN** 在配件列表底部显示"加载更多"按钮
- **AND** 点击后请求下一页（current++）

### Requirement: 适配配件卡片列表
系统SHALL在车型信息气泡下方展示318car返回的适配配件卡片（按分类组织），并补充本地库存和价格数据。

#### Scenario: 展示配件卡片
- **WHEN** VIN解码成功后
- **THEN** 解析 `pageProduct` 响应中 `empowerTenantProductList` 按分类展示配件
- **AND** 每个配件卡片显示：型号(model)、名称(name)、品牌(tenantBrandName)、分类(categoryName)、售价(price)、指导价(guidePrice)、备注(notes)、图片首张缩略图

#### Scenario: 本地库存匹配
- **WHEN** 318car返回配件卡片
- **THEN** 按优先级匹配本地 `part_data` 表：精确匹配 `partno` = `model` → 模糊匹配 `name` 包含 `model` → 模糊匹配 `name` 包含 `name`
- **AND** 匹配成功的卡片标记 `IsLocalMatched=true` 并填充 `LocalPartId`、`LsPrice`、`PfPrice`、`StockAmount`
- **AND** 未匹配的卡片显示"未匹配本地库存"标识

### Requirement: 配件详情展开
系统SHALL支持点击配件卡片展开详情，显示配件图片和完整信息。

#### Scenario: 点击卡片展开详情
- **WHEN** 用户点击配件卡片
- **THEN** 展开详情区域，显示：
    - 图片轮播（来自318car `imgUrlList`）
    - 型号(model)、品牌(tenantBrandName)、分类(tenantCategoryName)
    - 售价(price)、进货价(purchasePrice)、指导价(guidePrice)
    - 备注/适用车型(notes)、产地(producer)、单位(unit)、安装位置(installationLocation)
    - 本地零售价/批发价/库存（如果IsLocalMatched=true）

### Requirement: 添加配件到销售明细
系统SHALL支持从VIN窗口将配件添加到当前销售开单的配件明细中，且必须校验主程序是否在销售开单页面。

#### Scenario: 正常添加配件
- **WHEN** 用户点击配件的[添加]按钮，且该配件已匹配本地库存，且主程序当前激活Tab为销售开单页面
- **THEN** 弹出已有的 `SellEditDialog` 设置数量/单价/开票单价
- **AND** 用户确认后，配件追加到当前激活销售开单Tab的明细列表

#### Scenario: 配件未匹配本地库存
- **WHEN** 用户点击[添加]按钮，但该配件 `IsLocalMatched=false`
- **THEN** 弹窗提示"该配件未匹配到本地库存，无法添加"且不弹出编辑窗口

#### Scenario: 主程序未在销售开单页面
- **WHEN** 用户点击[添加]按钮，但主程序当前激活Tab不是销售开单页面
- **THEN** 弹窗提示"请先打开销售开单页面后再添加配件"且不弹出编辑窗口

#### Scenario: 库存为0的配件
- **WHEN** 用户添加库存为0的配件
- **THEN** `SellEditDialog` 以只读模式打开，仅可查看历史记录（复用现有逻辑）

### Requirement: Mock服务用于开发调试
系统SHALL在 `appsettings.json` 中 `VinQuery:UseMock=true` 时使用 `MockVinQueryService`，无需318car API即可开发调试。

#### Scenario: 使用Mock服务
- **WHEN** `appsettings.json` 中 `VinQuery:UseMock` 为 `true`
- **THEN** DI容器注册 `MockVinQueryService`，返回模拟车型和配件数据
- **AND** 无需真实API即可完成UI开发和调试

## MODIFIED Requirements

### Requirement: 销售开单配件明细添加路径
原有：配件明细仅通过SellControl内部配件搜索添加。
修改：新增从VIN查询窗口添加配件明细的路径，通过 `SellControl.AddDetailFromVin()` 方法实现，数据结构和现有添加逻辑一致（SellControlItem）。

## REMOVED Requirements
无
