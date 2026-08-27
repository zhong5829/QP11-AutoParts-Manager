# Tasks

- [x] Task 1: Core层 — 新增实体与接口
  - [x] 1.1: 创建 `QP11.Core/Entities/VinDecodeResult.cs` — VIN解码结果实体，字段对应318car API-1响应：Vin, Brand, Manufacturers, Series, Models, ChassisCode4, DisplacementWithT, EngineModel, YearRange, Generation, VehicleAttributes, BrandImg, ProductTime, VehicleIds(List<string>)
  - [x] 1.2: 创建 `QP11.Core/Entities/VinPartCard.cs` — 配件卡片实体，字段对应318car API-2响应empowerTenantProductList中的product：Id, Name, Model, ImgUrlList(List<string>), TenantBrandName, TenantCategoryName, Notes, Unit, Producer, Price, PurchasePrice, PurchaseGuidePrice, GuidePrice, CostPrice, Stock, InstallationLocation, ProductUnitList；以及本地补充字段：LocalPartId, LsPrice, PfPrice, StockAmount, IsLocalMatched
  - [x] 1.3: 创建 `QP11.Core/Interfaces/IVinQueryService.cs` — 接口定义：LoginAsync(phone, smsCode, ct), SendSmsAsync(phone, ct), DecodeVinAsync(vin, ct), GetPartCardsAsync(vin, vehicleInfo, page, ct), RefreshTokenAsync(ct)

- [x] Task 2: Services层 — 新增VIN查询服务实现
  - [x] 2.1: 创建 `QP11.Services/VinQueryService.cs` — 真实318car API实现：HttpClient调用mp.318car.com，手机号+短信验证码登录（sendSms/smsLogin），JWT Token管理（accessToken/refreshToken），401自动刷新重试，Tenant=226 Header，分页支持，VIN解码用URL query参数（?vin={vin}&tenantId=226）
  - [x] 2.2: 创建 `QP11.Services/MockVinQueryService.cs` — Mock实现：返回模拟车型和配件数据（参考318car真实响应结构），开发调试用

- [x] Task 3: WPF层 — 新增VinQueryWindow独立窗口（聊天式UI + 登录界面）
  - [x] 3.1: 创建 `QP11.Wpf/Views/VinQueryWindow.xaml` — 窗口布局：登录界面（手机号+发送验证码+验证码+登录按钮）和查询界面（顶部标题栏+中间对话区ScrollViewer+底部输入栏TextBox+发送按钮）两套视图，通过登录状态切换
  - [x] 3.2: 创建消息气泡模板：用户消息（右对齐蓝色气泡）、系统消息（左对齐灰色气泡，可内嵌车型信息+配件卡片列表）
  - [x] 3.3: 创建配件卡片DataTemplate：型号/名称/品牌/分类/售价/指导价/备注/匹配状态标识/[添加]按钮/图片缩略图
  - [x] 3.4: 创建 `QP11.Wpf/Views/VinQueryWindow.xaml.cs` — 窗口逻辑：登录流程（发送验证码60秒倒计时、登录切换视图）、VIN输入与发送、消息气泡渲染、车型信息展示、配件卡片列表渲染（按分类分组）、本地库存匹配、详情展开、添加到销售明细、分页加载更多、Token过期自动重登录

- [x] Task 4: MainWindow改造 — 菜单入口与窗口管理
  - [x] 4.1: 修改 `MainWindow.xaml` — 在"销售退货"菜单项后添加Separator和"VIN查询"菜单项（Tag="vin1"）
  - [x] 4.2: 修改 `MainWindow.xaml.cs` — OpenFunctionTab中新增"vin1"路由，VIN窗口单例管理（非模态Show），新增 `GetActiveSellControl()` 方法

- [x] Task 5: SellControl改造 — 支持从VIN窗口添加配件明细
  - [x] 5.1: 修改 `SellControl.xaml.cs` — 新增 `AddDetailFromVin()` 和 `GetCurrentClientName()` 公开方法

- [x] Task 6: DI注册与配置
  - [x] 6.1: 修改 `App.xaml.cs` — 根据 `VinQuery:UseMock` 配置注册 `MockVinQueryService` 或 `VinQueryService`
  - [x] 6.2: 修改 `appsettings.json` — 新增 `VinQuery` 配置节：ApiBaseUrl(https://mp.318car.com), TenantId(226), UseMock(true), Phone(15781805504), CacheExpirationMinutes(120), RequestTimeoutSeconds(10)

# Task Dependencies
- [Task 3] depends on [Task 1], [Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] 独立，可与Task 3/4并行
- [Task 6] depends on [Task 2]
