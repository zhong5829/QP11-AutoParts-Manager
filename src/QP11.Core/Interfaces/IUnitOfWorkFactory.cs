namespace QP11.Core.Interfaces;

/// <summary>
/// 工作单元工厂 — 解耦 WPF 层对 QP11.Data.Infrastructure.UnitOfWork 具体类的直接依赖
/// </summary>
public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
