# Tasks

- [x] Task 1: Core层 — 数据源接口与实体扩展
  - [x] 1.1: 新增 `QP11.Core/Interfaces/IVinDataSource.cs` — 数据源抽象接口，定义 SourceName、IsLoggedIn、SendSmsAsync、LoginAsync、DecodeVinAsync、GetPartCardsAsync、RefreshTokenAsync
  - [x] 1.2: 修改 `QP11.Core/Entities/VinPartCard.cs` — 增加 `SourceName` 字段（数据来源标识）、`AlternateSources` 字段（同编码多来源对比列表）、`PartNumber` 字段（OE号，品秀特有）、`VehicleComment` 字段（车型备注，统一318car的notes和品秀的vehicleComment）
  - [x] 1.3: 修改 `QP11.Core/Interfaces/IVinQueryService.cs` — 增加 `GetLoggedInSources()` 方法用于获取已登录数据源列表，增加 `LoginSourceAsync(sourceName, phone, smsCode)` 和 `SendSourceSmsAsync(sourceName, phone)` 用于指定数据源登录

- [x] Task 2: Services层 — 重构318car数据源
  - [x] 2.1: 修改 `QP11.Services/VinQueryService.cs` — 实现IVinDataSource接口，增加SourceName="318car"属性，Token文件路径改为 `Data/vin_token_318car.json`，保持全部业务逻辑不变，为返回的VinPartCard设置SourceName="318car"、VehicleComment=notes
  - [x] 2.2: 修改 `QP11.Services/MockVinQueryService.cs` — 适配IVinDataSource接口变更

- [x] Task 3: Services层 — 新增品秀传动数据源（完整实现）
  - [x] 3.1: 新增 `QP11.Services/PinxiuDataSource.cs` — 完整实现IVinDataSource接口，SourceName="品秀"
    - SendSmsAsync: POST /pdmPro/oauth/oauthSendSmsCodeValidate，Body={"phone":"xxx"}，Headers含company-id/product-code
    - LoginAsync: POST /pdmPro/oauth/loginOrRegSpUser，Body={"phone":"xxx","code":"yyy"}，Base64解码响应提取Token
    - DecodeVinAsync: POST /pdmPro/aisearch/getAlphaRecommendVehicleList，Body含keyword=VIN码+addField，Base64解码响应，提取mjsid和车型信息，映射到VinDecodeResult
    - GetPartCardsAsync: POST /pdmPro/sp/getProdListByVIN，Body={"mjsids":[...],"vin":"xxx"}，Base64解码响应，将cspuList映射为VinPartPageResult（分类信息直接从cspuList中每个元素的categoryName/categoryId获取，无需额外调用分类接口；cspuModel→Model, brandName→TenantBrandName, categoryName→TenantCategoryName, placesName→InstallationLocation, partNumber→PartNumber, vehicleComment→VehicleComment, pImage→ImgUrlList单元素数组）
    - RefreshTokenAsync: 清除Token返回false，让上层触发重新登录（品秀无refreshToken机制）
    - Token持久化到 Data/vin_token_pinxiu.json
    - 所有响应需先Base64解码再解析JSON，检查code="0000"
    - 为返回的VinPartCard设置SourceName="品秀"

- [x] Task 4: Services层 — 新增组合服务
  - [x] 4.1: 新增 `QP11.Services/CompositeVinQueryService.cs` — 实现IVinQueryService接口，注入所有IVinDataSource实现，并行查询已登录数据源（Task.WhenAll），按编码（Model字段）去重合并结果，单数据源失败不影响其他

- [x] Task 5: DI注册与配置
  - [x] 5.1: 修改 `QP11.Wpf/App.xaml.cs` — 注册所有IVinDataSource实现（VinQueryService、PinxiuDataSource），注册CompositeVinQueryService为IVinQueryService单例，PinxiuDataSource的注册受Pinxiu:Enabled配置控制
  - [x] 5.2: 修改 `QP11.Wpf/appsettings.json` — 新增 `Pinxiu` 配置节：ApiBaseUrl(https://api.dataenlighten.com:8045), CompanyId(MTEzMQ==), ProductCode(MKZ25), Phone(15781805504), RequestTimeoutSeconds(10), Enabled(true)

- [x] Task 6: WPF层 — 登录面板多数据源适配
  - [x] 6.1: 修改 `VinQueryWindow.xaml` — 登录面板改造为多数据源ItemsControl布局，每个数据源独立手机号+验证码+登录按钮，已登录显示状态标签
  - [x] 6.2: 修改 `VinQueryWindow.xaml.cs` — 登录逻辑适配多数据源（VinSourceLoginItem），某个数据源登录成功即允许查询，手机号自动填充到其他数据源输入框

- [x] Task 7: WPF层 — 配件列表来源标识展示
  - [x] 7.1: 修改配件卡片DataTemplate — 增加SourceName标签显示（蓝色圆角Border），OE号显示（橙色），合并配件显示多来源标签+多价格对比
  - [x] 7.2: 修改分类导航统计 — 已匹配数/总数展示保持现有逻辑（编码与本地一致，合并后不影响匹配统计）
  - [x] 7.3: 品秀配件卡片额外显示OE号(PartNumber)和安装位置(InstallationLocation)

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1]
- [Task 4] depends on [Task 2], [Task 3]
- [Task 5] depends on [Task 4]
- [Task 6] depends on [Task 5]
- [Task 7] depends on [Task 4]
- [Task 6] 和 [Task 7] 可并行
