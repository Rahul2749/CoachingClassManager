$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $projectRoot 'api/CoachingClassManager.Api/appsettings.Local.json'
$envPath = Join-Path $projectRoot 'infra/.env'
if ((Test-Path -LiteralPath $configPath) -or (Test-Path -LiteralPath $envPath)) {
    throw 'Local configuration already exists. Preserve its database credentials; edit it manually if needed.'
}
function New-Secret {
    [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}
$dbPassword = New-Secret
$configuration = @{
    ConnectionStrings = @{ Database = "Host=localhost;Port=15432;Database=coaching;Username=coaching;Password=$dbPassword" }
    Auth = @{ SigningKey = (New-Secret); OtpPepper = (New-Secret); EnableDevelopmentOtp = $true }
}
$configuration | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $configPath -Encoding utf8
"POSTGRES_PASSWORD=$dbPassword" | Set-Content -LiteralPath $envPath -Encoding utf8
$appEnv = Join-Path $projectRoot 'app/.env'
if (!(Test-Path -LiteralPath $appEnv)) {
    'EXPO_PUBLIC_API_URL=http://10.0.2.2:5080' | Set-Content -LiteralPath $appEnv -Encoding utf8
}
Write-Output 'Created ignored local configuration with independent random secrets. No secrets were printed.'
