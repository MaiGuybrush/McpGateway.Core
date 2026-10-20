using McpGateway.Core.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using McpGateway.__Department__.Host.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog (讀取 appsettings.json 組態，支援 Console + 每日滾動檔案輸出)
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 1. 註冊 McpGateway 核心基礎設施 (認證、健康檢查、指標、連線階段隔離)
builder.Services.AddMcpGateway();

// 註冊宿主驗證用 Smoke Tool (HelloTool)
builder.Services.AddMcpServer().WithTools<HelloTool>();

// 2. 組裝各子系統模組 (由 add-module.ps1 自動注入)
// __SUBSYSTEM_REGISTRATION__

var app = builder.Build();

// 3. 掛載 Ingress 端點與啟動
app.MapMcpGateway();
await app.RunMcpGatewayAsync();
