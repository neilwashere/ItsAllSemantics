using ItsAllSemantics.Web.Components;
using ItsAllSemantics.Web.Hubs;
using ItsAllSemantics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents(options =>
{
    // Reduce render batching for real-time updates
    options.DetailedErrors = builder.Environment.IsDevelopment();
})
    .AddInteractiveServerComponents(options =>
    {
        // Configure for real-time streaming
        options.DetailedErrors = builder.Environment.IsDevelopment();
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
        options.MaxBufferedUnacknowledgedRenderBatches = 2; // Reduce buffering
    });
builder.Services.AddSignalR(options =>
{
    // Reduce buffering for real-time streaming
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = null; // Remove size limits
    options.StreamBufferCapacity = 1; // Minimize stream buffering
});

// Feature flag controls which responder is used
var useSk = builder.Configuration.GetValue<bool>("Features:UseSemanticKernel");
builder.Services.Configure<SemanticKernelOptions>(builder.Configuration.GetSection("SemanticKernel"));
builder.Services.AddSingleton<IChatExceptionTranslator, DefaultChatExceptionTranslator>();
if (useSk)
{
    builder.Services.AddSingleton<IChatResponder, SemanticKernelChatResponder>();
}
else
{
    builder.Services.AddSingleton<IChatResponder, EchoChatResponder>();
}
builder.Services.AddSingleton<IChatStreamManager, ChatStreamManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
