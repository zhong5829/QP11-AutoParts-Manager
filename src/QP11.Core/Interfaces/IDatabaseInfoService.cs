namespace QP11.Core.Interfaces;

/// <summary>
/// 数据库信息服务 — 解耦 WPF 层对 DatabaseFactory 静态类的直接依赖
/// </summary>
public interface IDatabaseInfoService
{
    string Provider { get; }
    string ConnectionMode { get; }
    bool TestConnection(out string message);
}
