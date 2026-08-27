using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace QP11.Services.AI;

public sealed class AgnesAuditor
{
    private static readonly object _lock = new();
    private readonly string _auditFile;

    public AgnesAuditor()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _auditFile = Path.Combine(dir, "agnes_audit.jsonl");
    }

    public Task RecordAsync(string username, string toolName, string argsJson, bool success, string resultSummary)
    {
        var entry = new
        {
            ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            user = username ?? "anonymous",
            tool = toolName,
            args = argsJson,
            success,
            result = resultSummary.Length > 500 ? resultSummary.Substring(0, 500) : resultSummary
        };

        lock (_lock)
        {
            try
            {
                var line = JsonSerializer.Serialize(entry);
                File.AppendAllText(_auditFile, line + Environment.NewLine);
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }
}
