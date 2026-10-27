# ADR-016: Tool 輸出契約的 Agent 層標準化（鏡射契約止於 Service、MCP0013 強制）

## 狀態
**✅ 已採用 (Accepted)**（2026-10-22，決議過程見 [Issue #15](../../issue/issue-15-output-contract-agent-layer-standardization.md) §6）

## 背景

平台目標是 Agent 跨系統呼叫各部門 MCP tool 時，語意相同的欄位名稱一致。詞庫（ADR-010）定義了標準詞，分析器（MCP0010）保證了 C# 原始碼層一致，但標準化鏈在輸出層斷裂：分析器只檢查 C# 符號名，而 **Agent 實際讀到的 JSON 鍵由 `[JsonPropertyName]` 決定**。單層鏡射写法下，即使 C# 屬性已改為標準詞，Agent 讀到的仍是下游方言（`totalCount`、`createTime`、snake_case）。盤點確認四個子系統模組存在兩種契約形態（Qsys.Umsapi 與 Report.Mfg 為兩層、Qsys.Issapi 與 Mes.Arymes 為單層透傳），且既有 ADR/Skill 對鏡射場景完全無規則。詳見 [Issue #15](../../issue/issue-15-output-contract-agent-layer-standardization.md) §1–§3。

## 決策

我們決定將 **Gateway 對 Agent 的契約（輸出契約）與對下游的契約（鏡射契約）強制分層**，使詞庫標準詞真正送達 Agent：

1. **輸出契約不得標註 `[JsonPropertyName]`**：tool 回傳型別（及其巢狀閉包）的 JSON 鍵一律由 C# 屬性名的 camelCase 推導，因此分析器檢查 C# 名即等價於檢查 Agent 讀到的鍵——標準化鏈閉環。輸入契約同受此規則約束，但 MCP0013 第一版只強制輸出側。
2. **鏡射契約標 `internal` 且止於 Service 層**：跟隨上游 JSON 鍵的契約型別不得作為 tool 回傳型別；下游方言轉為標準詞的唯一發生點是 Service 層的**契約映射**（`Map*()`）。
3. **新增 Roslyn 診斷 `MCP0013`（Warning）**，由獨立的 `McpOutputContractAnalyzer` 實作。輸出契約的判定以**從 tool 簽名追符號可達性為唯一權威**（不靠類別名字後綴或目錄位置）；`Contracts/`＋`Dtos/` 目錄佈局僅為導航輔助。不觸發僅剩結構性三項：`internal` 型別、不可達型別、Core/Analyzers 組件型別（外部系統契約）。
4. **規則絕對，不設 tool 級豁免**：曾考慮 `[FaithfulMirror]` attribute 讓「忠實鏡射」型 tool（如 Mes Arymes 58 欄鏡射）豁免，同日推翻。理由：該豁免唯一保護的是「Agent 讀到下游鍵名」，正是本決策要消滅的不一致；「忠實鏡射」的忠實指**資料完整性**（欄位齊全、全可空、值不加工），與鍵名無關，兩層化可完整保留。Mes Arymes 同樣兩層化（其 C# 名已標準化，成本機械性），下游來源欄名寫進輸出契約的 `[Description]` 供除錯對照。
5. **過渡**：Core 發版時 MCP0013 預設 severity `suggestion`（不擋建置）；下游各專案在遷移完成的 PR 中把 `.editorconfig` 收為 `warning`。存量遷移：Qsys Issapi 18 tool 一次全部兩層化、Mes Arymes 機械性兩層化（輸出鍵變更為 BREAKING，硬切不保留舊鍵）；Umsapi/Report 僅目錄對齊。

## 考慮過的替代方案

- **只修 C# 命名、保留鏡射 `JsonPropertyName`**（消除警告但不觸及 Agent 層）：排除——治理目標在 Agent 可見鍵，不在編譯器綠燈。
- **只寫文件不加分析器**：排除——與 Issue #14 教訓相悖，靠 review 把關必破窗。
- **以類別名字後綴或目錄位置判定輸出契約**：排除——`ConsulShopItem`（設定鏡射、名字撞 `Item` 後綴）證明啟發式會偽陽性；可達性追蹤讓「藏在 Service 後的鏡射契約」天然免檢，規則與檢查共用同一事實。
- **詞庫向下游現實讓步（`totalCount` 升為標準詞）**：排除——`createTime` 等詞的語意模糊正是 Issue #14 剛處理的來源；且鏡射是下游契約，不構成標準的依據。
- **`[FaithfulMirror]` 豁免 attribute（曾採、同日推翻）**：排除——它保護的恰是本決策要消滅的屬性（Agent 讀下游鍵名），把不一致固化成特權；規則有例外就需要二次討論「何時可以豁免」，絕對規則的心智成本反而更低；且它是 Core 唯一的新增公開 API，發佈後難以收回。除錯對照需求由 `[Description]` 附註來源欄名滿足。

## 後果

- **正面**：Agent 在三部門 Gateway 讀到同一套詞庫標準詞；MCP0010 的檢查首次覆蓋治理目標層；欄位裁剪（token 成本）在契約映射處有了執行點。
- **成本與風險**：Issapi 18 tool 與 Mes Arymes 輸出鍵變更是 Agent 可見的 BREAKING 變更（硬切，需通知整合方）；Core 新增一條診斷（MCP0013），屬長期維護面（已因不設豁免 attribute 而不新增公開 API）；契約映射是純屬性拷貝（微秒級），效能非顧慮，兩層＋裁剪反而降低 Agent 端 token。
- **術語**：鏡射契約／輸出契約／契約映射 已收錄 [GLOSSARY.md](../../../GLOSSARY.md)「Tool 契約」段。

## 相關

- [ADR-010](ADR-010-mcp-tool-field-standardization-and-structured-dto.md)：本 ADR 補齊其支柱 1/3 未覆蓋的輸出鍵層；兩者可並存，無牴觸。
- [Issue #15](../../issue/issue-15-output-contract-agent-layer-standardization.md)：完整分析、方案評估與逐項決議記錄。
- 實作範本：Qsys `Umsapi`（鏡射 internal→`Map*()`→輸出契約）、Report `Mfg`（`MfgWipRawRecord`→`WipItemDto`）。
