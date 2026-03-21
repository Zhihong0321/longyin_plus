$scriptPath = Join-Path $PSScriptRoot 'git-push-ota.ps1'
& $scriptPath @args
exit $LASTEXITCODE
