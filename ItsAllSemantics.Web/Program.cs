using ItsAllSemantics.Web.Components;
using ItsAllSemantics.Web.Hubs;
using ItsAllSemantics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

// Feature flag controls which responder is used
var useSk = builder.Configuration.GetValue<bool>("Features:UseSemanticKernel");
builder.Services.Configure<SemanticKernelOptions>(builder.Configuration.GetSection("SemanticKernel"));
if (useSk)
{
    builder.Services.AddSingleton<IChatResponder, SemanticKernelChatResponder>();
}
else
{
    builder.Services.AddSingleton<IChatResponder, EchoChatResponder>();
}

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
