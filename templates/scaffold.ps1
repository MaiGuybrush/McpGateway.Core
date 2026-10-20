<#
.SYNOPSIS
    MCP Gateway 部門 Modular Monorepo 專案腳手架 (Scaffold) 自動化產生腳本

.DESCRIPTION
    依據 ADR-014 部門內部多系統模組化與端點分流架構標準，自動化建立符合規範之部門 Gateway Modular Monorepo 方案，
    包含薄宿主 (Thin Host Web App)、分層組態與 .sln 方案檔。
    各業務子系統請於建立宿主後，透過 add-module.ps1 增量擴充。

.PARAMETER Department
    部門名稱代號 (如 eap, spc, mfg)

.PARAMETER Port
    服務監聽通訊埠 (如 5200)

.PARAMETER ToolName
    主要 MCP 工具名稱 (選填，依 ADR-014 子系統請透過 add-module.ps1 新增)

.PARAMETER AuthProvider
    認證提供者 (API-KEY, JWT, NTLM, None，預設為 API-KEY)

.PARAMETER UseConsul
    是否啟用 Consul 集中式服務發現設定 (預設為 $true)

.PARAMETER Solution
    是否建立 .sln 方案檔 (預設為 $true)

.PARAMETER OutDir
    輸出目錄路徑 (預設為當前目錄)

.PARAMETER DryRun
    預覽模式 (不寫入磁碟)

.PARAMETER Force
    若目標目錄已存在則強制覆寫

.EXAMPLE
    pwsh .\scaffold.ps1 -Department mfg -Port 5200 -AuthProvider API-KEY -OutDir D:\Projects\.NET
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "部門名稱代號 (如 eap, spc, mfg)")]
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
    [string]$Department,

    [Parameter(Mandatory = $true, HelpMessage = "服務監聽通訊埠 (如 5200)")]
    [ValidateRange(1024, 65535)]
    [int]$Port,

    [Parameter(Mandatory = $false, HelpMessage = "主要 MCP 工具名稱 (選填，依 ADR-014 子系統請透過 add-module.ps1 新增)")]
    [string]$ToolName,

    [Parameter(Mandatory = $false, HelpMessage = "認證提供者")]
    [ValidateSet('API-KEY', 'JWT', 'NTLM', 'None')]
    [string]$AuthProvider = 'API-KEY',

    [Parameter(Mandatory = $false, HelpMessage = "是否啟用 Consul 集中式服務發現設定")]
    [object]$UseConsul = $true,

    [Parameter(Mandatory = $false, HelpMessage = '是否建立 .sln 方案檔 (預設為 $true)')]
    [object]$Solution = $true,

    [Parameter(Mandatory = $false, HelpMessage = "輸出目錄路徑 (預設為當前目錄)")]
    [string]$OutDir = '.\',

    [Parameter(Mandatory = $false, HelpMessage = "預覽模式 (不寫入磁碟)")]
    [switch]$DryRun,

    [Parameter(Mandatory = $false, HelpMessage = "若目標目錄已存在則強制覆寫")]
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

# 0. 解析 UseConsul 與 Solution 布林值
$isUseConsul = $true
if ($null -ne $UseConsul) {
    if ($UseConsul -is [bool]) {
        $isUseConsul = $UseConsul
    } elseif ($UseConsul -is [string]) {
        $isUseConsul = ($UseConsul.ToLowerInvariant() -in @('$true', 'true', '1', 'yes'))
    } elseif ($UseConsul -is [int]) {
        $isUseConsul = ($UseConsul -ne 0)
    }
}
$UseConsul = $isUseConsul

$isSolution = $true
if ($null -ne $Solution) {
    if ($Solution -is [bool]) {
        $isSolution = $Solution
    } elseif ($Solution -is [string]) {
        $isSolution = ($Solution.ToLowerInvariant() -in @('$true', 'true', '1', 'yes'))
    } elseif ($Solution -is [int]) {
        $isSolution = ($Solution -ne 0)
    }
}

# 1. 驗證部門名稱長度
if ($Department.Length -lt 2 -or $Department.Length -gt 20) {
    Write-Error "部門名稱長度必須在 2 到 20 個字元之間。"
    exit 1
}

# 2. 驗證保留字
$reservedWords = @('core', 'common', 'shared', 'test', 'tests', 'gateway', 'base', 'mock', 'mcp', 'template', 'host')
if ($reservedWords -contains $Department.ToLowerInvariant()) {
    Write-Error "部門名稱 '$Department' 為系統保留字，請使用具體業務部門名稱。"
    exit 1
}

# 3. 提示 ToolName (若有傳入)
$deptLower = $Department.ToLowerInvariant()
if (-not [string]::IsNullOrWhiteSpace($ToolName)) {
    Write-Host "[INFO] 偵測到傳入 -ToolName '$ToolName'。依據 ADR-014 規範，子系統工具請於宿主建立完成後，使用 add-module.ps1 增量新增。" -ForegroundColor Yellow
}

# 4. 計算名稱映射
$pascalDept = ConvertTo-PascalCase $Department
$projectName = "McpGateway.$pascalDept"
$hostProjectName = "McpGateway.$pascalDept.Host"

# 5. 動態解析 Core 版本
$coreCsprojPath = Join-Path $PSScriptRoot "..\src\McpGateway.Core\McpGateway.Core.csproj"
$coreVersion = "0.1.0-preview"
if (Test-Path $coreCsprojPath) {
    try {
        $xml = [xml](Get-Content $coreCsprojPath -Raw -Encoding UTF8)
        $verNode = $xml.SelectSingleNode("//PropertyGroup/Version")
        if ($verNode -and -not [string]::IsNullOrWhiteSpace($verNode.InnerText)) {
            $coreVersion = $verNode.InnerText.Trim()
        }
    }
    catch {
        # Fallback to default version if XML parsing fails
    }
}

# 6. 載入 Auth Snippet
$authSnippetFile = switch ($AuthProvider.ToUpperInvariant()) {
    'API-KEY' { 'api-key.json' }
    'JWT'     { 'jwt.json' }
    'NTLM'    { 'ntlm.json' }
    'NONE'    { 'none.json' }
    default   { 'api-key.json' }
}
$authSnippetPath = Join-Path $PSScriptRoot "snippets\auth\$authSnippetFile"
$authSnippetContent = ""
if (Test-Path $authSnippetPath) {
    $authSnippetContent = Get-Content $authSnippetPath -Raw -Encoding UTF8
    $authSnippetContent = $authSnippetContent.Replace('__department__', $deptLower)
}

# 7. 計算目標路徑與衝突防護
$targetDirPath = [System.IO.Path]::GetFullPath((Join-Path $OutDir $projectName))
$hostDirPath = Join-Path $targetDirPath "src\$hostProjectName"

if (Test-Path $targetDirPath) {
    $existingItems = Get-ChildItem $targetDirPath -Force -ErrorAction SilentlyContinue
    if ($existingItems -and $existingItems.Count -gt 0) {
        if (-not $Force -and -not $DryRun) {
            Write-Error "目標目錄 '$targetDirPath' 已存在且非空。請指定 -Force 參數進行覆寫，或更換輸出目錄。"
            exit 1
        }
    }
}

# 8. 替換規則表
$replacementList = @(
    @{ Key = '__Department__';   Value = $pascalDept },
    @{ Key = '__department__';   Value = $deptLower },
    @{ Key = '__Port__';         Value = $Port.ToString() },
    @{ Key = '__Namespace__';    Value = $hostProjectName },
    @{ Key = '__CoreVersion__';  Value = $coreVersion },
    @{ Key = '__AuthSection__';  Value = $authSnippetContent.TrimEnd() }
)

$pathReplacements = @(
    @{ Key = '__Department__';   Value = $pascalDept },
    @{ Key = '__department__';   Value = $deptLower }
)

$templateHostRoot = Join-Path $PSScriptRoot "template-host"
if (-not (Test-Path $templateHostRoot)) {
    Write-Error "找不到 Host 專案模板目錄: $templateHostRoot"
    exit 1
}

# 9. DryRun 模式
if ($DryRun) {
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  [DryRun 預覽模式] MCP Gateway 部門 Modular Monorepo 產生清單" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  方案名稱:    $projectName"
    Write-Host "  薄宿主專案:  src/$hostProjectName"
    Write-Host "  通訊埠:      $Port (http://localhost:$Port)"
    Write-Host "  認證方式:    $AuthProvider"
    Write-Host "  Core 版本:   $coreVersion"
    Write-Host "  輸出路徑:    $targetDirPath"
    Write-Host "  Consul 整合: $(if ($UseConsul) { '啟用' } else { '停用' })"
    Write-Host "  建立 .sln:   $(if ($isSolution) { '是' } else { '否' })"
    Write-Host "--------------------------------------------------------------------------------"
    Write-Host "預計產生之檔案與目錄清單：" -ForegroundColor Yellow

    if ($isSolution) {
        Write-Host "  [FILE] $projectName.sln" -ForegroundColor Green
    }
    Write-Host "  [FILE] .gitignore" -ForegroundColor Green
    Write-Host "  [FILE] nuget.config" -ForegroundColor Green
    Write-Host "  [FILE] README.md" -ForegroundColor Green
    Write-Host "  [DIR ] .vscode" -ForegroundColor DarkGray
    Write-Host "  [FILE] .vscode/launch.json" -ForegroundColor Green
    Write-Host "  [FILE] .vscode/tasks.json" -ForegroundColor Green
    Write-Host "  [DIR ] src/$hostProjectName" -ForegroundColor DarkGray

    $templateItems = Get-ChildItem -Recurse $templateHostRoot
    foreach ($item in $templateItems) {
        if ($item.Name -eq 'README.root.md') {
            continue
        }
        if ($item.FullName.StartsWith((Join-Path $templateHostRoot ".vscode"))) {
            continue
        }
        if ($item.FullName -match '[\\/](obj|bin)([\\/]|$)') {
            continue
        }
        $relPath = $item.FullName.Substring($templateHostRoot.Length).TrimStart('\', '/')
        $destRelPath = $relPath
        foreach ($r in $pathReplacements) {
            $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
        }
        $fullRel = "src\$hostProjectName\$destRelPath"
        if ($item.PSIsContainer) {
            Write-Host "  [DIR ] $fullRel" -ForegroundColor DarkGray
        } else {
            Write-Host "  [FILE] $fullRel" -ForegroundColor Green
        }
    }
    Write-Host "================================================================================" -ForegroundColor Cyan
    return
}

# 10. 執行實際產生
if (-not (Test-Path $targetDirPath)) {
    New-Item -ItemType Directory -Path $targetDirPath -Force | Out-Null
}
if (-not (Test-Path $hostDirPath)) {
    New-Item -ItemType Directory -Path $hostDirPath -Force | Out-Null
}

# 產生 Host 專案檔案
$allHostTemplateItems = Get-ChildItem -Recurse $templateHostRoot

foreach ($item in $allHostTemplateItems) {
    if ($item.Name -eq 'README.root.md') {
        continue
    }
    if ($item.FullName.StartsWith((Join-Path $templateHostRoot ".vscode"))) {
        continue
    }
    if ($item.FullName -match '[\\/](obj|bin)([\\/]|$)') {
        continue
    }

    $relPath = $item.FullName.Substring($templateHostRoot.Length).TrimStart('\', '/')
    $destRelPath = $relPath
    foreach ($r in $pathReplacements) {
        $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
    }
    $destFullPath = Join-Path $hostDirPath $destRelPath

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

        if (-not $UseConsul) {
            $content = [System.Text.RegularExpressions.Regex]::Replace(
                $content,
                '"Urls":\s*\[[^\]]*\]',
                '"Urls": []'
            )
        }

        [System.IO.File]::WriteAllText($destFullPath, $content, [System.Text.Encoding]::UTF8)
    }
}

# 產生方案根目錄通用檔案 (.gitignore, nuget.config, README.md, .vscode)
$rootGitIgnorePath = Join-Path $targetDirPath ".gitignore"
$hostGitIgnore = Join-Path $templateHostRoot ".gitignore"
if (Test-Path $hostGitIgnore) {
    Copy-Item $hostGitIgnore $rootGitIgnorePath -Force
}

$rootNugetPath = Join-Path $targetDirPath "nuget.config"
$hostNuget = Join-Path $templateHostRoot "nuget.config"
if (Test-Path $hostNuget) {
    Copy-Item $hostNuget $rootNugetPath -Force
}

$rootReadmeTemplate = Join-Path $templateHostRoot "README.root.md"
if (Test-Path $rootReadmeTemplate) {
    $rootReadmeContent = Get-Content $rootReadmeTemplate -Raw -Encoding UTF8
    foreach ($entry in $replacementList) {
        $rootReadmeContent = $rootReadmeContent.Replace($entry.Key, $entry.Value)
    }
    $rootReadmePath = Join-Path $targetDirPath "README.md"
    [System.IO.File]::WriteAllText($rootReadmePath, $rootReadmeContent, [System.Text.Encoding]::UTF8)
}

# 產生方案根目錄 .vscode 除錯與建置設定 (launch.json, tasks.json)
$templateVsCodeDir = Join-Path $templateHostRoot ".vscode"
if (Test-Path $templateVsCodeDir) {
    $targetVsCodeDir = Join-Path $targetDirPath ".vscode"
    if (-not (Test-Path $targetVsCodeDir)) {
        New-Item -ItemType Directory -Path $targetVsCodeDir -Force | Out-Null
    }
    Get-ChildItem -Path $templateVsCodeDir -File | ForEach-Object {
        $vscodeContent = Get-Content $_.FullName -Raw -Encoding UTF8
        foreach ($entry in $replacementList) {
            $vscodeContent = $vscodeContent.Replace($entry.Key, $entry.Value)
        }
        $destFile = Join-Path $targetVsCodeDir $_.Name
        [System.IO.File]::WriteAllText($destFile, $vscodeContent, [System.Text.Encoding]::UTF8)
    }
}

# 產生 .sln 方案並加入 Host 專案
if ($isSolution) {
    $slnPath = Join-Path $targetDirPath "$projectName.sln"
    $hostCsprojPath = Join-Path $hostDirPath "$hostProjectName.csproj"
    dotnet new sln -n $projectName -o $targetDirPath --force | Out-Null
    dotnet sln $slnPath add $hostCsprojPath | Out-Null
}

# 11. 輸出成功摘要與指引
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  MCP Gateway 部門 Modular Monorepo 宿主專案產生成功！" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  方案名稱:    $projectName"
Write-Host "  薄宿主專案:  src/$hostProjectName"
Write-Host "  根命名空間:  $hostProjectName"
Write-Host "  通訊埠:      $Port (http://localhost:$Port)"
Write-Host "  認證方式:    $AuthProvider"
Write-Host "  Core 版本:   $coreVersion"
Write-Host "  輸出路徑:    $targetDirPath"
Write-Host "  方案檔案:    $(if ($isSolution) { Join-Path $targetDirPath "$projectName.sln" } else { '無' })"
Write-Host "================================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步操作指引："
Write-Host "  1. 增量建立子系統模組（例如 MES 子系統）："
Write-Host "     pwsh .\templates\add-module.ps1 -Department $deptLower -System mes -ToolName mes_query_lot"
Write-Host ""
Write-Host "  2. 切換至專案目錄："
Write-Host "     cd $targetDirPath"
Write-Host ""
Write-Host "  3. 還原與驗證編譯："
Write-Host "     dotnet restore ; dotnet build"
Write-Host ""
Write-Host "  4. 啟動薄宿主服務測試："
Write-Host "     dotnet run --project src/$hostProjectName"
Write-Host "     curl http://localhost:$Port/$deptLower/mcp"
