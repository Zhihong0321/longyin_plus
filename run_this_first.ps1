param(
    [string]$GameRoot = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms

$repoRoot = $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$gameExeName = "LongYinLiZhiZhuan.exe"

function Get-SelectedGameRoot {
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Select the game folder that contains LongYinLiZhiZhuan.exe"
    $dialog.ShowNewFolderButton = $false

    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        throw "Installation canceled."
    }

    return $dialog.SelectedPath
}

function Resolve-GameRoot {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path $RequestedPath).Path
    }

    return (Resolve-Path (Get-SelectedGameRoot)).Path
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path $Path)) {
        throw "$Label not found: $Path"
    }
}

function Confirm-Install {
    param([string]$TargetPath)

    if ($Force) {
        return
    }

    Write-Host ""
    Write-Host "Target game folder:"
    Write-Host $TargetPath
    Write-Host ""
    $answer = Read-Host "Type INSTALL to copy dist into this folder"
    if ($answer -ne "INSTALL") {
        throw "Installation canceled."
    }
}

Assert-PathExists -Path $distRoot -Label "dist folder"

$targetRoot = Resolve-GameRoot -RequestedPath $GameRoot
$targetExe = Join-Path $targetRoot $gameExeName

Assert-PathExists -Path $targetExe -Label $gameExeName
Confirm-Install -TargetPath $targetRoot

Write-Host ""
Write-Host "Copying dist payload into:"
Write-Host $targetRoot
Write-Host ""

Copy-Item -Path (Join-Path $distRoot "*") -Destination $targetRoot -Recurse -Force

Write-Host "Install complete."
Write-Host "You can now run LongYinModControl.ps1 from the game root."
