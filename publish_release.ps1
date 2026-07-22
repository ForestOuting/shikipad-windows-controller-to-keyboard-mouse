[CmdletBinding()]
param(
    [switch]$AllowUnsigned,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$project = Join-Path $root 'ShikiPad.csproj'
$publishDir = Join-Path $root 'release\publish'
$packageDir = Join-Path $root 'release\ShikiPad'
$zipPath = Join-Path $root 'ShikiPad.zip'
$temporaryZip = Join-Path $root 'release\ShikiPad.zip'
$rootExe = Join-Path $root 'ShikiPad.exe'
$hashFile = Join-Path $root 'SHA256SUMS.txt'

$gitStatus = @(& git -C $root status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Git working-tree status."
}
$isDirty = $gitStatus.Count -gt 0
if ($isDirty -and !$AllowDirty) {
    throw "The Git working tree is not clean. Commit or discard changes before a formal release; use -AllowDirty only for an internal test package."
}
$gitCommit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve the Git commit."
}

if (Test-Path -LiteralPath $rootExe) {
    try {
        $probe = [System.IO.File]::Open($rootExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $probe.Dispose()
    } catch {
        throw "ShikiPad.exe is in use. Close the running app or dismiss its UAC prompt before packaging."
    }
}

foreach ($path in @($publishDir, $packageDir)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

& dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDir 'ShikiPad.exe'
$version = (Get-Item -LiteralPath $publishedExe).VersionInfo
if ($version.FileVersion -ne '5.2.0.0') {
    throw "Unexpected file version $($version.FileVersion); expected 5.2.0.0."
}

$signature = Get-AuthenticodeSignature -LiteralPath $publishedExe
if (!$AllowUnsigned -and $signature.Status -ne 'Valid') {
    throw "ShikiPad.exe failed Authenticode validation ($($signature.Status)). Use -AllowUnsigned only for an internal test package."
}

$packageFiles = @(
    'install_driver.bat',
    'interception.dll',
    'README.md',
    'RELEASE_NOTES.md',
    'THIRD_PARTY_NOTICES.md',
    'shiki.ico',
    'ShikiPad.manifest'
)
foreach ($relativePath in $packageFiles) {
    Copy-Item -LiteralPath (Join-Path $root $relativePath) -Destination (Join-Path $packageDir $relativePath) -Force
}
Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $packageDir 'ShikiPad.exe') -Force
@(
    'Version: 5.2.0',
    "GitCommit: $gitCommit",
    "WorkingTreeDirty: $isDirty"
) | Set-Content -LiteralPath (Join-Path $packageDir 'BUILD_INFO.txt') -Encoding ascii

$driverDir = Join-Path $packageDir 'driver'
New-Item -ItemType Directory -Path $driverDir | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'driver\install-interception.exe') -Destination $driverDir -Force

$hashLines = Get-ChildItem -LiteralPath $packageDir -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($packageDir.Length + 1).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relative"
    }
$hashLines | Set-Content -LiteralPath (Join-Path $packageDir 'SHA256SUMS.txt') -Encoding utf8

if (Test-Path -LiteralPath $temporaryZip) {
    Remove-Item -LiteralPath $temporaryZip -Force
}
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $temporaryZip -CompressionLevel Optimal

Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
Copy-Item -LiteralPath (Join-Path $packageDir 'SHA256SUMS.txt') -Destination $hashFile -Force
Move-Item -LiteralPath $temporaryZip -Destination $zipPath -Force

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "ShikiPad V5.2 package created: $zipPath"
Write-Host "ZIP SHA256: $zipHash"
