using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

/// <summary>
/// 计划订货服务接口
/// </summary>
public interface IJhdhService
{
    /// <summary>
    /// 创建计划订货单
    /// </summary>
    Task<string> CreatePlanOrderAsync(BillJhdh bill, List<DetailJhdh> details);

    /// <summary>
    /// 更新计划订货单（单头+明细）
    /// </summary>
    Task UpdatePlanOrderAsync(BillJhdh bill, List<DetailJhdh> details);

    /// <summary>
    /// 将计划单转为采购入库单（核心流程）：
    /// 1. 生成 bill_buy + detail_buy
    /// 2. 增加库存
    /// 3. 记录欠款
    /// 4. 更新 jhdh flag = 1（已执行）
    /// </summary>
    Task<string> ConvertToBuyOrderAsync(string jhdhSn, List<DetailBuy> buyDetails, decimal cash = 0, decimal credit = 0);

    /// <summary>
    /// 作废计划单（flag = 3）
    /// </summary>
    Task CancelPlanOrderAsync(string sn);
}
