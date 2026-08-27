using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Models;

namespace QP11.Core.Interfaces;

public interface ICalcService
{
    decimal CalculateLineSubtotal(decimal price, decimal amount, decimal discountRate);
    SellOrderSummary CalculateSellOrderSummary(IEnumerable<DetailSell> details, decimal orderDiscountRate, decimal yunfei = 0);
    decimal CalculateArrear(decimal totalPayment, PaymentInfo payment);
    void ValidateDiscountRate(ClientInfor client, decimal requestedDiscount);
}

public interface IAuthService
{
    Task<UserInfor?> LoginAsync(string username, string password);
    event EventHandler<UserInfor>? LoginSucceeded;
}

public interface ISerialNumberService
{
    Task<string> GenerateSellSN(IDbTransaction? transaction = null);
    Task<string> GenerateBuySN(IDbTransaction? transaction = null);
    Task<string> GenerateSellReturnSN(IDbTransaction? transaction = null);
    Task<string> GenerateBuyReturnSN(IDbTransaction? transaction = null);
    Task<string> GenerateSupplierSN(IDbTransaction? transaction = null);
    Task<string> GenerateWorkerSN(IDbTransaction? transaction = null);
    Task<string> GenerateClientSN(IDbTransaction? transaction = null);
    Task<string> GenerateShopSN(IDbTransaction? transaction = null);
    Task<string> GeneratePlanSN(IDbTransaction? transaction = null);
    Task<string> GenerateLogisticsCoSN(IDbTransaction? transaction = null);
    Task<string> GenerateShippingSN(IDbTransaction? transaction = null);
    Task<string> GenerateReceivingSN(IDbTransaction? transaction = null);
    Task<string> GenerateExchangeSN(IDbTransaction? transaction = null);
}
