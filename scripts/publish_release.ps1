[CmdletBinding()]
param(
    [switch]$AllowUnsigned,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'ShikiPad.csproj'
$releaseDir = Join-Path $root 'release'
$publishDir = Join-Path $root 'release\publish'
$packageDir = Join-Path $root 'release\ShikiPad'
$zipPath = Join-Path $root 'ShikiPad.zip'
$temporaryZip = Join-Path $releaseDir 'ShikiPad.tmp.zip'
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
    @{ Source = 'install_driver.bat'; Destination = 'install_driver.bat' },
    @{ Source = 'interception.dll'; Destination = 'interception.dll' },
    @{ Source = 'README.md'; Destination = 'README.md' },
    @{ Source = 'docs\RELEASE_NOTES.md'; Destination = 'RELEASE_NOTES.md' },
    @{ Source = 'docs\THIRD_PARTY_NOTICES.md'; Destination = 'THIRD_PARTY_NOTICES.md' },
    @{ Source = 'assets\shiki.ico'; Destination = 'shiki.ico' },
    @{ Source = 'assets\ShikiPad.manifest'; Destination = 'ShikiPad.manifest' }
)
foreach ($packageFile in $packageFiles) {
    Copy-Item -LiteralPath (Join-Path $root $packageFile.Source) -Destination (Join-Path $packageDir $packageFile.Destination) -Force
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
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open(
    $temporaryZip,
    [System.IO.Compression.ZipArchiveMode]::Create
)
try {
    Get-ChildItem -LiteralPath $packageDir -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($packageDir.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $_.FullName,
                $relative,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
} finally {
    $archive.Dispose()
}

Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
Copy-Item -LiteralPath (Join-Path $packageDir 'SHA256SUMS.txt') -Destination $hashFile -Force
Move-Item -LiteralPath $temporaryZip -Destination $zipPath -Force

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($path in @($publishDir, $packageDir)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
if ((Test-Path -LiteralPath $releaseDir) -and @(Get-ChildItem -LiteralPath $releaseDir -Force).Count -eq 0) {
    Remove-Item -LiteralPath $releaseDir -Force
}
Write-Host "ShikiPad V5.2 package created: $zipPath"
Write-Host "ZIP SHA256: $zipHash"
Write-Host "Upload this ZIP as a GitHub Release asset; do not commit it to the repository."
