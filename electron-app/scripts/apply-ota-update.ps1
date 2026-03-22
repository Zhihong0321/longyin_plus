param(
    [Parameter(Mandatory = $true)]
    [int]$WaitPid,

    [Parameter(Mandatory = $true)]
    [string]$StageRoot,

    [Parameter(Mandatory = $true)]
    [string]$TargetRoot,

    [Parameter(Mandatory = $true)]
    [string]$AppExecutableName,

    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Log {
    param([string]$Message)

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $folder = Split-Path -Parent $LogPath
    if ($folder) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    Add-Content -Path $LogPath -Value "[$timestamp] $Message"
}

function Wait-ForProcessExit {
    param(
        [int]$PidToWait,
        [string]$ProcessName,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $byPid = Get-Process -Id $PidToWait -ErrorAction SilentlyContinue
        $byName = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)

        if (-not $byPid -and $byName.Count -eq 0) {
            return
        }

        if ($byPid) {
            Write-Log "Waiting for updater owner process $PidToWait to exit..."
        }
        elseif ($byName.Count -gt 0) {
            Write-Log "Waiting for remaining $ProcessName processes to exit: $($byName.Count)"
        }

        Start-Sleep -Seconds 1
    }

    throw "Timed out waiting for $ProcessName to exit."
}

trap {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}

$processName = [System.IO.Path]::GetFileNameWithoutExtension($AppExecutableName)
$targetExePath = Join-Path $TargetRoot $AppExecutableName

Write-Log "OTA helper started. waitPid=$WaitPid stageRoot=$StageRoot targetRoot=$TargetRoot appExe=$AppExecutableName"

if (-not (Test-Path -LiteralPath $StageRoot)) {
    throw "Stage root does not exist: $StageRoot"
}

if (-not (Test-Path -LiteralPath $TargetRoot)) {
    throw "Target root does not exist: $TargetRoot"
}

Wait-ForProcessExit -PidToWait $WaitPid -ProcessName $processName -TimeoutSeconds 90

Write-Log "Applying staged update with robocopy..."
& robocopy.exe $StageRoot $TargetRoot /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
$robocopyExitCode = $LASTEXITCODE
Write-Log "Robocopy exit code: $robocopyExitCode"

if ($robocopyExitCode -gt 7) {
    throw "Robocopy failed with exit code $robocopyExitCode."
}

if (-not (Test-Path -LiteralPath $targetExePath)) {
    throw "Updated executable not found: $targetExePath"
}

Write-Log "Cleaning staged update folder..."
Remove-Item -LiteralPath $StageRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Log "Relaunching updated app..."
Start-Process -FilePath $targetExePath -WorkingDirectory $TargetRoot | Out-Null
Write-Log "OTA helper completed successfully."
