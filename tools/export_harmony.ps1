<#
.SYNOPSIS
    Unity -> OpenHarmony 出包流水线（无人值守 / CI 入口）。

.DESCRIPTION
    推荐用法（日常）：在 Unity 编辑器里点
        Tools/自动化打包/Harmony/Debug  或  Release
    那条路径会走 AppBuilder_HARMONY.cs 里的完整流水线，是唯一权威实现。

    本脚本用于无人值守场景，两种模式：
      A) 默认：调用 Unity 命令行 -executeMethod HarmonyExportBuilder.RunBatch
              —— RunBatch 内部同样走 AppBuilder 的完整流水线（导出 → 补丁 → 签名 → ohpm → hvigor），
                 与菜单点出来的产物完全一致，不存在第二套补丁实现。
      B) -SkipUnityExport：Unity GUI 已经开着、导出产物已存在时，只跑后处理 + 出包。
              这条路径用 tools/harmony_patch/apply_harmony_patch.py 作为补丁实现（应急/手工用）。
              注意：Unity GUI 开着时无法再启 CLI 实例，同一个工程只能开一个。

    配置来源（签名密文只存在一处，且不入库）：
      Assets/Middleware/Editor/AppBuilder/harmony_toolchain.json          公开配置，入库
      Assets/Middleware/Editor/AppBuilder/harmony_toolchain.local.json    私密配置，被 gitignore
      后者覆盖前者，keyPassword / storePassword 只写在 local 里。

    关键坑（Unity 每次导出都会重置，流水线里已自动补齐）：
      - 手改的 .ets 会被 Unity 重建 -> 必须重放补丁
      - build-profile.json5 的 signingConfigs 导出时是空数组 -> 必须重新注入
      - oh_modules 不会随导出带出 -> 首次必须 ohpm install

.EXAMPLE
    .\export_harmony.cmd                     # Unity 未开：命令行跑完整流水线
    .\export_harmony.cmd -SkipUnityExport    # Unity 已开：只对已有导出目录出包
    .\export_harmony.cmd -SkipBuild          # 只导出不打包
#>
[CmdletBinding()]
param(
    [string] $UnityProject = "D:\GitFork\HexWord",
    [string] $ExportDir    = "D:\GitFork\HexWord\output\harmony_fix_1.3.4",
    [string] $KeystoreDir  = "",
    [string] $UnityExe     = "D:\Unity_tuanjie\2022.3.55t4\Editor\Unity.exe",
    [string] $DevEcoHome   = "C:\Program Files\Huawei\DevEco Studio",
    [switch] $SkipUnityExport,
    [switch] $SkipBuild,
    [switch] $Clean
)

$ErrorActionPreference = "Stop"
$script:Failed = $false

# 统一控制台编码为 UTF-8，避免 python 子进程输出（utf-8）与 PowerShell 输出（GBK）不一致导致乱码
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$env:PYTHONIOENCODING = "utf-8"

function Write-Step  ($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Ok    ($msg) { Write-Host "  [OK]   $msg" -ForegroundColor Green }
function Write-Info  ($msg) { Write-Host "  [INFO] $msg" -ForegroundColor Gray }
function Write-Warn2 ($msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Err   ($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red; $script:Failed = $true }

# ---------------------------------------------------------------- 0. 前置检查
Write-Step "0/4 前置检查"

# 0.1 读取工具链配置（公开 + 本地私密，后者覆盖前者）
$ToolchainJson     = Join-Path $UnityProject "Assets\Middleware\Editor\AppBuilder\harmony_toolchain.json"
$ToolchainLocalJson= Join-Path $UnityProject "Assets\Middleware\Editor\AppBuilder\harmony_toolchain.local.json"

$cfg = $null
if (Test-Path $ToolchainJson) {
    try {
        $cfg = Get-Content $ToolchainJson -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Ok "已读取工具链配置: $ToolchainJson"
    } catch { Write-Err "工具链配置解析失败: $($_.Exception.Message)" }
} else {
    Write-Err "缺少工具链配置: $ToolchainJson"
}
if (Test-Path $ToolchainLocalJson) {
    try {
        $local = Get-Content $ToolchainLocalJson -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($p in $local.PSObject.Properties) {
            $cfg | Add-Member -NotePropertyName $p.Name -NotePropertyValue $p.Value -Force
        }
        Write-Ok "已合并本地私密配置: $ToolchainLocalJson"
    } catch { Write-Err "本地私密配置解析失败: $($_.Exception.Message)" }
} else {
    Write-Warn2 "未找到 $ToolchainLocalJson（签名密文需自行创建该文件）"
}

# 0.2 用配置覆盖可空的命令行参数
if ($cfg) {
    if ([string]::IsNullOrEmpty($KeystoreDir) -and $cfg.signingKeystoreDir) { $KeystoreDir = $cfg.signingKeystoreDir }
    if ([string]::IsNullOrEmpty($DevEcoHome) -or $DevEcoHome -eq "C:\Program Files\Huawei\DevEco Studio") {
        if ($cfg.devEcoHome) { $DevEcoHome = $cfg.devEcoHome }
    }
}
$keyPassword   = if ($cfg) { $cfg.keyPassword } else { "" }
$storePassword = if ($cfg) { $cfg.storePassword } else { "" }

$hvigorw = Join-Path $DevEcoHome "tools\hvigor\bin\hvigorw.bat"
$ohpm    = Join-Path $DevEcoHome "tools\ohpm\bin\ohpm.bat"
$nodeHome= Join-Path $DevEcoHome "tools\node"

foreach ($p in @($hvigorw, $ohpm, $nodeHome)) {
    if (Test-Path $p) { Write-Ok $p } else { Write-Err "缺失: $p" }
}
if (-not (Test-Path $KeystoreDir)) { Write-Err "签名材料目录不存在: $KeystoreDir" }
foreach ($f in @("hexa.p12", "hexa_debug.cer", "hexa_debugDebug.p7b")) {
    if (Test-Path (Join-Path $KeystoreDir $f)) { Write-Ok "签名材料 $f" } else { Write-Err "签名材料缺失: $f" }
}
if ([string]::IsNullOrEmpty($keyPassword))   { Write-Err "缺少 keyPassword（应写在 harmony_toolchain.local.json）" }
if ([string]::IsNullOrEmpty($storePassword)) { Write-Err "缺少 storePassword（应写在 harmony_toolchain.local.json）" }
if ($script:Failed) { Write-Host "`n前置检查未通过，已中止。" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- 1. 决定执行路径
Write-Step "1/4 打包"

$unityRunning = @()
if (Test-Path $UnityExe) {
    $unityRunning = @(Get-Process -Name "Unity" -ErrorAction SilentlyContinue |
                      Where-Object { $_.Path -eq $UnityExe })
}

if (-not $SkipUnityExport -and $unityRunning.Count -gt 0) {
    Write-Warn2 "检测到 Unity 已在运行（PID: $($unityRunning.Id -join ',')）"
    Write-Warn2 "同一工程不能开第二个实例。推荐直接在已开的 Unity 里点："
    Write-Warn2 "    Tools/自动化打包/Harmony/Debug  或  Release"
    Write-Warn2 "那走的是同一套 AppBuilder 流水线，产物与命令行一致。"
    Write-Warn2 ""
    Write-Warn2 "若只想对已有导出目录出包，可加 -SkipUnityExport 重跑本脚本。"
    exit 2
}

if ($SkipUnityExport) {
    Write-Info "已指定 -SkipUnityExport：只跑后处理 + 出包（导出产物假定已存在）"
    if (-not (Test-Path (Join-Path $ExportDir "build-profile.json5"))) {
        Write-Err "导出目录结构异常（缺少 build-profile.json5）: $ExportDir"; exit 1
    }
} else {
    if (-not (Test-Path $UnityExe)) { Write-Err "Unity 不存在: $UnityExe"; exit 1 }
    if ($Clean -and (Test-Path $ExportDir)) {
        Write-Info "清空导出目录: $ExportDir"
        Remove-Item -Path $ExportDir -Recurse -Force
    }

    $env:HARMONY_OUT_DIR = $ExportDir
    $parentDir = Split-Path -Parent $ExportDir
    Write-Info "启动 Unity 批处理流水线（导出+补丁+签名+ohpm+hvigor，约 5 分钟）..."
    $proc = Start-Process -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-quit", "-silent-crashes",
        "-projectPath", "`"$UnityProject`"",
        "-executeMethod", "HarmonyExportBuilder.RunBatch",
        "-logFile", "`"$parentDir\unity_batch_export.log`""
    ) -PassThru -Wait -NoNewWindow
    if ($proc.ExitCode -ne 0) {
        Write-Err "Unity 流水线失败，退出码 $($proc.ExitCode)"
        Write-Host "  日志: $parentDir\unity_batch_export.log" -ForegroundColor Yellow
        exit 1
    }
    Write-Ok "Unity 流水线完成（导出+补丁+签名+ohpm+hvigor 已在 Unity 内部完成）"
}

# ---------------------------------------------------------------- 2. 仅 -SkipUnityExport 模式才需要的后处理
if ($SkipUnityExport) {
    if (-not $SkipBuild) {
        # 2.1 ArkTS 补丁重放
        Write-Step "2/4 重放 ArkTS 加固补丁"
        $patchPy = Join-Path $PSScriptRoot "harmony_patch\apply_harmony_patch.py"
        if (-not (Test-Path $patchPy)) { Write-Err "补丁脚本不存在: $patchPy"; exit 1 }
        & python $patchPy $ExportDir
        if ($LASTEXITCODE -ne 0) { Write-Err "补丁脚本执行失败 (exit=$LASTEXITCODE)"; exit 1 }
        Write-Ok "ArkTS 补丁已重放（幂等）"

        # 2.2 签名配置注入
        Write-Step "3/4 注入签名配置"
        $bp = Join-Path $ExportDir "build-profile.json5"
        $cert  = (Join-Path $KeystoreDir "hexa_debug.cer")  -replace '\\', '/'
        $prof  = (Join-Path $KeystoreDir "hexa_debugDebug.p7b") -replace '\\', '/'
        $store = (Join-Path $KeystoreDir "hexa.p12") -replace '\\', '/'
        $signBlock = @"
"signingConfigs": [
      {
        "name": "default",
        "type": "HarmonyOS",
        "material": {
          "certpath": "$cert",
          "keyAlias": "hexa",
          "keyPassword": "$keyPassword",
          "profile": "$prof",
          "signAlg": "SHA256withECDSA",
          "storeFile": "$store",
          "storePassword": "$storePassword"
        }
      }
    ]
"@
        $text = Get-Content $bp -Raw -Encoding UTF8
        if ($text -notmatch '(?s)"signingConfigs"\s*:\s*\[.*?\]') {
            Write-Err "build-profile.json5 里找不到 signingConfigs 节点"; exit 1
        }
        $text = [regex]::Replace($text, '(?s)"signingConfigs"\s*:\s*\[.*?\]', $signBlock.TrimEnd())
        Set-Content -Path $bp -Value $text -Encoding UTF8 -NoNewline
        Write-Ok "签名配置已注入"

        # 2.3 ohpm install
        Write-Step "4/4 ohpm install + hvigor 出包"
        $env:NODE_HOME = $nodeHome
        $env:DEVECO_SDK_HOME = (Join-Path $DevEcoHome "sdk")
        $env:PATH = "$nodeHome;$env:PATH"

        Push-Location $ExportDir
        try {
            if (Test-Path (Join-Path $ExportDir "oh_modules")) {
                Write-Info "oh_modules 已存在，跳过安装"
            } else {
                & $ohpm install
                if ($LASTEXITCODE -ne 0) { Write-Err "ohpm install 失败 (exit=$LASTEXITCODE)"; exit 1 }
                Write-Ok "ohpm install 完成"
            }
        } finally { Pop-Location }
    }
}

# ---------------------------------------------------------------- 3. 收集产物
if (-not $SkipBuild) {
    $hap = Get-ChildItem -Path (Join-Path $ExportDir "entry\build\default\outputs") -Filter "*.hap" -Recurse -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $hap) {
        Write-Err "未找到 .hap 产物"
        exit 1
    }
    Write-Ok "出包成功: $($hap.FullName)"
    Write-Host ("  大小: {0:N1} MB   修改时间: {1}" -f ($hap.Length / 1MB), $hap.LastWriteTime) -ForegroundColor Green
    Write-Host "`n全部完成。" -ForegroundColor Green
    Write-Host "导出工程 : $ExportDir"
    Write-Host "安装包   : $($hap.FullName)"
    exit 0
}

Write-Host "`n完成（已按 -SkipBuild 跳过打包）。" -ForegroundColor Green
Write-Host "导出工程: $ExportDir"
exit 0
