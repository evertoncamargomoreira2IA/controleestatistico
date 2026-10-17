param([int]$Port = 5188)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget/packages'
$localDotnet = Join-Path $PWD '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }
& $dotnetCommand run --project src/Qms.Web -c Release --no-launch-profile --urls "http://127.0.0.1:$Port"
