using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;

namespace QP11.Services.AI.Abstractions;

public interface IChatClient
{
    IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
