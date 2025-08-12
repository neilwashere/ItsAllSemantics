using ItsAllSemantics.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ItsAllSemantics.Web.Services;

public sealed class SemanticKernelChatResponder : IChatResponder
{
    private readonly SemanticKernelOptions _options;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;

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
        _chat = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<ChatMessageModel> GetResponseAsync(string userMessage, CancellationToken ct = default)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("You are a helpful assistant. Be concise.");
        history.AddUserMessage(userMessage);

        var message = await _chat.GetChatMessageContentAsync(
            history,
            executionSettings: null,
            kernel: _kernel,
            cancellationToken: ct);

        var content = message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) content = "(No response)";
        return new ChatMessageModel(content, "ai", DateTimeOffset.Now);
    }
}

public sealed class SemanticKernelOptions
{
    public string Provider { get; set; } = "OpenAI"; // or AzureOpenAI
    public string Model { get; set; } = "gpt-4o-mini";
    public string Endpoint { get; set; } = string.Empty; // for AzureOpenAI
    public string ApiKey { get; set; } = string.Empty;
}
