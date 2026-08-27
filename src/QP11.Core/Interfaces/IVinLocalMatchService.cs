using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

/// <summary>VIN配件本地库存匹配服务 — 将外部数据源配件与本地part_data/part_stock进行模糊匹配</summary>
public interface IVinLocalMatchService
{
    /// <summary>对配件列表执行本地库存匹配，直接修改cards的LocalXxx字段</summary>
    Task EnrichWithLocalDataAsync(IEnumerable<VinPartCard> cards, VinDecodeResult vehicleInfo);
}
