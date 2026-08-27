using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QP11.Core.AI;
using QP11.Services.AI;
using QP11.Services.AI.Abstractions;

namespace QP11.Wpf.ViewModels;

public sealed class AgnesChatViewModel : BaseViewModel
{
    private readonly AgnesOrchestrator _orchestrator;
    private readonly AgnesAuditor _auditor;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgnesOptions _options;
    private readonly List<ChatMessage> _history = new();
    private AgnesMessageItem? _currentAssistantItem;

    public ObservableCollection<AgnesMessageItem> Messages { get; } = new();

    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isOnline = true;
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (SetProperty(ref _isOnline, value))
                StatusText = value ? "在线" : "离线模式";
        }
    }

    public RelayCommand SendCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand ToggleOnlineCommand { get; }

    public AgnesChatViewModel(
        AgnesOrchestrator orchestrator,
        AgnesAuditor auditor,
        IToolRegistry toolRegistry,
        AgnesOptions options)
    {
        _orchestrator = orchestrator;
        _auditor = auditor;
        _toolRegistry = toolRegistry;
        _options = options;

        SendCommand = new RelayCommand(async () => await SendAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(InputText));
        ClearCommand = new RelayCommand(ClearHistory, () => !IsBusy);
        ToggleOnlineCommand = new RelayCommand(() => IsOnline = !IsOnline);

        AddSystemMessage($"Agnes 已就绪。供应商 {_options.Provider}，模型 {_options.Model}。输入问题即可查询配件、库存、价格与历史。");
    }

    private async Task SendAsync()
    {
        var input = (InputText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(input)) return;

        InputText = string.Empty;
        IsBusy = true;
        StatusText = "正在思考...";
        AddUserMessage(input);

        if (!IsOnline && _options.OfflineFallback)
        {
            await HandleOfflineAsync(input);
            IsBusy = false;
            StatusText = "离线模式";
            CommandManager.InvalidateRequerySuggested();
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        try
        {
            _currentAssistantItem = null;
            await foreach (var evt in _orchestrator.StreamConversationAsync(_history, input, cts.Token))
            {
                HandleEvent(evt);
            }
            TrimHistory();
            StatusText = "就绪";
        }
        catch (OperationCanceledException)
        {
            AddSystemMessage("请求超时，请检查网络或增大超时配置。");
            StatusText = "超时";
        }
        catch (Exception ex)
        {
            AddSystemMessage($"异常: {ex.Message}");
            StatusText = "异常";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void HandleEvent(AgnesEvent evt)
    {
        switch (evt)
        {
            case AgnesEvent.TextDelta d:
                if (_currentAssistantItem == null)
                {
                    _currentAssistantItem = new AgnesMessageItem { Role = "assistant" };
                    Dispatch(() => Messages.Add(_currentAssistantItem));
                }
                var snap = _currentAssistantItem;
                Dispatch(() => snap.AppendText(d.Text));
                break;

            case AgnesEvent.ToolCalling tc:
                _currentAssistantItem = null;
                Dispatch(() => Messages.Add(new AgnesMessageItem
                {
                    Role = "tool",
                    ToolName = tc.Name,
                    Text = $"调用工具 {tc.Name}..."
                }));
                break;

            case AgnesEvent.ToolResult tr:
                Dispatch(() => Messages.Add(new AgnesMessageItem
                {
                    Role = "tool",
                    ToolName = tr.Name,
                    Success = tr.Success,
                    Text = $"工具 {tr.Name} 返回（{(tr.Success ? "成功" : "失败")}）: {Summarize(tr.Result)}"
                }));
                _ = _auditor.RecordAsync(
                    App.CurrentUser?.Username ?? "anonymous",
                    tr.Name,
                    string.Empty,
                    tr.Success,
                    tr.Result);
                break;

            case AgnesEvent.Finished:
                _currentAssistantItem = null;
                break;

            case AgnesEvent.Error err:
                _currentAssistantItem = null;
                Dispatch(() => Messages.Add(new AgnesMessageItem { Role = "system", Text = $"错误: {err.Message}" }));
                break;
        }
    }

    private async Task HandleOfflineAsync(string input)
    {
        var keyword = input;
        var triggers = new[] { "查询", "查一下", "查", "库存", "有没有", "找" };
        foreach (var t in triggers)
        {
            if (keyword.StartsWith(t))
            {
                keyword = keyword.Substring(t.Length).Trim();
                break;
            }
        }
        if (string.IsNullOrEmpty(keyword)) keyword = input;

        var args = new JsonObject { ["keyword"] = keyword };
        AddSystemMessage($"离线模式：直接本地搜索「{keyword}」");
        var result = await _toolRegistry.DispatchAsync("search_parts", args);
        Dispatch(() => Messages.Add(new AgnesMessageItem
        {
            Role = "assistant",
            Text = FormatOfflineResult(result.Content)
        }));
        await _auditor.RecordAsync(App.CurrentUser?.Username ?? "anonymous", "search_parts(offline)",
            args.ToJsonString(), result.Success, result.Content);
    }

    private static string FormatOfflineResult(string content)
    {
        try
        {
            var node = JsonNode.Parse(content);
            if (node == null) return "无结果";
            var count = node["count"]?.GetValue<int>() ?? 0;
            if (count == 0) return "未找到匹配配件。";

            var sb = new System.Text.StringBuilder();
            sb.Append($"找到 {count} 条结果：\n");
            var items = node["items"]?.AsArray();
            if (items == null) return sb.ToString();
            int i = 1;
            foreach (var it in items)
            {
                sb.Append($"{i}. ");
                sb.Append(it?["partNo"]?.ToString() ?? "");
                sb.Append(" | ");
                sb.Append(it?["name"]?.ToString() ?? "");
                sb.Append(" | 车型:");
                sb.Append(it?["carType"]?.ToString() ?? "");
                sb.Append(" | 零售价:");
                sb.Append(it?["lsPrice"]?.ToString() ?? "0");
                sb.Append('\n');
                i++;
            }
            return sb.ToString();
        }
        catch
        {
            return Summarize(content);
        }
    }

    private static string Summarize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(空)";
        return s.Length > 300 ? s.Substring(0, 300) + "..." : s;
    }

    private void TrimHistory()
    {
        var max = _options.MaxHistoryMessages;
        while (_history.Count > max)
        {
            _history.RemoveAt(0);
        }
    }

    private void AddUserMessage(string text)
    {
        Dispatch(() => Messages.Add(new AgnesMessageItem { Role = "user", Text = text }));
    }

    private void AddSystemMessage(string text)
    {
        Dispatch(() => Messages.Add(new AgnesMessageItem { Role = "system", Text = text }));
    }

    private void ClearHistory()
    {
        _history.Clear();
        Messages.Clear();
        AddSystemMessage($"Agnes 已就绪。供应商 {_options.Provider}，模型 {_options.Model}。");
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
