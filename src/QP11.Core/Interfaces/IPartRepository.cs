using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Models;

namespace QP11.Core.Interfaces;

public interface IPartRepository : IRepository<PartData>
{
    Task<PartData?> GetByIdAsync(long partid);
    Task<PagedResult<PartData>> GetPagedAsync(PartQueryCriteria criteria, int page = 1, int pageSize = 50);
    Task<IEnumerable<PartData>> SearchAsync(string keyword);
    new Task<int> InsertAsync(PartData entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(PartData entity);
    Task<int> LogicDeleteAsync(long partid);
    Task<int> IncreaseStockAsync(long partid, decimal quantity, IDbTransaction? transaction = null, IDbConnection? conn = null);
    Task<int> DecreaseStockAsync(long partid, decimal quantity, IDbTransaction? transaction = null, IDbConnection? conn = null);
    Task<PartStock?> GetStockByIdAsync(long partId, IDbTransaction? transaction = null, IDbConnection? conn = null);
    Task<IEnumerable<PartStockDisplay>> GetStockListAsync(string? keyword = null, int top = 0);
    Task<IEnumerable<PartStockDisplay>> GetStockListAdvancedAsync(string? partNo = null, string? partName = null, string? partNamePy = null, string? cartype = null, string? cartypePy = null, string? className = null, string? classPy = null, int queryMode = 3);
    /// <summary>标签打印数据查询：三条件（编码/名称/车型），按仓位、零件编码排序</summary>
    Task<IEnumerable<PartStockDisplay>> GetLabelItemsAsync(string? partNo = null, string? partName = null, string? cartype = null, int top = 0);
    /// <summary>按多个配件编号精确查询（用于多条件查询弹窗）</summary>
    Task<IEnumerable<PartStockDisplay>> GetStockListByCodesAsync(IEnumerable<string> partNos);
    Task<long> GetOrCreateWasteStockAsync(long originalPartId, int quantity, IDbTransaction? transaction = null, IDbConnection? conn = null);
    Task DecreaseWasteStockAsync(long originalPartId, int quantity, IDbTransaction? transaction = null, IDbConnection? conn = null);
    Task<Dictionary<long, PartData>> GetByIdsAsync(IEnumerable<long> partIds);
    Task<IEnumerable<StockAlertItem>> GetStockAlertItemsAsync();
    Task<int> UpdateWarningAsync(long partId, decimal warning);
    Task<List<PinyinFixRow>> GetMissingPinyinAsync();
    Task<int> UpdatePinyinAsync(long partId, string? namePy, string? cartypePy);
}
