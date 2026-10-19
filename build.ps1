param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    dotnet run --project tests/ClickDesk.Core.Tests/ClickDesk.Core.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core verification failed.' }

    $packageName = if ($Runtime -eq 'win-x64') { 'ClickDesk-Windows-x64' } else { 'ClickDesk-Windows-ARM64' }
    $outputPath = Join-Path $PSScriptRoot "dist/$packageName"
    dotnet publish src/ClickDesk/ClickDesk.csproj -c Release -r $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }

    Copy-Item README.md (Join-Path $outputPath 'README.md') -Force
    Copy-Item docs/QUICKSTART.txt (Join-Path $outputPath '使用说明.txt') -Force
    Copy-Item docs/WINDOWS-TESTING.md (Join-Path $outputPath 'WINDOWS-TESTING.md') -Force
    $archive = Join-Path $PSScriptRoot "dist/$packageName.zip"
    Compress-Archive -Path $outputPath -DestinationPath $archive -Force
    Write-Host "Created: $archive"
}
finally { Pop-Location }
