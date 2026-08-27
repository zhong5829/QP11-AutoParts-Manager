using System;
using System.Threading.Tasks;
using QP11.Core.Exceptions;
using QP11.Core.Interfaces;

namespace QP11.Services;

public class ValidationService : IValidationService
{
    private readonly IPartRepository _partRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IArrearageRepository _arrearRepo;

    public ValidationService(IPartRepository partRepo, IClientRepository clientRepo, IArrearageRepository arrearRepo)
    {
        _partRepo = partRepo;
        _clientRepo = clientRepo;
        _arrearRepo = arrearRepo;
    }

    public async Task ValidateStockAsync(long partId, decimal requiredAmount)
    {
        var part = await _partRepo.GetByIdAsync(partId);
        if (part == null) throw new BusinessRuleException($"配件ID {partId} 不存在");
        var stock = await _partRepo.GetStockByIdAsync(partId);
        if (stock == null || stock.Amount < (long)requiredAmount)
            throw new InsufficientStockException($"配件 {part.Name ?? partId.ToString()} 库存不足(库存:{stock?.Amount ?? 0}, 需要:{requiredAmount})");
    }

    public async Task ValidateClientCreditAsync(string clientId, decimal newArrear)
    {
        var client = await _clientRepo.GetByIdAsync(clientId);
        if (client == null) throw new BusinessRuleException($"客户 {clientId} 不存在");
        var credit = client.Credit ?? 0m;
        if (credit <= 0) return;
        var currentArrear = await _arrearRepo.GetClientArrearTotalAsync(clientId);
        if (currentArrear + newArrear > credit)
            throw new BusinessRuleException($"客户 {client.Name} 欠款将超限(额度:{credit:C2}, 已欠:{currentArrear:C2}, 新增:{newArrear:C2})");
    }

    public void ValidateDiscountRate(decimal rate)
    {
        if (rate <= 0 || rate > 1)
            throw new BusinessRuleException($"折扣率必须在0-1之间，当前值: {rate}");
    }

    public void ValidateAmount(decimal amount, string fieldName = "数量")
    {
        if (amount <= 0)
            throw new BusinessRuleException($"{fieldName}必须大于0");
    }

    public void ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException($"{fieldName}不能为空");
    }

    public void ValidateDateNotFuture(DateTime? date, string fieldName = "日期")
    {
        if (date.HasValue && date.Value.Date > DateTime.Now.Date)
            throw new BusinessRuleException($"{fieldName}不能是未来日期");
    }
}
