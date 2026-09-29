# Issue #9 - TokenCache ConnectionString 空字串時 RedisHealthCheck 無法解析 IConnectionMultiplexer 導致啟動崩潰

> **Issue 連結**: http://tncimweb.cminl.oa/git-server/CIM-1-3-1/McpGateway.Core/issues/9
> **建立日期**: 2026-09-29
> **類型**: Bug Fix

## 1. 問題描述

`AddMcpGateway()` 內 Redis 相關服務註冊不一致：當 `McpGateway:TokenCache:ConnectionString` 為空字串（未配置 Redis）時，`IConnectionMultiplexer` 不會被註冊，但 `RedisHealthCheck` / `GatewayReadinessHealthCheck` 無條件註冊，且建構子硬依賴 `IConnectionMultiplexer`，導致 `ValidateOnBuild`（ASP.NET Core Development 環境預設開啟）在啟動時直接拋出 `AggregateException` 崩潰。

**重現方式**：

1. `appsettings.json` 設定 `"McpGateway": { "TokenCache": { "ConnectionString": "" } }`
2. `ASPNETCORE_ENVIRONMENT=Development` 啟動任意宿主（如 `McpGateway.Qsys.Host`）

**症狀**：

```
System.AggregateException: Some services are not able to be constructed
(Error while validating the service descriptor
'ServiceType: McpGateway.Core.Observability.RedisHealthCheck ...':
Unable to resolve service for type 'StackExchange.Redis.IConnectionMultiplexer' ...)
```

**目前下游 workaround**：`McpGateway.Qsys.Host` 的 `appsettings.Development.json` 以 `"ConnectionString": "localhost:6379,abortConnect=false"` 保證非空繞過。Core 修復後可移除。

## 2. 根本原因分析

### 2.1 服務註冊順序矛盾（`McpGatewayHostExtensions.cs` 約 69-135 行）

```csharp
// 無條件註冊：建構子需要 IConnectionMultiplexer
services.AddTransient<RedisHealthCheck>();
services.AddTransient<GatewayReadinessHealthCheck>();

// 但只有連線字串非空時才註冊 IConnectionMultiplexer
if (!string.IsNullOrEmpty(tokenCache?.ConnectionString))
{
    services.AddSingleton<IConnectionMultiplexer>(...);
    ...
}
else
{
    // 空字串走這裡：IConnectionMultiplexer 完全未註冊
}
```

### 2.2 `RedisHealthCheck` 的 nullable 參數並不能讓 DI 視為「可選」

`RedisHealthCheck.cs` 建構子雖標註為 nullable：

```csharp
public RedisHealthCheck(IConnectionMultiplexer? redisConnection, ILogger<RedisHealthCheck> logger)
```

但 .NET DI 容器（`Microsoft.Extensions.DependencyInjection`）在解析建構子參數時，**nullable reference type 標註不影響服務是否為必要**——只有參數具備 `= null` 預設值，或改用 `IServiceProvider.GetService<T>()` 手動解析，才會被視為可選依賴。因此當 `IConnectionMultiplexer` 未註冊、且啟用 `ValidateOnBuild = true` 時，容器驗證階段即失敗，錯誤發生在應用程式啟動當下，而非執行期呼叫健康檢查時。

### 2.3 連鎖影響

`GatewayReadinessHealthCheck` 建構子依賴 `RedisHealthCheck`，因此同一原因會連帶驗證失敗，兩者皆列在 `AggregateException` 中。

### 2.4 附帶問題：`ConfigureServices` 中途呼叫 `BuildServiceProvider()`

```csharp
var tokenCache = services.BuildServiceProvider().GetRequiredService<IOptions<McpGatewayOptions>>().Value.TokenCache;
```

在服務註冊過程中呼叫 `BuildServiceProvider()` 會建立一個暫時的獨立容器，其中建立的實例（例如 `IOptions<McpGatewayOptions>` 背後的設定繫結流程）與最終正式容器中的實例並非同一份，屬於重複建置的反模式（temporary service provider anti-pattern）。慣例作法是直接讀取傳入的 `IConfiguration`/`IConfigurationSection`，或延後到 factory lambda（`sp => ...`）內以 `sp.GetRequiredService<...>()` 解析。此問題目前未造成觀察到的錯誤，但建議與本次修復一併整理，降低未來排查成本。

## 3. 修復方案

### 3.1 `RedisHealthCheck` 改為容忍 Redis 未配置（最小範圍修改）

保留無條件註冊 `RedisHealthCheck` / `GatewayReadinessHealthCheck`（維持 `/health` 端點結構一致），但讓 `IConnectionMultiplexer` 缺席時仍可正常建構：

```csharp
public RedisHealthCheck(IConnectionMultiplexer? redisConnection = null, ILogger<RedisHealthCheck>? logger = null)
```

給予明確的 `= null` 預設值，讓 DI 容器將該參數視為可選依賴（而非僅靠 nullable 標註）。搭配 `services.AddTransient<RedisHealthCheck>()` 註冊不變，`ValidateOnBuild` 便不會再因缺少 `IConnectionMultiplexer` 而失敗；執行期邏輯本就已處理 `_redisConnection == null` 情境（回傳 `Healthy("Redis not configured (in-memory cache used)")`），無需變更。

### 3.2 `GatewayReadinessHealthCheck` 同步確認

`GatewayReadinessHealthCheck` 本身不直接依賴 `IConnectionMultiplexer`，只依賴 `RedisHealthCheck`，因此 3.1 修復後其驗證鏈亦隨之解除。不需額外修改建構子。

### 3.3 修正 `BuildServiceProvider()` 反模式

移除 `AddMcpGateway()` 中段的臨時容器建置，改為在後續各個 factory lambda 內各自解析：

```csharp
// 修改前
var tokenCache = services.BuildServiceProvider().GetRequiredService<IOptions<McpGatewayOptions>>().Value.TokenCache;
if (!string.IsNullOrEmpty(tokenCache?.ConnectionString)) { ... }

// 修改後：改用 IConfiguration 直接讀取繫結前的原始設定值以決定分支
var tokenCacheConnectionString = configuration.GetSection("McpGateway:TokenCache:ConnectionString").Value;
if (!string.IsNullOrEmpty(tokenCacheConnectionString))
{
    services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<McpGatewayOptions>>().Value;
        var connectionString = options.TokenCache?.ConnectionString;
        ...
    });
}
```

需將 `IConfiguration` 作為 `AddMcpGateway(this IServiceCollection services, IConfiguration configuration)` 的參數傳入，或由呼叫端在註冊前先行 `services.AddSingleton<IConfiguration>(configuration)` 並於此處 `services.BuildServiceProvider()` 之前透過既有的 `IConfiguration` 執行個體讀取（避免建立臨時容器）。實際簽章變更需確認所有既有呼叫端（各 Host 專案）同步調整。

## 4. 影響範圍

| 檔案 | 類型 | 說明 |
|------|------|------|
| `src/McpGateway.Core/Observability/RedisHealthCheck.cs` | 修改 | 建構子參數加上 `= null` 預設值，使 DI 視為可選依賴 |
| `src/McpGateway.Core/Hosting/McpGatewayHostExtensions.cs` | 修改 | 移除 `ConfigureServices` 中途 `BuildServiceProvider()`，改由 `IConfiguration` 或 factory lambda 解析 `TokenCache.ConnectionString` |
| `tests/McpGateway.Core.IntegrationTests/Issue9RedisHealthCheckValidationTests.cs` | 既有 | 已涵蓋空字串／`null` 兩種情境的 `ValidateOnBuild` 重現測試，修復後應轉為通過 |
| 各 Host 專案（如 `McpGateway.Qsys.Host`）的 `appsettings.Development.json` | 可選清理 | Core 修復後可移除 `localhost:6379,abortConnect=false` 的 workaround |

## 5. 風險評估

| 風險 | 影響 | 緩解措施 |
|------|------|---------|
| `RedisHealthCheck` 建構子簽章加上預設值 | 對既有呼叫端無破壞性（仍相容既有明確傳參用法） | 僅新增預設值，不移除既有參數 |
| `AddMcpGateway` 若改為需要 `IConfiguration` 參數 | 呼叫端（各 Host `Program.cs`）需同步調整呼叫方式 | 若採此路線需檢查所有 Host 專案並同步更新；亦可改為不變更公開簽章，僅在既有 `services.BuildServiceProvider()` 呼叫點以 try/finally 顯式 `Dispose()` 暫時容器降低資源殘留風險，作為次要備案 |
| Redis 未配置時健康檢查恆為 `Healthy` | 掩蓋預期使用 Redis但忘記設定的情境 | 現行行為已是既有設計（`in-memory cache used` 視為正常降級），本次不變更此語意 |

## 6. 驗收標準

- [ ] `Issue9RedisHealthCheckValidationTests.AddMcpGateway_WithEmptyRedisConnectionString_PassesValidateOnBuild` 通過
- [ ] `Issue9RedisHealthCheckValidationTests.AddMcpGateway_WithNullRedisConnectionString_PassesValidateOnBuild` 通過
- [ ] `ASPNETCORE_ENVIRONMENT=Development`、`TokenCache.ConnectionString` 為空字串時，任意 Host 啟動不再拋出 `AggregateException`
- [ ] `/health` 各端點（`gateway_live` / `gateway_ready`）在 Redis 未配置時回傳 `Healthy`，訊息包含 "Redis not configured"
- [ ] 移除 `ConfigureServices` 中途 `BuildServiceProvider()` 後，既有測試（含 Subsystem/Auth/Downstream 相關整合測試）全數通過
- [ ] 下游 `McpGateway.Qsys.Host` 的 Redis workaround 設定可安全移除並驗證仍正常啟動
