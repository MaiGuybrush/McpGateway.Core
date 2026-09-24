// McpGateway.Core — NuGet 套件建置／測試／發佈 Pipeline
// 使用 jenkins-pipeline-library 的 dotnetPkgPipeline（文件：docs/dotnetPkgPipeline.md）
//
// 設計依據：
//   - ADR-015：單一套件合併發布（Core dll + Analyzers dll → McpGateway.Core nupkg）
//   - issue #56：global.json 精確比對環境 SDK 清單（version 完全一致 + rollForward=disable）
//   - issue #64：packProjectName 支援子目錄 csproj（src/McpGateway.Core）
//
// 分支慣例：
//   - release/{version}（如 release/0.1.0）→ 驗證分支版本與 csproj Version 一致後發佈 NuGet
//   - 其他分支 / PR → 只 build + test，不發佈
//
// 前提（Verify Documentation stage 要求）：
//   - docs/README.md 須包含目前版本號（0.1.0-preview）
//   - pack 專案 csproj 不得含 ProjectReference / 本機 DLL HintPath（ADR-015 以 None+Pack 合併，符合）

@Library('jenkins-pipeline-library') _

dotnetPkgPipeline(
    useSolutionFile: 'McpGateway.Core.sln',  // restore/build/test 跑整個 sln（test 專案編譯但不進套件）
    packProjectName: 'McpGateway.Core',      // 只 pack src/McpGateway.Core → 上傳單一 nupkg（含 analyzers/dotnet/cs）
    testEnabled: true,
    publishEnabled: true,                    // 實際發佈仍受 release branch 條件保護
    buildConfiguration: 'Release',
    checkHintPath: true,
    runIntegrationTests: false               // 整合測試手動觸發時改 true 並設 integrationTestEnvKey
    // integrationTestEnvKey: 'ENABLE_INTEGRATION_TESTS'
)
