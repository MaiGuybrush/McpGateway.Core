<#
.SYNOPSIS
    MCP Gateway 增量式子系統模組產生腳本 (Add Subsystem Module)

.DESCRIPTION
    依據 ADR-014 部門內部多系統模組化與端點分流架構標準，在既有部門 Gateway Modular Monorepo 方案中增量建立子系統 Class Library 專案，
    自動產出遵循三段式命名規範之 MCP Tool、註冊擴充方法、組態與服務抽象，
    並自動執行方案註冊 (dotnet sln add)、Host 專案參考 (dotnet add reference)、Host/Program.cs 自動裝配 (Auto-wiring)
    與 appsettings.json 組態區段註冊。

.PARAMETER Department
    部門名稱代號 (如 eap, spc, mfg)

.PARAMETER System
    子系統名稱代號 (如 mes, wms, eap)

.PARAMETER ToolName
    主要 MCP 工具名稱 (如 mes_query_lot、query_lot 或 mfg_mes_query_lot)

.PARAMETER OutDir
    部門方案或其上層目錄路徑 (預設為當前目錄)

.PARAMETER DryRun
    預覽模式 (不寫入磁碟與修改方案)

.PARAMETER Force
    若子系統目錄已存在則強制覆寫

.EXAMPLE
    pwsh .\add-module.ps1 -Department mfg -System mes -ToolName mes_query_lot

.EXAMPLE
    pwsh .\add-module.ps1 -Department mfg -System mes -ToolName mes_query_lot -OutDir D:\Projects\.NET
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "部門名稱代號 (如 eap, spc, mfg)")]
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
    [string]$Department,

    [Parameter(Mandatory = $true, HelpMessage = "子系統名稱代號 (如 mes, wms, eap)")]
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
    [string]$System,

    [Parameter(Mandatory = $true, HelpMessage = "主要 MCP 工具名稱 (如 mes_query_lot、query_lot 或 mfg_mes_query_lot)")]
    [ValidatePattern('^[a-zA-Z0-9_]+$')]
    [string]$ToolName,

    [Parameter(Mandatory = $false, HelpMessage = "部門方案或其上層目錄路徑 (預設為當前目錄)")]
    [Alias('RepoRoot', 'TargetDir')]
    [string]$OutDir = '.\',

    [Parameter(Mandatory = $false, HelpMessage = "預覽模式 (不寫入磁碟與修改方案)")]
    [switch]$DryRun,

    [Parameter(Mandatory = $false, HelpMessage = "若子系統目錄已存在則強制覆寫")]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-PascalCase ([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return "" }
    $parts = $text -split '[_\-]'
    $pascal = ""
    foreach ($part in $parts) {
        if ($part.Length -gt 0) {
            $pascal += $part.Substring(0, 1).ToUpperInvariant() + $part.Substring(1).ToLowerInvariant()
        }
    }
    return $pascal
}

# 1. 驗證名稱長度與保留字
$reservedWords = @('core', 'common', 'shared', 'test', 'tests', 'gateway', 'base', 'mock', 'mcp', 'template', 'host')

if ($Department.Length -lt 2 -or $Department.Length -gt 20) {
    Write-Error "部門名稱長度必須在 2 到 20 個字元之間。"
    exit 1
}
if ($reservedWords -contains $Department.ToLowerInvariant()) {
    Write-Error "部門名稱 '$Department' 為系統保留字，請使用具體業務部門名稱。"
    exit 1
}

if ($System.Length -lt 2 -or $System.Length -gt 20) {
    Write-Error "子系統名稱長度必須在 2 到 20 個字元之間。"
    exit 1
}
if ($reservedWords -contains $System.ToLowerInvariant()) {
    Write-Error "子系統名稱 '$System' 為系統保留字，請使用具體業務子系統名稱。"
    exit 1
}

# 2. 計算名稱與識別碼
$pascalDept = ConvertTo-PascalCase $Department
$deptLower = $Department.ToLowerInvariant()
$pascalSystem = ConvertTo-PascalCase $System
$systemLower = $System.ToLowerInvariant()

$projectName = "McpGateway.$pascalDept"
$hostProjectName = "McpGateway.$pascalDept.Host"
$subsystemProjectName = "McpGateway.$pascalDept.$pascalSystem"

# 3. 解析工具名稱與三段式規範
$rawToolName = $ToolName.ToLowerInvariant().Trim()

# 移除可能的 {dept}_{system}_ 或 {system}_ 前綴以擷取 action
$deptSysPrefix = "${deptLower}_${systemLower}_"
$sysPrefix = "${systemLower}_"

if ($rawToolName.StartsWith($deptSysPrefix)) {
    $actionPart = $rawToolName.Substring($deptSysPrefix.Length)
} elseif ($rawToolName.StartsWith($sysPrefix)) {
    $actionPart = $rawToolName.Substring($sysPrefix.Length)
} else {
    $actionPart = $rawToolName
}

if ([string]::IsNullOrWhiteSpace($actionPart)) {
    Write-Error "工具名稱 '$ToolName' 無法解析出有效之動作名稱 (Action)。"
    exit 1
}

$fullToolName = "${deptLower}_${systemLower}_${actionPart}"
$toolClass = ConvertTo-PascalCase $actionPart

# 4. 尋找目標方案與 Host 專案目錄
$resolvedTargetDir = $null
$candidateDirs = @(
    (Join-Path $OutDir "src\$hostProjectName"),
    (Join-Path $OutDir "$projectName\src\$hostProjectName"),
    (Join-Path (Get-Location) "src\$hostProjectName"),
    (Join-Path (Get-Location) "$projectName\src\$hostProjectName")
)

if (Test-Path (Join-Path $OutDir "src\$hostProjectName")) {
    $resolvedTargetDir = [System.IO.Path]::GetFullPath($OutDir)
} elseif (Test-Path (Join-Path $OutDir "$projectName\src\$hostProjectName")) {
    $resolvedTargetDir = [System.IO.Path]::GetFullPath((Join-Path $OutDir $projectName))
} elseif (Test-Path (Join-Path (Get-Location) "src\$hostProjectName")) {
    $resolvedTargetDir = [System.IO.Path]::GetFullPath((Get-Location))
} elseif (Test-Path (Join-Path (Get-Location) "$projectName\src\$hostProjectName")) {
    $resolvedTargetDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $projectName))
} else {
    Write-Error "找不到部門宿主專案 '$hostProjectName'。請確認目標目錄或 -OutDir 中包含 'src/$hostProjectName/$hostProjectName.csproj'，或先執行 scaffold.ps1 建立部門薄宿主方案。"
    exit 1
}

$hostDirPath = Join-Path $resolvedTargetDir "src\$hostProjectName"
$hostCsprojPath = Join-Path $hostDirPath "$hostProjectName.csproj"
$hostProgramPath = Join-Path $hostDirPath "Program.cs"

if (-not (Test-Path $hostCsprojPath)) {
    Write-Error "找不到 Host 專案檔: $hostCsprojPath"
    exit 1
}
if (-not (Test-Path $hostProgramPath)) {
    Write-Error "找不到 Host 進入點檔案: $hostProgramPath"
    exit 1
}

# 尋找方案檔
$slnPath = Join-Path $resolvedTargetDir "$projectName.sln"
if (-not (Test-Path $slnPath)) {
    $slnCandidates = Get-ChildItem -Path $resolvedTargetDir -Filter "*.sln" -File -ErrorAction SilentlyContinue
    if ($slnCandidates -and $slnCandidates.Count -gt 0) {
        $slnPath = $slnCandidates[0].FullName
    } else {
        $slnPath = $null
    }
}

# 5. 解析 Core 版本
$coreVersion = "0.2.4-preview"
try {
    $hostXml = [xml](Get-Content $hostCsprojPath -Raw -Encoding UTF8)
    $pkgNode = $hostXml.SelectSingleNode("//PackageReference[@Include='McpGateway.Core']")
    if ($pkgNode -and -not [string]::IsNullOrWhiteSpace($pkgNode.GetAttribute("Version"))) {
        $coreVersion = $pkgNode.GetAttribute("Version").Trim()
    } else {
        $coreCsprojPath = Join-Path $PSScriptRoot "..\src\McpGateway.Core\McpGateway.Core.csproj"
        if (Test-Path $coreCsprojPath) {
            $xml = [xml](Get-Content $coreCsprojPath -Raw -Encoding UTF8)
            $verNode = $xml.SelectSingleNode("//PropertyGroup/Version")
            if ($verNode -and -not [string]::IsNullOrWhiteSpace($verNode.InnerText)) {
                $coreVersion = $verNode.InnerText.Trim()
            }
        }
    }
}
catch {
    # Fallback to default
}

# 6. 計算子系統與測試專案路徑與衝突防護
$subsystemDirPath = Join-Path $resolvedTargetDir "src\$subsystemProjectName"
$subsystemCsprojPath = Join-Path $subsystemDirPath "$subsystemProjectName.csproj"

$testsProjectName = "McpGateway.$pascalDept.$pascalSystem.Tests"
$testsDirPath = Join-Path $resolvedTargetDir "tests\$testsProjectName"
$testsCsprojPath = Join-Path $testsDirPath "$testsProjectName.csproj"

if (Test-Path $subsystemDirPath) {
    $existingItems = Get-ChildItem $subsystemDirPath -Force -ErrorAction SilentlyContinue
    if ($existingItems -and $existingItems.Count -gt 0) {
        if (-not $Force -and -not $DryRun) {
            Write-Error "子系統目錄 '$subsystemDirPath' 已存在且非空。請指定 -Force 參數進行覆寫，或指定其他子系統代號。"
            exit 1
        }
    }
}

# 7. 替換規則表
$replacementList = @(
    @{ Key = '__Department__';     Value = $pascalDept },
    @{ Key = '__department__';     Value = $deptLower },
    @{ Key = '__System__';         Value = $pascalSystem },
    @{ Key = '__system__';         Value = $systemLower },
    @{ Key = '__ToolClass__';      Value = $toolClass },
    @{ Key = '__full_tool_name__'; Value = $fullToolName },
    @{ Key = '__CoreVersion__';    Value = $coreVersion }
)

$pathReplacements = @(
    @{ Key = '__Department__'; Value = $pascalDept },
    @{ Key = '__department__'; Value = $deptLower },
    @{ Key = '__System__';     Value = $pascalSystem },
    @{ Key = '__system__';     Value = $systemLower },
    @{ Key = '__ToolClass__';  Value = $toolClass }
)

$templateModuleRoot = Join-Path $PSScriptRoot "template-module"
if (-not (Test-Path $templateModuleRoot)) {
    Write-Error "找不到子系統專案模板目錄: $templateModuleRoot"
    exit 1
}

$templateModuleTestsRoot = Join-Path $PSScriptRoot "template-module-tests"

# 8. DryRun 預覽模式
if ($DryRun) {
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  [DryRun 預覽模式] MCP Gateway 增量式子系統產生清單" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  部門代號:    $pascalDept ($deptLower)"
    Write-Host "  子系統代號:  $pascalSystem ($systemLower)"
    Write-Host "  子系統專案:  src/$subsystemProjectName"
    Write-Host "  測試專案:    tests/$testsProjectName"
    Write-Host "  MCP 工具名:  $fullToolName"
    Write-Host "  工具類別:    ${toolClass}Tool"
    Write-Host "  Core 版本:   $coreVersion"
    Write-Host "  目標方案:    $(if ($slnPath) { $slnPath } else { '未指定' })"
    Write-Host "  宿主專案:    $hostDirPath"
    Write-Host "--------------------------------------------------------------------------------"
    Write-Host "預計產生之子系統檔案清單：" -ForegroundColor Yellow

    $allModuleItems = Get-ChildItem -Recurse $templateModuleRoot | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)([\\/]|$)' }
    foreach ($item in $allModuleItems) {
        $relPath = $item.FullName.Substring($templateModuleRoot.Length).TrimStart('\', '/')
        $destRelPath = $relPath
        foreach ($r in $pathReplacements) {
            $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
        }
        $fullRel = "src\$subsystemProjectName\$destRelPath"
        if ($item.PSIsContainer) {
            Write-Host "  [DIR ] $fullRel" -ForegroundColor DarkGray
        } else {
            Write-Host "  [FILE] $fullRel" -ForegroundColor Green
        }
    }

    if (Test-Path $templateModuleTestsRoot) {
        $allTestItems = Get-ChildItem -Recurse $templateModuleTestsRoot | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)([\\/]|$)' }
        foreach ($item in $allTestItems) {
            $relPath = $item.FullName.Substring($templateModuleTestsRoot.Length).TrimStart('\', '/')
            $destRelPath = $relPath
            foreach ($r in $pathReplacements) {
                $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
            }
            $fullRel = "tests\$testsProjectName\$destRelPath"
            if ($item.PSIsContainer) {
                Write-Host "  [DIR ] $fullRel" -ForegroundColor DarkGray
            } else {
                Write-Host "  [FILE] $fullRel" -ForegroundColor Green
            }
        }
    }

    Write-Host "--------------------------------------------------------------------------------"
    Write-Host "預計執行之自動裝配 (Auto-Wiring) 操作：" -ForegroundColor Yellow
    if ($slnPath) {
        Write-Host "  [SLN ] dotnet sln `"$slnPath`" add `"src\$subsystemProjectName\$subsystemProjectName.csproj`"" -ForegroundColor Cyan
        Write-Host "  [SLN ] dotnet sln `"$slnPath`" add `"tests\$testsProjectName\$testsProjectName.csproj`"" -ForegroundColor Cyan
    }
    Write-Host "  [REF ] dotnet add `"$hostCsprojPath`" reference `"src\$subsystemProjectName\$subsystemProjectName.csproj`"" -ForegroundColor Cyan
    Write-Host "  [CODE] 在 $hostProgramPath 注入 'using McpGateway.$pascalDept.$pascalSystem;'" -ForegroundColor Cyan
    Write-Host "  [CODE] 在 $hostProgramPath 注入 'builder.Services.Add${pascalSystem}Subsystem(builder.Configuration);'" -ForegroundColor Cyan
    Write-Host "  [CONF] 在 appsettings.json 註冊 'McpGateway:Systems:$systemLower' 組態區段" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    return
}

# 9. 建立子系統目錄與複製模板檔案
if (-not (Test-Path $subsystemDirPath)) {
    New-Item -ItemType Directory -Path $subsystemDirPath -Force | Out-Null
}

$allModuleItems = Get-ChildItem -Recurse $templateModuleRoot | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)([\\/]|$)' }
foreach ($item in $allModuleItems) {
    $relPath = $item.FullName.Substring($templateModuleRoot.Length).TrimStart('\', '/')
    $destRelPath = $relPath
    foreach ($r in $pathReplacements) {
        $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
    }
    $destFullPath = Join-Path $subsystemDirPath $destRelPath

    if ($item.PSIsContainer) {
        if (-not (Test-Path $destFullPath)) {
            New-Item -ItemType Directory -Path $destFullPath -Force | Out-Null
        }
    } else {
        $parentDir = Split-Path $destFullPath -Parent
        if (-not (Test-Path $parentDir)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }

        $content = Get-Content $item.FullName -Raw -Encoding UTF8
        foreach ($entry in $replacementList) {
            $content = $content.Replace($entry.Key, $entry.Value)
        }

        [System.IO.File]::WriteAllText($destFullPath, $content, [System.Text.Encoding]::UTF8)
    }
}

# 9.1 建立測試專案目錄與複製模板檔案
if (Test-Path $templateModuleTestsRoot) {
    if (-not (Test-Path $testsDirPath)) {
        New-Item -ItemType Directory -Path $testsDirPath -Force | Out-Null
    }

    $allTestItems = Get-ChildItem -Recurse $templateModuleTestsRoot | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)([\\/]|$)' }
    foreach ($item in $allTestItems) {
        $relPath = $item.FullName.Substring($templateModuleTestsRoot.Length).TrimStart('\', '/')
        $destRelPath = $relPath
        foreach ($r in $pathReplacements) {
            $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
        }
        $destFullPath = Join-Path $testsDirPath $destRelPath

        if ($item.PSIsContainer) {
            if (-not (Test-Path $destFullPath)) {
                New-Item -ItemType Directory -Path $destFullPath -Force | Out-Null
            }
        } else {
            $parentDir = Split-Path $destFullPath -Parent
            if (-not (Test-Path $parentDir)) {
                New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
            }

            $content = Get-Content $item.FullName -Raw -Encoding UTF8
            foreach ($entry in $replacementList) {
                $content = $content.Replace($entry.Key, $entry.Value)
            }

            [System.IO.File]::WriteAllText($destFullPath, $content, [System.Text.Encoding]::UTF8)
        }
    }
}

# 10. 自動裝配管線連結 (Auto-Wiring)
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  開始執行子系統模組自動裝配 (Auto-Wiring)..." -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# 10.1 方案註冊 (dotnet sln add)
if ($slnPath -and (Test-Path $slnPath)) {
    Write-Host "[1/4] 將子系統與測試專案註冊至方案檔..." -ForegroundColor Yellow
    try {
        $slnOutput = dotnet sln $slnPath add $subsystemCsprojPath 2>&1
        Write-Host "      $slnOutput" -ForegroundColor Gray
        if (Test-Path $testsCsprojPath) {
            $slnTestOutput = dotnet sln $slnPath add $testsCsprojPath 2>&1
            Write-Host "      $slnTestOutput" -ForegroundColor Gray
        }
    }
    catch {
        Write-Warning "加入方案時發生非預期錯誤: $_"
    }
} else {
    Write-Host "[1/4] 方案檔不存在，略過方案註冊。" -ForegroundColor DarkGray
}

# 10.2 專案參考 (dotnet add reference)
Write-Host "[2/4] 為 Host 專案加入子系統專案參考..." -ForegroundColor Yellow
try {
    $refOutput = dotnet add $hostCsprojPath reference $subsystemCsprojPath 2>&1
    Write-Host "      $refOutput" -ForegroundColor Gray
}
catch {
    Write-Warning "加入專案參考時發生非預期錯誤: $_"
}

# 10.3 進入點注入 (Host/Program.cs)
Write-Host "[3/4] 在 Host Program.cs 自動注入子系統註冊碼..." -ForegroundColor Yellow
try {
    $programContent = Get-Content $hostProgramPath -Raw -Encoding UTF8

    # 注入 using
    $usingDirective = "using McpGateway.$pascalDept.$pascalSystem;"
    if ($programContent -notmatch [regex]::Escape($usingDirective)) {
        if ($programContent -match "(?m)(^using\s+[^;]+;\r?\n)(?!using\s)") {
            $programContent = [regex]::Replace($programContent, "(?m)(^using\s+[^;]+;\r?\n)(?!using\s)", "`$1$usingDirective`r`n")
        } else {
            $programContent = "$usingDirective`r`n" + $programContent
        }
    }

    # 注入 AddSubsystem
    $regCall = "builder.Services.Add${pascalSystem}Subsystem(builder.Configuration);"
    if ($programContent -notmatch [regex]::Escape("Add${pascalSystem}Subsystem")) {
        if ($programContent.Contains("// __SUBSYSTEM_REGISTRATION__")) {
            $anchor = "// __SUBSYSTEM_REGISTRATION__"
            $programContent = $programContent.Replace($anchor, "$anchor`r`n$regCall")
        } elseif ($programContent -match "var\s+app\s*=\s*builder\.Build\(\);") {
            $programContent = [regex]::Replace($programContent, "(var\s+app\s*=\s*builder\.Build\(\);)", "$regCall`r`n`r`n`$1")
        } else {
            $programContent += "`r`n$regCall`r`n"
        }
    }

    [System.IO.File]::WriteAllText($hostProgramPath, $programContent, [System.Text.Encoding]::UTF8)
    Write-Host "      已成功注入 $regCall" -ForegroundColor Gray
}
catch {
    Write-Warning "注入 Program.cs 時發生非預期錯誤: $_"
}

# 10.4 更新 Host/appsettings.json
Write-Host "[4/4] 檢查並更新 Host appsettings.json 組態區段..." -ForegroundColor Yellow
$appSettingsPath = Join-Path $hostDirPath "appsettings.json"
if (Test-Path $appSettingsPath) {
    try {
        $appJson = Get-Content $appSettingsPath -Raw -Encoding UTF8
        if ($appJson -notmatch """$systemLower""\s*:") {
            # 扁平結構，對齊 template-module/Configuration/__System__Options.cs 的實際屬性
            # （ConsulKey / FallbackBaseUrls / TimeoutSeconds，不包 Downstream 巢狀物件，也沒有 BaseUrl 屬性）
            if ($appJson -match '("Systems"\s*:\s*\{)\s*(\})') {
                $subsystemSnippet = "`r`n      `"$systemLower`": {`r`n        `"ConsulKey`": `"`",`r`n        `"FallbackBaseUrls`": [ `"http://api.corp.local/$deptLower/$systemLower`" ],`r`n        `"TimeoutSeconds`": 30`r`n      }`r`n    "
                $appJson = $appJson -replace '("Systems"\s*:\s*\{)\s*(\})', "`$1$subsystemSnippet`$2"
                [System.IO.File]::WriteAllText($appSettingsPath, $appJson, [System.Text.Encoding]::UTF8)
                Write-Host "      已向 appsettings.json 加入 Systems:$systemLower 預設組態" -ForegroundColor Gray
            } elseif ($appJson -match '("Systems"\s*:\s*\{)') {
                $subsystemSnippet = "`r`n      `"$systemLower`": {`r`n        `"ConsulKey`": `"`",`r`n        `"FallbackBaseUrls`": [ `"http://api.corp.local/$deptLower/$systemLower`" ],`r`n        `"TimeoutSeconds`": 30`r`n      },"
                $appJson = $appJson -replace '("Systems"\s*:\s*\{)', "`$1$subsystemSnippet"
                [System.IO.File]::WriteAllText($appSettingsPath, $appJson, [System.Text.Encoding]::UTF8)
                Write-Host "      已向 appsettings.json 加入 Systems:$systemLower 預設組態" -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Warning "更新 appsettings.json 時發生非預期錯誤: $_"
    }
}

# 11. 輸出成功摘要與指引
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  MCP Gateway 子系統模組建立與自動裝配完成！" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  部門代號:    $pascalDept ($deptLower)"
Write-Host "  子系統代號:  $pascalSystem ($systemLower)"
Write-Host "  子系統專案:  src/$subsystemProjectName"
Write-Host "  MCP 工具名:  $fullToolName"
Write-Host "  工具類別:    ${toolClass}Tool"
Write-Host "  專案路徑:    $subsystemDirPath"
Write-Host "  端點路徑:    /$deptLower/$systemLower/mcp"
Write-Host "================================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步操作指引："
Write-Host "  1. 切換至方案目錄："
Write-Host "     cd `"$resolvedTargetDir`""
Write-Host ""
Write-Host "  2. 進行方案編譯與驗證："
Write-Host "     dotnet build"
Write-Host ""
Write-Host "  3. 啟動薄宿主服務測試："
Write-Host "     dotnet run --project src/$hostProjectName"
Write-Host "     curl http://localhost:<port>/$deptLower/$systemLower/mcp"
Write-Host ""
