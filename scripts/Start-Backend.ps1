param(
    [string]$GoPath = '',
    [string]$Address = '127.0.0.1:8080',
    [string]$Database = '',
    [string]$Origins = 'http://localhost:5173,http://127.0.0.1:5173'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $GoPath) {
    $goCommand = Get-Command go -ErrorAction SilentlyContinue
    if ($goCommand) { $GoPath = $goCommand.Source }
    elseif (Test-Path -LiteralPath 'D:\environment\Go\bin\go.exe') { $GoPath = 'D:\environment\Go\bin\go.exe' }
    else { throw 'Go not found. Pass -GoPath C:\path\to\go.exe.' }
}
if (-not $Database) { $Database = Join-Path $projectRoot 'backend\data\inspection.db' }
Push-Location (Join-Path $projectRoot 'backend')
try {
    & $GoPath run ./cmd/server -addr $Address -db $Database -origins $Origins
    if ($LASTEXITCODE -ne 0) { throw "Backend exited with code $LASTEXITCODE" }
} finally { Pop-Location }
