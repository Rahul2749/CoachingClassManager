$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$config = Get-Content -LiteralPath (Join-Path $projectRoot 'api/CoachingClassManager.Api/appsettings.Local.json') -Raw | ConvertFrom-Json
$env:COACHING_TEST_DATABASE = $config.ConnectionStrings.Database
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.cache/dotnet'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
try {
    dotnet test (Join-Path $projectRoot 'api/CoachingClassManager.slnx') --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'API tests failed.' }
} finally {
    Remove-Item Env:COACHING_TEST_DATABASE -ErrorAction SilentlyContinue
}
