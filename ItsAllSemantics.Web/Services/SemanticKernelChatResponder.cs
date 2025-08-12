using ItsAllSemantics.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Text;

namespace ItsAllSemantics.Web.Services;

public sealed class SemanticKernelChatResponder : IChatResponder
{
    private readonly SemanticKernelOptions _options;
    private readonly Kernel _kernel;
    private readonly ChatCompletionAgent _agent;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AgentThread> _threads = new();

    public SemanticKernelChatResponder(IOptions<SemanticKernelOptions> options)
    {
        _options = options.Value;
        var builder = Kernel.CreateBuilder();
        if (string.Equals(_options.Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: _options.Model,
                endpoint: _options.Endpoint,
                apiKey: _options.ApiKey);
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: _options.Model,
                apiKey: _options.ApiKey);
        }
        _kernel = builder.Build();

        _agent = new ChatCompletionAgent
        {
            Name = "AImee",
            Instructions = "You are a concise, helpful assistant. Keep answers short unless asked to elaborate.",
            Kernel = _kernel
        };
    }

    private AgentThread GetOrCreateThread(string sessionId)
    {
        return _threads.GetOrAdd(sessionId, static _ =>
            new ChatHistoryAgentThread([new ChatMessageContent(AuthorRole.System, "You are a helpful assistant. Be concise.")]));
    }

    public async Task<ChatMessageModel> GetResponseAsync(string userMessage, string sessionId, CancellationToken ct = default)
    {
        var thread = GetOrCreateThread(sessionId);

        var user = new ChatMessageContent(AuthorRole.User, userMessage);

        ChatMessageContent? last = null;
        await foreach (var response in _agent.InvokeAsync(user, thread, options: null, cancellationToken: ct))
        {
            last = response;
        }

        var content = last?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) content = "(No response)";
        return new ChatMessageModel(content, _agent.Name ?? "ai", DateTimeOffset.Now);
    }

    public void RemoveSession(string sessionId)
    {
        _threads.TryRemove(sessionId, out _);
    }
}

public sealed class SemanticKernelOptions
{
    public string Provider { get; set; } = "OpenAI"; // or AzureOpenAI
    public string Model { get; set; } = "gpt-4o-mini";
    public string Endpoint { get; set; } = string.Empty; // for AzureOpenAI
    public string ApiKey { get; set; } = string.Empty;
}
