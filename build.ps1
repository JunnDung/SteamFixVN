$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:APPDATA = Join-Path $env:DOTNET_CLI_HOME 'AppData'
$env:LOCALAPPDATA = Join-Path $env:DOTNET_CLI_HOME 'LocalAppData'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore tests/SteamFixVN.Tests.csproj --configfile "$PSScriptRoot/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Restore tests failed' }
New-Item -ItemType Directory -Force dist | Out-Null
dotnet run --project tests/SteamFixVN.Tests.csproj -c Release --no-restore | Tee-Object -FilePath dist/file-tests.txt
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet restore src/SteamFixVN.csproj -r win-x64 -p:SelfContained=true --configfile "$PSScriptRoot/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Restore app failed' }
dotnet publish src/SteamFixVN.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item -ItemType Directory -Force dist/licenses,dist/third-party-sources | Out-Null
Copy-Item vendor/engine/licenses/* dist/licenses -Force
Copy-Item vendor/sources/* dist/third-party-sources -Force
Copy-Item vendor/THIRD-PARTY-NOTICES.md dist -Force
Copy-Item README.md dist/HUONG-DAN.md -Force
Copy-Item LICENSE dist -Force
Copy-Item RESEARCH-STEAM-VN.md dist -Force
$releaseHash = Get-FileHash dist/SteamFixVN.exe -Algorithm SHA256
($releaseHash.Hash + '  SteamFixVN.exe') | Set-Content dist/SHA256.txt -Encoding ASCII
$releaseHash | Format-List
