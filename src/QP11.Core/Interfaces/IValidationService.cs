using System;
using System.Threading.Tasks;
using QP11.Core.Exceptions;

namespace QP11.Core.Interfaces;

public interface IValidationService
{
    Task ValidateStockAsync(long partId, decimal requiredAmount);
    Task ValidateClientCreditAsync(string clientId, decimal newArrear);
    void ValidateDiscountRate(decimal rate);
    void ValidateAmount(decimal amount, string fieldName = "数量");
    void ValidateRequired(string value, string fieldName);
    void ValidateDateNotFuture(DateTime? date, string fieldName = "日期");
}
