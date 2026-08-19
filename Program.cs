using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ApimReplica.Data;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using ApimReplica.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddReverseProxy().LoadFromMemory([], []);
builder.Services.AddSingleton<ProxyConfigService>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddRateLimiter(options =>
{
    // Partitioned by caller: a single global bucket let one client exhaust the
    // quota of everyone else hitting the gateway.
    options.AddPolicy("proxy", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddHostedService<HealthCheckService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ProxyConfigService>().ReloadAsync();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapReverseProxy().RequireRateLimiting("proxy");
app.MapGet("/health", () => new { status = "ok", time = DateTime.UtcNow });
app.MapControllers();

app.Run();