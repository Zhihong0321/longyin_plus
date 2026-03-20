param(
    [string]$GameRoot = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms

$repoRoot = $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$gameExeName = "LongYinLiZhiZhuan.exe"
$steamAppId = "3202030"

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
        throw "Installation canceled."
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

    $autoDetectedRoot = Get-AutoDetectedGameRoot
    if (-not [string]::IsNullOrWhiteSpace($autoDetectedRoot)) {
        return $autoDetectedRoot
    }

    $selected = Resolve-Path (Get-SelectedGameRoot)
    Assert-PathExists -Path (Join-Path $selected.Path $gameExeName) -Label $gameExeName
    return $selected.Path
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

function Ensure-SteamAppId {
    param([string]$TargetRoot)

    $steamAppIdPath = Join-Path $TargetRoot "steam_appid.txt"

    if (Test-Path $steamAppIdPath) {
        $currentValue = (Get-Content -Path $steamAppIdPath -Raw).Trim()
        if ($currentValue -ne $steamAppId) {
            Copy-Item -Path $steamAppIdPath -Destination "$steamAppIdPath.bak" -Force
        }
    }

    Set-Content -Path $steamAppIdPath -Value $steamAppId -Encoding ASCII
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

Get-ChildItem -Path $distRoot -Force | Where-Object { $_.Name -notlike "*.zip" } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $targetRoot -Recurse -Force
}
Ensure-SteamAppId -TargetRoot $targetRoot

Write-Host "Install complete."
Write-Host "You can now run LongYinModControl.cmd from the game root."
