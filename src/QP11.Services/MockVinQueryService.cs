using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Services;

/// <summary>VIN查询服务 — Mock实现（开发调试用，无需318car API）</summary>
public class MockVinQueryService : IVinQueryService, IVinDataSource
{
    private bool _loggedIn;

    public bool IsLoggedIn => _loggedIn;
    public event EventHandler? LoginStatusChanged { add { } remove { } }
    public DateTime? GetTokenExpiryTime() => null;
    public event EventHandler? SourceStatusChanged { add { } remove { } }

    public Task<bool> SendSmsAsync(string phone, CancellationToken ct = default)
    {
        // Mock: 始终返回成功
        return Task.FromResult(true);
    }

    public Task<bool> LoginAsync(string phone, string smsCode, CancellationToken ct = default)
    {
        _loggedIn = true;
        return Task.FromResult(true);
    }

    public Task<VinDecodeResult?> DecodeVinAsync(string vin, CancellationToken ct = default)
    {
        return Task.FromResult<VinDecodeResult?>(new VinDecodeResult
        {
            Vin = vin,
            Brand = "大众",
            Manufacturers = "上海大众",
            Series = "朗逸",
            Models = "朗逸 1.4T",
            ChassisCode4 = "CSA",
            DisplacementWithT = "1.4T",
            EngineModel = "EA211",
            YearRange = "2023-2025",
            Generation = "3",
            VehicleAttributes = "合资",
            DriveModel = "前置前驱",
            TransmissionDescription = "双离合变速器(DCT)",
            BrandImg = "https://pandafunphoto.oss-cn-shanghai.aliyuncs.com/images/CarLOGO/DaZhong.png",
            ProductTime = "2023-06-01",
            VehicleIds = new List<string> { "mock_vehicle_id_1", "mock_vehicle_id_2" }
        });
    }

    public Task<VinPartPageResult?> GetPartCardsAsync(string vin, VinDecodeResult vehicleInfo, int page = 1, CancellationToken ct = default)
    {
        var result = new VinPartPageResult
        {
            Total = 6,
            Pages = 1,
            Current = 1,
            AdaptQueryRecordId = 100001,
            Categories = new List<VinPartCategoryGroup>
            {
                new VinPartCategoryGroup
                {
                    TenantCategoryId = 1,
                    CategoryName = "刹车片",
                    Products = new List<VinPartCard>
                    {
                        new VinPartCard
                        {
                            Id = 1, Name = "前刹车片", Model = "DZ-LY-001",
                            TenantBrandName = "博世", TenantCategoryName = "刹车片",
                            Notes = "朗逸1.4T[23-]", Unit = "套", Producer = "中国",
                            Price = 180, PurchasePrice = 120, GuidePrice = 180,
                            Stock = 5, ImgUrlList = new List<string>()
                        },
                        new VinPartCard
                        {
                            Id = 2, Name = "后刹车片", Model = "DZ-LY-002",
                            TenantBrandName = "博世", TenantCategoryName = "刹车片",
                            Notes = "朗逸1.4T[23-]", Unit = "套", Producer = "中国",
                            Price = 150, PurchasePrice = 95, GuidePrice = 150,
                            Stock = 3, ImgUrlList = new List<string>()
                        }
                    }
                },
                new VinPartCategoryGroup
                {
                    TenantCategoryId = 2,
                    CategoryName = "滤清器",
                    Products = new List<VinPartCard>
                    {
                        new VinPartCard
                        {
                            Id = 3, Name = "空气滤清器", Model = "DZ-LY-003",
                            TenantBrandName = "马勒", TenantCategoryName = "滤清器",
                            Notes = "朗逸1.4T[23-]", Unit = "个", Producer = "德国",
                            Price = 45, PurchasePrice = 28, GuidePrice = 45,
                            Stock = 10, ImgUrlList = new List<string>()
                        },
                        new VinPartCard
                        {
                            Id = 4, Name = "机油滤清器", Model = "DZ-LY-004",
                            TenantBrandName = "马勒", TenantCategoryName = "滤清器",
                            Notes = "朗逸1.4T[23-]", Unit = "个", Producer = "德国",
                            Price = 25, PurchasePrice = 15, GuidePrice = 25,
                            Stock = 20, ImgUrlList = new List<string>()
                        }
                    }
                },
                new VinPartCategoryGroup
                {
                    TenantCategoryId = 3,
                    CategoryName = "火花塞",
                    Products = new List<VinPartCard>
                    {
                        new VinPartCard
                        {
                            Id = 5, Name = "铱金火花塞", Model = "DZ-LY-005",
                            TenantBrandName = "NGK", TenantCategoryName = "火花塞",
                            Notes = "朗逸1.4T[23-]", Unit = "支", Producer = "日本",
                            Price = 35, PurchasePrice = 20, GuidePrice = 35,
                            Stock = 50, ImgUrlList = new List<string>()
                        },
                        new VinPartCard
                        {
                            Id = 6, Name = "普通火花塞", Model = "DZ-LY-006",
                            TenantBrandName = "NGK", TenantCategoryName = "火花塞",
                            Notes = "朗逸1.4T[23-]", Unit = "支", Producer = "日本",
                            Price = 15, PurchasePrice = 8, GuidePrice = 15,
                            Stock = 100, ImgUrlList = new List<string>()
                        }
                    }
                }
            }
        };

        return Task.FromResult<VinPartPageResult?>(result);
    }

    public Task<bool> RefreshTokenAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    // IVinQueryService 多数据源扩展方法（Mock实现）
    public string SourceName => "Mock";
    public List<IVinDataSource> GetLoggedInSources() => _loggedIn ? [this] : [];
    public List<IVinDataSource> GetAllSources() => [this];
    public Task StartupRefreshAsync() => Task.CompletedTask;
    public Task<bool> SendSourceSmsAsync(string sourceName, string phone, CancellationToken ct = default)
        => Task.FromResult(true);
    public Task<bool> LoginSourceAsync(string sourceName, string phone, string smsCode, CancellationToken ct = default)
    { _loggedIn = true; return Task.FromResult(true); }
}
