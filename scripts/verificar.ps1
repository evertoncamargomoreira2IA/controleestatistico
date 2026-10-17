param([switch]$Restore, [string]$RunName = 'verificacao')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$localDotnet = Join-Path $PWD '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
New-Item -ItemType Directory -Force -Path artifacts | Out-Null
if ($Restore) {
    & $dotnetCommand restore Qms.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar dependências.' }
}
& $dotnetCommand build Qms.slnx --no-restore -c Release '-bl:artifacts/build.binlog'
if ($LASTEXITCODE -ne 0) { throw 'Falha de compilação.' }
& $dotnetCommand test Qms.slnx --no-build --no-restore -c Release --logger "trx;LogFileName=$RunName.trx" --results-directory artifacts/TestResults
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes.' }
