param(
    [string]$GameRoot = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms

$gameExeName = "LongYinLiZhiZhuan.exe"
$steamAppId = "3202030"

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path $Path)) {
        throw "$Label not found: $Path"
    }
}

function Test-GameRoot {
    param([string]$Path)

    return (Test-Path (Join-Path $Path $gameExeName))
}

function Get-SteamInstallRoots {
    $roots = New-Object System.Collections.Generic.List[string]

    $registryKeys = @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )

    foreach ($key in $registryKeys) {
        if (Test-Path $key) {
            $steamPath = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).SteamPath
            if (-not [string]::IsNullOrWhiteSpace($steamPath) -and (Test-Path $steamPath)) {
                $roots.Add($steamPath)
            }
        }
    }

    $basePaths = @(
        ${env:ProgramFiles(x86)},
        $env:ProgramFiles,
        $env:LOCALAPPDATA
    )

    foreach ($basePath in $basePaths) {
        if ([string]::IsNullOrWhiteSpace($basePath)) {
            continue
        }

        $candidate = Join-Path $basePath 'Steam'
        if (Test-Path $candidate) {
            $roots.Add($candidate)
        }
    }

    return $roots | Select-Object -Unique
}

function Get-SteamLibraryRoots {
    $libraryRoots = New-Object System.Collections.Generic.List[string]

    foreach ($steamRoot in Get-SteamInstallRoots) {
        if (-not (Test-Path $steamRoot)) {
            continue
        }

        $libraryRoots.Add($steamRoot)

        $libraryFoldersVdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path $libraryFoldersVdf)) {
            continue
        }

        foreach ($match in [regex]::Matches((Get-Content -Path $libraryFoldersVdf -Raw), '(?m)^\s*"path"\s*"([^"]+)"\s*$')) {
            $path = $match.Groups[1].Value -replace '\\\\', '\'
            if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
                $libraryRoots.Add($path)
            }
        }
    }

    return $libraryRoots | Select-Object -Unique
}

function Get-AutoDetectedGameRoot {
    foreach ($libraryRoot in Get-SteamLibraryRoots) {
        $manifestPath = Join-Path $libraryRoot "steamapps\appmanifest_$steamAppId.acf"
        if (-not (Test-Path $manifestPath)) {
            continue
        }

        $manifest = Get-Content -Path $manifestPath -Raw
        $installDirMatch = [regex]::Match($manifest, '(?m)^\s*"installdir"\s*"([^"]+)"\s*$')
        if (-not $installDirMatch.Success) {
            continue
        }

        $installDir = $installDirMatch.Groups[1].Value
        $candidate = Join-Path $libraryRoot "steamapps\common\$installDir"
        if (Test-GameRoot $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Get-SelectedGameRoot {
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Select the game folder that contains LongYinLiZhiZhuan.exe"
    $dialog.ShowNewFolderButton = $false

    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        throw "Uninstall canceled."
    }

    return $dialog.SelectedPath
}

function Resolve-GameRoot {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path $RequestedPath -ErrorAction Stop
        $item = Get-Item $resolved.Path

        if ($item.PSIsContainer) {
            $candidate = $item.FullName
        }
        else {
            $candidate = Split-Path $item.FullName -Parent
        }

        Assert-PathExists -Path (Join-Path $candidate $gameExeName) -Label $gameExeName
        return (Resolve-Path $candidate).Path
    }

    if (Test-GameRoot $PSScriptRoot) {
        return (Resolve-Path $PSScriptRoot).Path
    }

    $autoDetectedRoot = Get-AutoDetectedGameRoot
    if (-not [string]::IsNullOrWhiteSpace($autoDetectedRoot)) {
        return $autoDetectedRoot
    }

    $selected = Resolve-Path (Get-SelectedGameRoot)
    Assert-PathExists -Path (Join-Path $selected.Path $gameExeName) -Label $gameExeName
    return $selected.Path
}

function Confirm-Uninstall {
    param([string]$TargetPath)

    if ($Force) {
        return
    }

    Write-Host ""
    Write-Host "Target game folder:"
    Write-Host $TargetPath
    Write-Host ""
    $answer = Read-Host "Type UNINSTALL to remove the mod from this folder"
    if ($answer -ne "UNINSTALL") {
        throw "Uninstall canceled."
    }
}

function Remove-PathIfPresent {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }
}

function Remove-MatchingFiles {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        Get-ChildItem -Path $Root -Force -File -Filter $pattern -ErrorAction SilentlyContinue | Remove-Item -Force
    }
}

function Restore-SteamAppId {
    param([string]$TargetRoot)

    $steamAppIdPath = Join-Path $TargetRoot "steam_appid.txt"
    $backupPath = "$steamAppIdPath.bak"

    if (Test-Path $backupPath) {
        Copy-Item -Path $backupPath -Destination $steamAppIdPath -Force
        Remove-Item -Path $backupPath -Force
        return
    }

    if (Test-Path $steamAppIdPath) {
        Remove-Item -Path $steamAppIdPath -Force
    }
}

$targetRoot = Resolve-GameRoot -RequestedPath $GameRoot
Assert-PathExists -Path (Join-Path $targetRoot $gameExeName) -Label $gameExeName
Confirm-Uninstall -TargetPath $targetRoot

Write-Host ""
Write-Host "Removing mod files from:"
Write-Host $targetRoot
Write-Host ""

Restore-SteamAppId -TargetRoot $targetRoot

Remove-PathIfPresent -Path (Join-Path $targetRoot "BepInEx")
Remove-PathIfPresent -Path (Join-Path $targetRoot "dotnet")

Remove-MatchingFiles -Root $targetRoot -Patterns @(
    ".doorstop_version",
    "doorstop_config.ini",
    "winhttp.dll",
    "LaunchGame.cmd",
    "LongYinModControl.cmd",
    "LongYinModControl.ps1",
    "Play.cmd",
    "Install.cmd",
    "Uninstall.cmd",
    "Uninstall.ps1",
    "run_this_first.cmd",
    "run_this_first.ps1",
    "README.md",
    "安装与运行说明.md",
    "LongYin-Mod-Portable-*.zip",
    "LongYinPlus-InstallBundle-*.zip"
)

Write-Host "Uninstall complete."
Write-Host "The game folder now contains only the remaining base-game files and any user data you already had."
