# McpGateway.Core 套件文件

> 本檔為 Jenkins `dotnetPkgPipeline` 的版本檢查文件（Verify Documentation stage），內容須包含目前套件版本號。

## 目前版本

**0.2.5-preview**

| 版本 | 日期 | 說明 |
|---|---|---|
| 0.2.5-preview | 2026-10-27 | 輸出契約 Agent 層標準化（[Issue #15](issue/issue-15-output-contract-agent-layer-standardization.md)／[ADR-016](architecture/adr/ADR-016-agent-layer-output-contract-standardization.md)）：新增 `McpOutputContractAnalyzer`（MCP0013，tool 回傳可達閉包禁 `[JsonPropertyName]`，預設 suggestion 不擋建置）；`mcp-tool-dto-builder` Skill 補鏡射/透傳契約規則；template-module 改 Contracts/＋Dtos/ 三層契約佈局 |
| 0.2.4-preview | 2026-10-22 | 詞庫語意邊界治理（[Issue #14](issue/issue-14-vocabulary-semantic-boundary.md)）：startHours 移除 startTime 別名、startTime/startDate/endDate 收錄為獨立標準詞；35 條易混淆詞條補語意邊界；新增詞庫名稱一致性 CI 檢查；MCP0010/0011/0012 診斷中文化並附帶詞條描述（破壞性）；scaffold/add-module 腳手架工具鏈編碼與範本修復 |
| 0.2.3-preview | 2026-10-19 | 新增搬送/倉儲位置領域共通詞彙（zoneId/shelfId/patiId/position，見 [Issue #11](issue/issue-11-glossary-tier-governance.md) §6.7）；修正子系統腳手架範本殘留的舊版 Consul/Downstream 結構；修正 Consul Key 未設定時應跳過 Consul 查詢、不應使用寫死預設 Key 的不對稱回退邏輯 |
| 0.2.2-preview | 2026-10-19 | 詞彙治理單層化（[Issue #11](issue/issue-11-glossary-tier-governance.md)）：合併 Tier1/Tier2 雙層詞庫為單一 Tier1Vocabulary.cs 詞彙權威來源，移除孤兒 Tier 2 機制；修復 MockOcelotApi CS1998 警告 |
| 0.2.1-preview | 2026-09-29 | 修復 Redis 未配置時啟動崩潰（[Issue #9](issue/issue-9-redis-healthcheck-validation-crash.md)）：RedisHealthCheck 建構子參數改預設 null，移除 AddMcpGateway 中途 BuildServiceProvider() 反模式 |
| 0.2.0-preview | 2026-09-24 | ADR-015 單一套件正式打包：Core 執行期庫 + Analyzers 編譯期分析器（[ADR-015](architecture/adr/ADR-015-single-package-analyzers-merge.md)）；修復 Subsystem WithTools 重載誤綁 |
| 0.1.0-preview | 2026-09-24 | 首版發布（Analyzers 尚未併入，已自 feed 停用，請改升級 0.2.0-preview） |

## 套件內容（0.2.5-preview）

安裝 `McpGateway.Core` 即同時取得：

- `lib/net9.0/McpGateway.Core.dll` — 認證、稽核、驗證、傳輸等共用功能（見 [根 README](../README.md)）
- `analyzers/dotnet/cs/McpGateway.Analyzers.dll` — 命名規範與 schema description 的 Roslyn 分析器，編譯時自動生效，無需另外安裝

## 部門專案使用方式

```xml
<PackageReference Include="McpGateway.Core" Version="0.2.5-preview" />
```

## 建置與發佈

- Jenkins：repo 根目錄 `Jenkinsfile`（`dotnetPkgPipeline`，`packProjectName: 'McpGateway.Core'`）
- 發佈分支：`release/{version}`（分支版本須與 csproj `Version` 一致）
- 環境 SDK：`global.json` 精確比對政策（version `9.0.306`、`rollForward: disable`）
