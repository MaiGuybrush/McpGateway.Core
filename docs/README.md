# McpGateway.Core 套件文件

> 本檔為 Jenkins `dotnetPkgPipeline` 的版本檢查文件（Verify Documentation stage），內容須包含目前套件版本號。

## 目前版本

**0.1.0-preview**

| 版本 | 日期 | 說明 |
|---|---|---|
| 0.1.0-preview | 2026-09-24 | 首版合併發布：Core 執行期庫 + Analyzers 編譯期分析器（[ADR-015](architecture/adr/ADR-015-single-package-analyzers-merge.md)） |

## 套件內容（0.1.0-preview）

安裝 `McpGateway.Core` 即同時取得：

- `lib/net9.0/McpGateway.Core.dll` — 認證、稽核、驗證、傳輸等共用功能（見 [根 README](../README.md)）
- `analyzers/dotnet/cs/McpGateway.Analyzers.dll` — 命名規範與 schema description 的 Roslyn 分析器，編譯時自動生效，無需另外安裝

## 部門專案使用方式

```xml
<PackageReference Include="McpGateway.Core" Version="0.1.0-preview" />
```

## 建置與發佈

- Jenkins：repo 根目錄 `Jenkinsfile`（`dotnetPkgPipeline`，`packProjectName: 'McpGateway.Core'`）
- 發佈分支：`release/{version}`（分支版本須與 csproj `Version` 一致）
- 環境 SDK：`global.json` 精確比對政策（version `9.0.306`、`rollForward: disable`）
