$scriptPath = Join-Path $PSScriptRoot 'scripts\git-push-ota.ps1'
& $scriptPath @args
exit $LASTEXITCODE
