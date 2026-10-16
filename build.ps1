[CmdletBinding()]
param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$sourceRoot = Join-Path $repoRoot "src"
$distRoot = Join-Path $repoRoot "dist"
$portableName = "PS图片批量水平翻转_便携版_v$Version"
$portableRoot = Join-Path $distRoot $portableName
$exePath = Join-Path $portableRoot "PS图片批量水平翻转.exe"
$zipPath = Join-Path $distRoot "$portableName.zip"

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "找不到 .NET Framework C# 编译器 csc.exe。"
}

if (Test-Path -LiteralPath $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Core.dll `
    "/resource:$sourceRoot\photoshop_batch_flip.jsx,PhotoshopBatchFlip.photoshop_batch_flip.jsx" `
    "/out:$exePath" `
    "$sourceRoot\PhotoshopBatchFlip.cs"

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exePath)) {
    throw "编译失败。"
}

Copy-Item -LiteralPath (Join-Path $repoRoot "docs\安装与使用说明.txt") -Destination $portableRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $portableRoot

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $portableRoot -DestinationPath $zipPath -CompressionLevel Optimal

$checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $distRoot "SHA256SUMS.txt") `
    -Value "$checksum  $portableName.zip" -Encoding ASCII

Write-Host "Built: $zipPath"
Write-Host "SHA256: $checksum"

