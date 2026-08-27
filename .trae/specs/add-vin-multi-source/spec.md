# VIN查询多数据源并行查询 Spec

## Why
当前VIN查询模块仅对接318car单一数据源，配件覆盖范围有限。新增品秀传动(dataenlighten)数据源，查询时同时并行请求两个数据源，将结果合并展示到配件适配列表中，扩大配件覆盖面，提升查询命中率。

## What Changes
- 新增 `IVinDataSource` 数据源抽象接口，解耦具体平台实现
- 重构 `VinQueryService` 实现 `IVinDataSource`（318car数据源）
- 新增 `PinxiuDataSource` 实现 `IVinDataSource`（品秀传动数据源，完整实现）
- 新增 `CompositeVinQueryService` 组合服务，并行查询多数据源并合并结果
- 修改 `VinPartCard` 增加 `SourceName`、`PartNumber`(OE号)、`VehicleComment`(车型备注) 字段
- 修改 `IVinQueryService` 接口适配多数据源登录
- 修改 `VinQueryWindow` 登录面板适配多数据源，配件列表显示来源标识
- 修改 `App.xaml.cs` DI注册改为组合服务
- 修改 `appsettings.json` 新增品秀数据源配置节

## Impact
- Affected specs: add-vin-query-module（VIN查询模块，扩展多数据源能力）
- Affected code:
  - `QP11.Core/Interfaces/` — 新增 `IVinDataSource`，修改 `IVinQueryService`
  - `QP11.Core/Entities/VinPartCard.cs` — 增加 `SourceName`、`PartNumber`、`VehicleComment` 字段
  - `QP11.Services/VinQueryService.cs` — 重构为实现 `IVinDataSource`
  - `QP11.Services/PinxiuDataSource.cs` — 新增（品秀传动数据源完整实现）
  - `QP11.Services/CompositeVinQueryService.cs` — 新增
  - `QP11.Wpf/App.xaml.cs` — DI注册变更
  - `QP11.Wpf/Views/VinQueryWindow.xaml/.cs` — 登录面板 + 配件列表UI适配
  - `QP11.Wpf/appsettings.json` — 配置变更

---

## 品秀传动(dataenlighten) API 接口规范

### 基础信息

| 项目 | 值 |
|------|-----|
| API域名 | `https://api.dataenlighten.com:8045` |
| 前端域名 | `https://applets-new.dataenlighten.com` |
| 鉴权方式 | JWT Bearer Token (HS512) |
| 通用Headers | `company-id: MTEzMQ==` |
| | `product-code: MKZ25` |
| | `content-type: application/json` |
| | `authorization: Bearer <token>` (登录后) |
| 图片CDN | `https://mj-pdm-water.oss-cn-shanghai.aliyuncs.com` |
| 响应编码 | **Base64编码的JSON**（需先Base64解码再解析JSON） |
| 成功码 | `code: "0000"` |
| Token有效期 | 15天 (1296000秒) |

### JWT Token结构

```
accessToken payload:
  { "loginuser": "SMS:17601279397:1783926406357:1296000" }
```

- `loginuser` 格式: `SMS:<手机号>:<时间戳>:<有效期秒数>`
- 有效期 1296000 秒 = 15天

### API-0: 发送短信验证码

```
POST /pdmPro/oauth/oauthSendSmsCodeValidate
Headers: company-id, product-code, content-type
Body: {"phone":"17601279397"}

Response (Base64解码后):
{
  "code": "0000",
  "codeDescription": "成功",
  "toastMessage": "成功"
}
```

### API-1: 短信验证码登录/注册

```
POST /pdmPro/oauth/loginOrRegSpUser
Headers: company-id, product-code, content-type
Body: {"phone":"17601279397","code":"123456"}

Response (Base64解码后):
{
  "code": "0000",
  "codeDescription": "成功",
  "data": { ... token信息 ... }
}
```

### API-2: VIN解码（获取车型信息 + mjsid）

```
POST /pdmPro/aisearch/getAlphaRecommendVehicleList
Headers: authorization, company-id, product-code, content-type
Body: {
  "sourcetype": 1,
  "fieldType": "",
  "fieldName": "",
  "keyword": "LSGXE8352JD091700",       // VIN码
  "vehicleInfoReq": {},
  "flag": 0,
  "vehicleInfo": {},
  "addField": ["brand","sub_brand","vehicle_group","displacement","engine"],
  "pageIndex": 1,
  "pageSize": 10
}

Response.data (Base64解码后):
{
  "totalRecCount": 1,
  "totalPageCount": 1,
  "vin": "LSGXE8352JD091700",
  "list": [{
    "mjsid": "MJS2211472,MJS4598532,MJS3749931",  // ← 关键！后续查询用此ID
    "engine": "LFV",
    "isAdapt": 1,
    "vehicle_group": "昂科威 CP4 2014-",
    "sub_brand": "上汽通用",
    "displacement": "1.5T",
    "vehicle_chn": "昂科威",
    "brand": "别克"
  }],
  "uniqueConvert": 0
}
```

### API-3: 根据VIN获取配件列表（核心接口）

```
POST /pdmPro/sp/getProdListByVIN
Headers: authorization, company-id, product-code, content-type
Body: {
  "mjsids": ["MJS2211472","MJS4598532","MJS3749931"],  // 从API-2获取
  "vin": "LSGXE8352JD091700"
}

Response.data (Base64解码后):
{
  "cspuList": [                             // 按分类组织的配件列表
    {
      "categoryId": "63003",
      "categoryName": "半轴",
      "prodList": [
        {
          "cspuId": "549496",               // 配件ID
          "cspuModel": "GM-8-097",          // 配件型号(编码) ← 匹配本地库存用此字段
          "brandName": "CCL EPX",           // 品牌
          "categoryName": "半轴",           // 分类
          "placesName": "前桥左",           // 安装位置
          "partNumber": "84280831",         // OE号
          "qty": "1",                       // 数量
          "stock": 1,                       // 库存状态(1=有货)
          "stockQuantity": 0,               // 库存数量
          "marketPrice": "",                // 市场价
          "mj4sPrice": "",                  // 4S店价
          "vehicleComment": "左右 C-1722",  // 车型备注(部分配件有)
          "mappingCspuModel": "",           // 映射型号
          "pImage": "https://...",          // 图片URL
          "sid": "MJS2211472",             // 车型SID
          "brandId": 101395,
          "mjCategoryId": 775,
          "categoryId": 63003,
          "attrList": [],
          "brandSortNumber": -1
        }
      ]
    }
  ],
  "categoryList": [                         // 分类摘要
    { "categoryName": "半轴", "categoryId": 63003 },
    { "categoryName": "半轴球笼修理包", "categoryId": 63005 }
  ],
  "brandList": [                            // 品牌摘要
    { "brand_name": "CCL EPX", "brand_id": 101395 }
  ]
}
```

### 查询流程

```
Step 1: oauthSendSmsCodeValidate → 发送验证码
Step 2: loginOrRegSpUser → 登录获取Token
Step 3: getAlphaRecommendVehicleList(VIN码) → 获取mjsid和车型信息
Step 4: getProdListByVIN(mjsids, vin) → 获取配件列表（核心数据，cspuList中含分类信息）
```

### 与318car API的关键差异

| 差异点 | 318car | 品秀传动 |
|--------|--------|----------|
| VIN解码返回ID | `vehicleIds` (List<string>) | `mjsid` (逗号分隔字符串) |
| 配件编码字段 | `model` | `cspuModel` |
| OE号 | 无 | `partNumber` |
| 安装位置 | `installationLocation` | `placesName` |
| 车型备注 | `notes` | `vehicleComment` |
| 价格字段 | `price`/`purchasePrice`/`guidePrice` | `marketPrice`/`mj4sPrice` (常为空) |
| 品牌字段 | `tenantBrandName` | `brandName` |
| 分类字段 | `tenantCategoryName` | `categoryName` |
| 图片字段 | `imgUrlList` (数组) | `pImage` (单张URL) |
| 响应编码 | 明文JSON | **Base64编码JSON** |
| 品类范围 | 全品类汽配 | **球笼/半轴专业厂商** |

---

## ADDED Requirements

### Requirement: VIN数据源抽象接口
系统SHALL定义 `IVinDataSource` 接口，抽象出数据源通用能力，使各平台实现解耦。

#### Scenario: 数据源接口定义
- **WHEN** 系统初始化VIN查询模块
- **THEN** 每个数据源实现 `IVinDataSource` 接口，提供：`SourceName`（数据源名称）、`IsLoggedIn`、`SendSmsAsync`、`LoginAsync`、`DecodeVinAsync`、`GetPartCardsAsync`、`RefreshTokenAsync`

### Requirement: 318car数据源重构
系统SHALL将现有 `VinQueryService` 重构为实现 `IVinDataSource` 接口，业务逻辑不变。

#### Scenario: 318car数据源兼容
- **WHEN** 重构 `VinQueryService` 实现 `IVinDataSource`
- **THEN** `SourceName` 返回 `"318car"`
- **AND** 所有现有API调用逻辑（登录、VIN解码、配件查询、Token刷新）保持不变
- **AND** Token持久化文件路径改为 `Data/vin_token_318car.json`（区分不同数据源）

### Requirement: 品秀传动数据源
系统SHALL新增 `PinxiuDataSource` 实现品秀传动(dataenlighten)平台的数据源，采用手机号验证码登录。

#### Scenario: 品秀发送验证码
- **WHEN** 用户在登录面板点击品秀数据源的发送验证码按钮
- **THEN** 调用 `POST /pdmPro/oauth/oauthSendSmsCodeValidate`，Body为 `{"phone":"<手机号>"}`
- **AND** 携带Headers: `company-id: MTEzMQ==`、`product-code: MKZ25`、`content-type: application/json`

#### Scenario: 品秀登录
- **WHEN** 用户输入验证码点击登录
- **THEN** 调用 `POST /pdmPro/oauth/loginOrRegSpUser`，Body为 `{"phone":"<手机号>","code":"<验证码>"}`
- **AND** 登录成功后解析响应获取Token
- **AND** Token持久化到 `Data/vin_token_pinxiu.json`

#### Scenario: 品秀VIN解码
- **WHEN** 用户发起VIN查询
- **THEN** 调用 `POST /pdmPro/aisearch/getAlphaRecommendVehicleList`
- **AND** Body包含 `keyword`(VIN码)、`addField:["brand","sub_brand","vehicle_group","displacement","engine"]`、`pageIndex:1`、`pageSize:10`
- **AND** 携带 `authorization: Bearer <token>` Header
- **AND** 响应需先Base64解码再解析JSON
- **AND** 提取 `mjsid` 字段（逗号分隔字符串）供后续查询使用

#### Scenario: 品秀配件查询
- **WHEN** VIN解码成功返回mjsid
- **THEN** 调用 `POST /pdmPro/sp/getProdListByVIN`
- **AND** Body为 `{"mjsids":["MJSxxx","MJSyyy"], "vin":"<VIN码>"}`
- **AND** 响应需先Base64解码再解析JSON
- **AND** 分类信息直接从 `cspuList` 中每个元素的 `categoryName`/`categoryId` 获取，无需额外调用分类接口
- **AND** 将 `cspuList` 映射为标准 `VinPartPageResult`：`cspuModel` → `Model`、`brandName` → `TenantBrandName`、`categoryName` → `TenantCategoryName`、`placesName` → `InstallationLocation`、`partNumber` → `PartNumber`、`vehicleComment` → `VehicleComment`、`pImage` → `ImgUrlList`(单元素数组)

#### Scenario: 品秀Base64响应解码
- **WHEN** 品秀API返回响应
- **THEN** 先对响应body进行Base64解码
- **AND** 再将解码后的字符串解析为JSON
- **AND** 检查 `code` 字段是否为 `"0000"`，非 `"0000"` 则视为查询失败

#### Scenario: 品秀数据源禁用开关
- **WHEN** `appsettings.json` 中 `Pinxiu:Enabled` 为 `false`
- **THEN** 不注册品秀数据源，不影响318car正常使用
- **AND** 默认值为 `true`（已拿到完整API文档）

### Requirement: 组合服务并行查询
系统SHALL通过 `CompositeVinQueryService` 组合服务，并行查询所有已登录的数据源，合并结果后统一返回。

#### Scenario: 并行查询所有已登录数据源
- **WHEN** 用户发起VIN查询
- **THEN** 同时对所有 `IsLoggedIn=true` 的数据源发起异步查询
- **AND** 使用 `Task.WhenAll` 并行等待
- **AND** 单个数据源查询失败不影响其他数据源返回

#### Scenario: 单数据源登录
- **WHEN** 仅有一个数据源处于登录状态
- **THEN** 仅查询该数据源，结果正常展示

#### Scenario: 所有数据源未登录
- **WHEN** 所有数据源均未登录
- **THEN** 提示用户至少登录一个数据源

#### Scenario: 某数据源查询超时或失败
- **WHEN** 并行查询中某个数据源超时或异常
- **THEN** 该数据源返回空结果，不阻塞其他数据源
- **AND** 在结果中标注该数据源查询失败的提示

### Requirement: 配件去重合并策略
系统SHALL对多数据源返回的配件按编码（Model/cspuModel）进行去重合并，去重依据为编码与本地数据库完全一致。

#### Scenario: 编码相同的配件合并
- **WHEN** 两个数据源返回的配件编码（Model字段）完全一致
- **THEN** 合并为一条配件记录
- **AND** 该配件标记来自多个数据源（`SourceName` 逗号分隔，如 "318car,品秀"）
- **AND** 保留每个数据源的价格信息供用户对比

#### Scenario: 编码不同的配件独立展示
- **WHEN** 不同数据源返回的配件编码不同
- **THEN** 各自独立展示，不合并
- **AND** 每条配件通过 `SourceName` 字段标识来源

#### Scenario: 同一配件多来源价格对比
- **WHEN** 编码相同的配件来自多个数据源
- **THEN** 配件卡片显示各来源的价格信息（标签+价格）
- **AND** 用户可对比不同来源的价格差异

### Requirement: 配件数据模型扩展
系统SHALL扩展 `VinPartCard` 实体，支持多数据源来源标识、OE号和车型备注。

#### Scenario: VinPartCard扩展字段
- **WHEN** 配件从任何数据源返回
- **THEN** `SourceName` 字段记录数据源名称
- **AND** `AlternateSources` 列表存储同编码配件来自其他数据源的数据
- **AND** `PartNumber` 字段记录OE号（品秀数据源特有，318car为空）
- **AND** `VehicleComment` 字段记录车型备注（品秀的vehicleComment，318car的notes）

### Requirement: 登录面板多数据源适配
系统SHALL在VIN查询窗口登录面板支持多数据源登录。

#### Scenario: 多数据源登录界面
- **WHEN** 用户打开VIN查询窗口且未全部登录
- **THEN** 登录面板列出所有数据源，每个数据源独立的手机号+验证码登录区
- **AND** 已登录的数据源显示"已登录"状态，可重新登录
- **AND** 至少一个数据源登录成功即可进入查询界面

#### Scenario: 快速登录（同手机号）
- **WHEN** 用户在某个数据源登录成功
- **THEN** 其他数据源的手机号输入框自动填充相同号码
- **AND** 用户只需输入验证码即可快速登录其他数据源

### Requirement: 配件列表来源标识展示
系统SHALL在配件适配列表中展示每条配件的数据来源。

#### Scenario: 配件卡片来源标识
- **WHEN** 配件列表渲染
- **THEN** 每个配件卡片显示数据来源标签（如"318car"、"品秀"）
- **AND** 多来源合并的配件显示所有来源标签

#### Scenario: 配件分类导航显示各来源统计
- **WHEN** 展示分类导航
- **THEN** 每个分类旁显示匹配数/总数（含各来源的匹配统计）

## MODIFIED Requirements

### Requirement: 318car API对接与Token管理
原有：VinQueryService直接实现IVinQueryService，Token持久化到 Data/vin_token.json。
修改：VinQueryService改为实现IVinDataSource，Token持久化到 Data/vin_token_318car.json。由CompositeVinQueryService统一对外提供IVinQueryService能力。

## REMOVED Requirements
无
