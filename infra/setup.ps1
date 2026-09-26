# Creates infra/.env from .env.example with random secrets. Never overwrites an existing .env.
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
$ErrorActionPreference = 'Stop'

$envFile = Join-Path $PSScriptRoot '.env'
$exampleFile = Join-Path $PSScriptRoot '.env.example'

if (Test-Path $envFile) {
    Write-Host 'infra/.env already exists; leaving it unchanged.'
    exit 0
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

# Returns $count characters drawn uniformly from $alphabet (rejection sampling avoids modulo bias).
function Get-RandomChars([string]$alphabet, [int]$count) {
    $limit = 256 - (256 % $alphabet.Length)
    $buffer = New-Object byte[] 1
    $result = New-Object System.Text.StringBuilder
    while ($result.Length -lt $count) {
        $rng.GetBytes($buffer)
        if ($buffer[0] -lt $limit) {
            [void]$result.Append($alphabet[$buffer[0] % $alphabet.Length])
        }
    }
    return $result.ToString()
}

# 24 characters that always meet SQL Server complexity (upper, lower, digit, symbol).
# The symbols avoid characters that break .env files, shells or connection strings.
function New-Password {
    $upper = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
    $lower = 'abcdefghijklmnopqrstuvwxyz'
    $digits = '0123456789'
    return (Get-RandomChars ($upper + $lower + $digits) 20) +
        (Get-RandomChars $upper 1) +
        (Get-RandomChars $lower 1) +
        (Get-RandomChars $digits 1) +
        (Get-RandomChars '._!-' 1)
}

# 32 random bytes (256 bits), base64-encoded.
function New-JwtKey {
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

$lines = foreach ($line in [System.IO.File]::ReadAllLines($exampleFile)) {
    switch -Exact ($line) {
        'MSSQL_SA_PASSWORD=change-me'     { "MSSQL_SA_PASSWORD=$(New-Password)"; break }
        'MRA_APP_PASSWORD=change-me'      { "MRA_APP_PASSWORD=$(New-Password)"; break }
        'SYSTEM_ADMIN_PASSWORD=change-me' { "SYSTEM_ADMIN_PASSWORD=$(New-Password)"; break }
        'JWT_SIGNING_KEY=change-me'       { "JWT_SIGNING_KEY=$(New-JwtKey)"; break }
        default                           { $line }
    }
}

if ($lines -match '=change-me$') {
    Write-Error 'setup.ps1: a placeholder in .env.example has no generator; aborting.'
    exit 1
}

# UTF-8 without BOM and LF line endings, so docker compose reads the file cleanly.
$content = ($lines -join "`n") + "`n"
[System.IO.File]::WriteAllText($envFile, $content, (New-Object System.Text.UTF8Encoding($false)))

Write-Host 'Created infra/.env with random secrets.'
Write-Host 'System Admin credentials: SYSTEM_ADMIN_EMAIL and SYSTEM_ADMIN_PASSWORD in infra/.env'
