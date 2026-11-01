$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location (Join-Path $projectRoot 'admin')
try {
    # A leftover node_modules directory does not mean installation is complete.
    # npm ls checks locked/direct dependency versions; also verify Windows launchers.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & npm.cmd ls --depth=0 --include=dev *> $null
        $dependenciesValid = $LASTEXITCODE -eq 0
    } finally { $ErrorActionPreference = $previousPreference }
    $launchersPresent = (Test-Path -LiteralPath 'node_modules\.bin\vite.cmd') -and
        (Test-Path -LiteralPath 'node_modules\.bin\tsc.cmd')
    if (-not $dependenciesValid -or -not $launchersPresent) {
        Write-Host 'Frontend dependencies are missing or incomplete. Restoring from package-lock.json...'
        & npm.cmd ci --include=dev --bin-links=true
        if ($LASTEXITCODE -ne 0) {
            throw 'Dependency restore failed. If npm reports EPERM, close this project''s existing Vite/dev-server terminals (Ctrl+C), then retry. See the npm error above for details.'
        }
    }
    & npm.cmd run dev
    if ($LASTEXITCODE -ne 0) { throw "Admin exited with code $LASTEXITCODE" }
} finally { Pop-Location }
