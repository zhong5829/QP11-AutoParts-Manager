namespace QP11.Core.Constants;

/// <summary>
/// 业务常量定义 — 替代代码中散布的 magic number
/// </summary>
public static class BusinessConstants
{
    /// <summary>
    /// 单据状态（flag 字段）
    /// </summary>
    public enum BillFlag
    {
        Deleted = -1,
        Draft = 0,
        Confirmed = 1,
        Returned = 2,
        Voided = 3
    }

    /// <summary>
    /// 报损单固定客户 cid（client_infor 中 name='配件报损' 的特殊客户）
    /// 数据库实证：2018-03~2023-07-19 用 02288（1653单），2023-07-19 至今用 03136（916单）
    /// </summary>
    public const string BaosunClientId = "03136";

    /// <summary>
    /// 欠款类型（arrearage.type 字段）
    /// </summary>
    public static class ArrearType
    {
        public const int Buy = 1;
        public const int Sell = 2;
    }

    /// <summary>
    /// 欠款业务类型（arrearage.btype 字段）
    /// </summary>
    public static class ArrearBtype
    {
        public const int Buy = 1;
        public const int Sell = 2;
    }

    /// <summary>
    /// 明细类型（detail_sell.type / detail_buy.type 字段）
    /// </summary>
    public static class DetailType
    {
        public const int Normal = 0;
        public const int Gift = 1;
    }

    /// <summary>
    /// 采购单据状态标志
    /// </summary>
    public enum BuyFlag
    {
        Unsettled = 0,   // 未结算
        Settled = 1      // 已结算
    }

    /// <summary>
    /// 单据状态文本映射
    /// </summary>
    public static string GetFlagText(int flag) => flag switch
    {
        (int)BillFlag.Draft => "草稿",
        (int)BillFlag.Confirmed => "已审核",
        (int)BillFlag.Returned => "退货",
        (int)BillFlag.Voided => "已作废",
        (int)BillFlag.Deleted => "已删除",
        _ => "未知"
    };
}
