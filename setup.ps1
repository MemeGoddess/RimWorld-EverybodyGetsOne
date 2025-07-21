$scriptPath = ".github/actions/download-steam-workshop/setup.ps1"

if (Test-Path $scriptPath) {
    Write-Host "Running script: $scriptPath"
    & $scriptPath
} else {
    Write-Error "Script not found at path: $scriptPath"
}