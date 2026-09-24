# ADR-015: Core 單一套件合併發布 Analyzers（Single-Package Distribution）

## 狀態
**✅ 已提議 / 達成共識 (Proposed / Consensus Reached)**（2026-09-24）

---

## 背景 (Context)

依 [ADR-009](ADR-009-department-gateway-split.md) 架構，各部門 Gateway（Report、SPC、QC）以 NuGet 套件 `McpGateway.Core` 為共用基礎，並依 [ADR-014](ADR-014-intra-department-subsystem-modules.md) 與命名規範開發子系統模組。`McpGateway.Analyzers`（Roslyn Analyzers + CodeFixes，強制命名規範與 schema description 驗證）目前為**獨立套件**（`PackageId=McpGateway.Analyzers`）。

### 現況痛點

1. **部署成本翻倍**：部門專案要同時安裝 `McpGateway.Core` 與 `McpGateway.Analyzers` 兩個套件，才能同時取得執行期函式庫與編譯期規範檢查；漏裝 Analyzers 時規範失效且無警告。
2. **版本漂移風險**：兩套件獨立發布時，部門專案可能 Core 新版、Analyzers 舊版（或反之），規則與實作不一致。
3. **CI 限制**：Jenkins `dotnetPkgPipeline` 一次只 pack 一個 csproj、上傳單一 nupkg（倉庫慣例「Package project cannot contain ProjectReference」），兩套件需兩條 pipeline 或兩次觸發。

## 決策 (Decision)

**將 `McpGateway.Analyzers.dll` 合併進 `McpGateway.Core` 單一套件發布**，以 `None + Pack="true"` 直接打包兄弟專案的編譯輸出，**不使用 `ProjectReference`**。

`src/McpGateway.Core/McpGateway.Core.csproj`：

```xml
<ItemGroup>
  <!-- 合併打包 Analyzers（免 ProjectReference）：pipeline 先 build 整個 sln，pack --no-build 時 dll 已存在 -->
  <None Include="..\McpGateway.Analyzers\bin\$(Configuration)\netstandard2.0\McpGateway.Analyzers.dll"
        Pack="true" PackagePath="analyzers/dotnet/cs" />
</ItemGroup>
```

產出套件結構：

```
McpGateway.Core.<version>.nupkg
├── lib/net9.0/McpGateway.Core.dll          ← 執行期函式庫（消費端自動參考）
├── analyzers/dotnet/cs/McpGateway.Analyzers.dll  ← 編譯期分析器（編譯器自動載入）
└── README.md
```

### 關鍵技術依據

| 論點 | 說明 |
|---|---|
| **ProjectReference 不會嵌入 dll** | `dotnet pack` 對 ProjectReference 的預設行為是把它列為「套件依賴」，dll **不**進 package。「寫了 ProjectReference 使用者就會參考到那些 dll」是誤解。 |
| **套件內容由 Pack 位置決定** | dll 放 `lib/<tfm>/` → 被當函式庫參考；放 `analyzers/dotnet/cs/` → 被編譯器當 Roslyn 分析器載入。這是 Microsoft.CodeAnalysis.* 等業界套件的標準打包方式。 |
| **免 ProjectReference 可行** | CI（`dotnetPkgPipeline`）先 `dotnet build` 整個 sln（所有專案 dll 已產出），再 `dotnet pack --no-build` 單一 csproj；`None Include` 直接抓 disk 上的 dll，不需要專案參考保證建置順序。 |
| **規避 pipeline 限制** | 不加 ProjectReference → 不觸發 `dotnetPkgPipeline` 的「Package project cannot contain ProjectReference」檢查，CI 零修改（僅需 issue #64 的子目錄 csproj 定位修正）。 |

### 替代方案（不採用）

| 方案 | 拒絕理由 |
|---|---|
| 維持兩套件獨立發布 | 痛點 1、2 不變，且需兩條 Jenkins pipeline |
| `ProjectReference` + `ReferenceOutputAssembly=false` + `PrivateAssets=all` | 可保證建置順序且不進 nuspec，但觸發 pipeline 的 ProjectReference 禁令，需鬆綁 `parseCsprojInfo` 政策（跨倉庫協調成本高）；CI 已先 build sln，順序保證並非必要 |
| 多專案 `dotnet pack` 整個 sln 後合併 nupkg | 工具鏈複雜化（需自寫 nuspec 合併），偏離 pipeline 單一 pack 慣例 |

## 後果 (Consequences)

### 正面

- 部門專案只需 `Install-Package McpGateway.Core`，即同時取得執行期庫 + 命名規範分析器，**無法漏裝**。
- 版本單一化：Analyzers 規則與 Core 實作同版本發布、同 rollback。
- CI 維持單條 `dotnetPkgPipeline`（`useSolutionFile` + `packProjectName: 'McpGateway.Core'`）。

### 負面 / 風險

1. **直接 build Core.csproj（繞過 sln）時**，Analyzers 可能未先編譯 → pack 報「檔不存在」。緩解：CI 走 sln 不受影響；本地開發以 sln 為建置入口（IDE 預設）。
2. **Analyzers 無法單獨升級**：綁死 Core 版本。評估可接受——規則本就隨 Core 演進。
3. **合併範圍紀律**（重要）：僅 **compile-time only**（analyzers）或**無外部 NuGet 依賴**的專案可如此合併。具自身 NuGet 依賴的一般 class library **不得**以 `None Pack` 方式塞入 `lib/`——其依賴不會流動到 nuspec，消費端執行期會 `MissingReferenceException`。此為本 ADR 的邊界條件。
4. `McpGateway.Analyzers` 專案本身**保留**（sln 內仍編譯、測試仍跑 `McpGateway.Analyzers.Tests`），只是不再獨立發布 NuGet；其 csproj 的 `IsPackable` 改為 `false` 避免誤產出。

## 實施項目

- [x] `src/McpGateway.Core/McpGateway.Core.csproj`：加入 `None Pack="true"` 打包 Analyzers dll
- [x] `src/McpGateway.Analyzers/McpGateway.Analyzers.csproj`：`IsPackable` 改 `false`
- [x] 本地 `dotnet build` sln + `dotnet pack` 驗證套件含 `analyzers/dotnet/cs/McpGateway.Analyzers.dll`
- [ ] 部門專案端驗證：安裝新 Core 套件後命名規範警告生效
- [ ] `docs/README.md`（pipeline 版本檢查要求）與本 ADR 同步版本號

## 相關

- jenkins-pipeline-library issue #64（`dotnetPkgPipeline` 子目錄 csproj 定位）
- ADR-009（部門拆分）、ADR-014（子系統模組化）、wayfinder ticket 005（Roslyn 命名規則）
