using System;

namespace QP11.Core.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
    public BusinessRuleException(string message, Exception innerException) : base(message, innerException) { }
}

public class InsufficientStockException : BusinessRuleException
{
    public InsufficientStockException() : base("库存不足") { }
    public InsufficientStockException(string message) : base(message) { }
}

public class InvalidTransitionException : BusinessRuleException
{
    public InvalidTransitionException(string message) : base(message) { }
}
