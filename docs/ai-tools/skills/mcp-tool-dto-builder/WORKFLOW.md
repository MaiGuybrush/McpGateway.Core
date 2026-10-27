# MCP Tool DTO Builder — 技術規格與自動化手冊 (Technical Specification & Reference)

本文件作為 `mcp-tool-dto-builder` Skill 的深度技術規格文件，記載 Step 0 詞庫探測演算法、Roslyn 診斷碼規則、以及 Step 5 中央 Gitea PR 自動化提報機制。

---

## 1. 架構與生命週期總覽 (Architecture Overview)

```mermaid
flowchart TD
    subgraph S0["Step 0: Probe Phase"]
        A1["工作區 Glob 掃描"] --> A2{"找到 Tier1Vocabulary.cs?"}
        A2 -- 是 --> L1["載入標準詞庫"]
        A2 -- 否 --> B1[".csproj 專案/套件引用解析"]
        B1 --> B2{"本機相對引用存在?"}
        B2 -- 是 --> L1
        B2 -- 否 --> C1["Gitea / tea CLI 下載 Tag 至 .scratch/"]
        C1 --> L1
    end

    subgraph S14["Step 1-4: Contract Generation"]
        L1 --> D1["Ingest 原始欄位"]
        D1 --> D2["Canonicalize 詞庫歸一化"]
        D2 --> D3["Interactive Gate 對齊確認"]
        D3 --> D4["Emit 結構化 C# Record DTO"]
    end

    subgraph S5["Step 5: Contribution PR"]
        D4 --> E1{"存在新別名/新標準詞?"}
        E1 -- 是 --> E2["前置衝突與唯一性檢核"]
        E2 --> E3["Diff 預覽與開發者確認"]
        E3 --> E4["依環境執行 Git / tea CLI PR 建立"]
    end
```

---

## 2. Step 0: 動態解析 Tier 1 詞庫探測機制 (Probe Strategy)

為確保 AI Agent 取得最新且權威的製造業核心詞彙，採用雙層動態探測策略（2-Tier Probe Strategy）：

### 2.1 探測優先順序 (Probe Priority)

| 優先級 | 探測階段 | 目標路徑 / 搜尋樣式 | 說明 |
| :--- | :--- | :--- | :--- |
| **P1** | 工作區 Glob | `**/Tier1Vocabulary.cs` | 搜尋本機工作區原始碼（適用於 Central Core 開發環境） |
| **P2** | ProjectReference 解析 | `.csproj` / `.sln` 中 `<ProjectReference Include="..." />` | 解析跨專案相對路徑上游原始碼 |
| **P3** | PackageReference 版號對照 | `<PackageReference Include="McpGateway.Analyzers" Version="vX.Y.Z" />` | 取得 NuGet/Gitea Package Tag 版本 |
| **P4** | Gitea 遠端下載 | Gitea 預設庫設定或開發者提供之 repositoryUrl | 使用 `tea` CLI / Git 擷取至 `.scratch/tier1-vocab/` |
| **P5** | 終端提示 (Fallback) | Prompt Developer | 提示開發者提供本機路徑或 Gitea URL |

### 2.2 Gitea CLI (`tea`) 下載指令範例
```powershell
# 建立暫存目錄
if (-not (Test-Path ".scratch\tier1-vocab")) { New-Item -ItemType Directory -Path ".scratch\tier1-vocab" -Force }

# 擷取特定 Tag 之 Tier1Vocabulary.cs
tea repo clone guy.mai/McpGateway.Core .scratch/tier1-vocab/repo --branch <VersionTag>
Copy-Item ".scratch\tier1-vocab\repo\src\McpGateway.Analyzers\Tier1Vocabulary.cs" ".scratch\tier1-vocab\Tier1Vocabulary.cs"
```

---

## 3. 詞庫比對與 Roslyn 診斷碼對照 (Diagnostics Reference)

在 Step 2 進行詞彙歸一化與 Step 4 產出 DTO 時，遵循以下 Roslyn Analyzer 規則：

| 診斷代碼 | 嚴重性 | 觸發條件 | 修正行為 |
| :--- | :--- | :--- | :--- |
| **`MCP0010`** | Warning | 使用了已知歷史別名（如 `ProductCode`, `prodId`, `wipQty`） | 建議更換為標準名稱（如 `ProductId`, `QuantityInProcess`） |
| **`MCP0011`** | Warning | DTO 公開屬性遺漏 `[Description]` 特性 | 補齊繁體中文業務描述與格式範例 |
| **`MCP0012`** | Info | 屬性或參數包含底線（如 `PROD_ID`, `work_order`） | 調整為標準 CamelCase / PascalCase |
| **`MCP0013`** | Info（預設 suggestion） | 輸出契約（tool 回傳可達閉包內的 public 型別）屬性標註 `[JsonPropertyName]` | 移除 `JsonPropertyName`；鏡射契約標 `internal` 止於 Service 層，輸出欄位使用詞庫標準詞（見 [CONTRACT_RULES.md §2.2](CONTRACT_RULES.md)） |

---

## 4. Step 5: 詞庫擴充與中央 PR 自動提報機制 (PR Automation)

### 4.1 提報分類與程式碼異動規則
* **情境 1：新增既有詞彙之別名 (New Aliases)**
  修改 `Tier1Vocabulary.Entries` 中對應之 `GlossaryEntry`：
  ```csharp
  // 修改前
  new GlossaryEntry("productId", "ProductId", "產品料號或唯一代碼 (Product ID)", new[] { "productCode", "prodId" }),
  // 修改後（追加新別名）
  new GlossaryEntry("productId", "ProductId", "產品料號或唯一代碼 (Product ID)", new[] { "productCode", "prodId", "partNo", "materialNo" }),
  ```
  > 提報的別名必須**確為同義**的歷史命名；僅語意相近（如時刻 vs 時數）不得列為別名，應另立標準詞（見 [CONTRACT_RULES.md §2.1](CONTRACT_RULES.md)）。

* **情境 2：新增全域通用核心詞 (New Standard Entry)**
  向 `Tier1Vocabulary.Entries` 中**對應主題段落**追加新項目（分段順序即 AI 辨析順序，勿一律附加陣列末端）；`Description` 依三段式規範（定義＋邊界＋範例）撰寫：
  ```csharp
  new GlossaryEntry("<standardCamel>", "<StandardPascal>",
      "<定義>。<邊界：若欄位為 X 語意請用 Y>。<範例>",
      new[] { "<alias1>", "<alias2>" }),
  ```

### 4.2 前置防呆與衝突檢核演算法
在建立分支與 PR 前，執行以下自動驗證：
1. **別名唯一性檢核 (Alias Collision Check)**：遍歷現有 `Tier1Vocabulary.Entries`，確認提報之別名未以任何大小寫形式出現在其他詞條的 `StandardCamel`、`StandardPascal` 或 `Aliases` 中。
2. **格式檢核**：確認標準名稱不含底線與特殊符號，別名不包含前後空白。

### 4.3 環境感知執行命令

#### 情境 A：當前工作區為中央 Core 專案 (`McpGateway.Core`)
```powershell
git checkout -b vocab/<term>-alias
# 套用 Tier1Vocabulary.cs 異動
git add src/McpGateway.Analyzers/Tier1Vocabulary.cs
git commit -m "feat(vocab): add aliases for <ToolName>"
git push gitea vocab/<term>-alias
tea pr create --title "feat(vocab): add aliases for <ToolName>" --description "<PR_ALIGNMENT_TABLE>"
```

#### 情境 B：當前工作區為下游 Gateway 專案
```powershell
# 透過 tea CLI 對遠端中央 Repository 建立 PR
tea pr create --repo guy.mai/McpGateway.Core --title "feat(vocab): add aliases from <GatewayName>" --description "<PR_ALIGNMENT_TABLE>"
```

---

## 5. PR 結構化審核對齊表範本 (PR Markdown Template)

發送至中央團隊審核之 PR Description 格式：

```markdown
## 業務背景與提報來源
- **來源部門 / Gateway**: `MES` (或 `Report`, `EAP`, `SPC`, `QC`)
- **相關工具 / DTO**: `QueryWipTool` (`WipItemDto`)

## 詞庫異動對齊表
| 原始欄位 (Raw Field) | 提報類型 | 目標標準詞 (Standard Term) | 繁體中文說明 | 提報與審核理由 |
| :--- | :--- | :--- | :--- | :--- |
| `part_no` | 擴充別名 | `productId` | 產品料號或唯一代碼 | MES 歷史資料庫欄位別名收錄 |
| `fixtureId` | 全新核心詞 | `fixtureId` | 治具/夾具識別碼 | 治具管理跨 Gateway 共用概念 |

## 檢核狀態
- [x] 通過別名衝突檢核 (No alias collisions found)
- [x] 符合 CamelCase / PascalCase 命名規範

## 詞條品質自查（新增標準詞時必填；僅擴充別名可略）
- [x] 已在本地執行 `dotnet test`（詞庫一致性測試通過，無名稱/別名衝突）
- [x] 已比對 CONTRACT_RULES.md §2.1 易混淆群清單 → 本詞條命中：<群名或「無」>
- [x] 描述含三段式：定義 ✅｜與近義詞的辨析（若欄位為 X 請用 Y）✅｜格式範例 ✅
- [x] 別名僅列確為同義之歷史命名，不含僅語意相近者
- [x] 詞條已歸入 Tier1Vocabulary.cs 對應主題段落

| 提議詞條 | 可能混淆的現有詞條 | 邊界寫在哪裡 |
| :--- | :--- | :--- |
| `<term>` | `<near1>`、`<near2>` | 「<辨析句摘要>」 |
```

