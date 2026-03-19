Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic

$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Runtime.InteropServices;

public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

public static class LongYinOverlayNative
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
"@

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$configPath = Join-Path $repoRoot "BepInEx\config\codex.longyin.staminalock.cfg"
$logPath = Join-Path $repoRoot "BepInEx\LogOutput.log"
$overlayStatePath = Join-Path $PSScriptRoot "LongYinOverlay.state.json"
$questSnapshotPath = Join-Path $PSScriptRoot "LongYinQuestSnapshot.json"
$gameExeName = "LongYinLiZhiZhuan"
$overlayHotkeyText = "Ctrl+Shift+F8"
$overlayHotkeyKey = [System.Windows.Forms.Keys]::F8

$script:isHotkeyDown = $false
$script:lastStatusMessage = "Ready"
$script:overlayMode = "panel"
$script:state = $null
$script:logicalVisible = $true
$script:panelDirty = $false
$script:suppressDirtyEvents = $false

function New-DefaultOverlayState {
    return [ordered]@{
        OverlayVisible = $true
        LastMode = "panel"
        NotesText = "Use this for route reminders, crafting goals, or NPC notes."
        OverlayHotkey = $overlayHotkeyText
    }
}

function Read-OverlayState {
    $state = New-DefaultOverlayState

    if (-not (Test-Path $overlayStatePath)) {
        return $state
    }

    try {
        $parsed = Get-Content -Path $overlayStatePath -Raw | ConvertFrom-Json
        foreach ($key in $state.Keys) {
            $property = $parsed.PSObject.Properties[$key]
            if ($null -ne $property -and $null -ne $property.Value) {
                $state[$key] = $property.Value
            }
        }
    }
    catch {
    }

    return $state
}

function Save-OverlayState {
    param([hashtable]$State)

    $json = $State | ConvertTo-Json -Depth 4
    Set-Content -Path $overlayStatePath -Value $json -Encoding UTF8
}

function Get-ConfigText {
    if (-not (Test-Path $configPath)) {
        return $null
    }

    return Get-Content -Path $configPath -Raw
}

function Get-BoolValue {
    param(
        [string]$Text,
        [string]$Name,
        [bool]$DefaultValue
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $DefaultValue
    }

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Name))\s*=\s*(true|false)\s*$")
    if ($match.Success) {
        return [bool]::Parse($match.Groups[1].Value)
    }

    return $DefaultValue
}

function Get-IntValue {
    param(
        [string]$Text,
        [string]$Name,
        [int]$DefaultValue
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $DefaultValue
    }

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Name))\s*=\s*(-?\d+)\s*$")
    if ($match.Success) {
        return [int]::Parse($match.Groups[1].Value)
    }

    return $DefaultValue
}

function Get-StringValue {
    param(
        [string]$Text,
        [string]$Name,
        [string]$DefaultValue
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $DefaultValue
    }

    $match = [regex]::Match($Text, "(?m)^\s*$([regex]::Escape($Name))\s*=\s*(.+?)\s*$")
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return $DefaultValue
}

function Get-ConfigSnapshot {
    $text = Get-ConfigText
    if ([string]::IsNullOrWhiteSpace($text)) {
        return [pscustomobject]@{
            Available = $false
            LockStamina = $false
            ExpMultiplier = 1
            PointMultiplier = 1
            TraceMode = $false
            FreezeDate = $false
            FreezeHotkey = "F10"
            SpeedHotkey = "F11"
        }
    }

    return [pscustomobject]@{
        Available = $true
        LockStamina = Get-BoolValue $text "LockStamina" $true
        ExpMultiplier = Get-IntValue $text "ExpMultiplier" 1
        PointMultiplier = Get-IntValue $text "PointMultiplier" 1
        TraceMode = Get-BoolValue $text "TraceMode" $false
        FreezeDate = Get-BoolValue $text "FreezeDate" $false
        FreezeHotkey = Get-StringValue $text "ToggleFreezeDateHotkey" "F10"
        SpeedHotkey = Get-StringValue $text "CycleOutsideBattleSpeedHotkey" "F11"
    }
}

function Set-IniValue {
    param(
        [string]$Text,
        [string]$Name,
        [string]$Value
    )

    $pattern = "(?m)^(\s*$([regex]::Escape($Name))\s*=\s*).*$"
    $match = [regex]::Match($Text, $pattern)
    if ($match.Success) {
        return $Text.Remove($match.Index, $match.Length).Insert($match.Index, $match.Groups[1].Value + $Value)
    }

    $trimmed = $Text.TrimEnd()
    if ($trimmed.Length -gt 0) {
        $trimmed += "`r`n"
    }

    return $trimmed + "$Name = $Value`r`n"
}

function Save-ConfigSnapshot {
    param(
        [bool]$LockStamina,
        [int]$ExpMultiplier,
        [int]$PointMultiplier,
        [bool]$TraceMode,
        [bool]$FreezeDate
    )

    $text = Get-ConfigText
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Config file not found at $configPath"
    }

    $safeExp = [Math]::Max(1, [Math]::Min(999, $ExpMultiplier))
    $safePoints = [Math]::Max(1, [Math]::Min(999, $PointMultiplier))

    $text = Set-IniValue $text "LockStamina" ($LockStamina.ToString().ToLowerInvariant())
    $text = Set-IniValue $text "ExpMultiplier" $safeExp
    $text = Set-IniValue $text "PointMultiplier" $safePoints
    $text = Set-IniValue $text "TraceMode" ($TraceMode.ToString().ToLowerInvariant())
    $text = Set-IniValue $text "FreezeDate" ($FreezeDate.ToString().ToLowerInvariant())

    Set-Content -Path $configPath -Value $text -Encoding ASCII
}

function Convert-FullscreenModeName {
    param([Nullable[int]]$Mode)

    if ($null -eq $Mode) {
        return "unknown"
    }

    switch ($Mode.Value) {
        0 { return "exclusive fullscreen" }
        1 { return "fullscreen window" }
        2 { return "maximized window" }
        3 { return "windowed" }
        default { return "mode $($Mode.Value)" }
    }
}

function Get-UnityFullscreenMode {
    try {
        $registry = Get-ItemProperty "HKCU:\Software\TppStudio\LongYinLiZhiZhuan" -ErrorAction Stop
        if ($null -ne $registry.'Screenmanager Fullscreen mode_h3630240806') {
            return [int]$registry.'Screenmanager Fullscreen mode_h3630240806'
        }
    }
    catch {
    }

    return $null
}

function Get-GameInfo {
    $process = Get-Process -Name $gameExeName -ErrorAction SilentlyContinue | Select-Object -First 1
    $mode = Get-UnityFullscreenMode
    $modeName = Convert-FullscreenModeName $mode
    $supportsOverlay = ($mode -eq 1 -or $mode -eq 3)

    if ($null -eq $process) {
        return [pscustomobject]@{
            Running = $false
            ProcessId = 0
            Handle = [IntPtr]::Zero
            SupportsOverlay = $false
            ModeName = $modeName
            IsCompanion = $true
            Bounds = $null
            Warning = "Game not running. Overlay stays in companion mode."
        }
    }

    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero -or -not [LongYinOverlayNative]::IsWindowVisible($handle) -or [LongYinOverlayNative]::IsIconic($handle)) {
        return [pscustomobject]@{
            Running = $true
            ProcessId = $process.Id
            Handle = $handle
            SupportsOverlay = $false
            ModeName = $modeName
            IsCompanion = $true
            Bounds = $null
            Warning = "Game window is not ready. Overlay stays in companion mode."
        }
    }

    $rect = New-Object RECT
    [void][LongYinOverlayNative]::GetWindowRect($handle, [ref]$rect)

    $warning = if ($supportsOverlay) {
        "Attached to game window."
    }
    else {
        "Display mode '$modeName' may block overlays. Using companion placement."
    }

    return [pscustomobject]@{
        Running = $true
        ProcessId = $process.Id
        Handle = $handle
        SupportsOverlay = $supportsOverlay
        ModeName = $modeName
        IsCompanion = -not $supportsOverlay
        Bounds = $rect
        Warning = $warning
    }
}

function Get-SendKeysToken {
    param([string]$HotkeyName)

    switch ($HotkeyName.ToUpperInvariant()) {
        "F1" { return "{F1}" }
        "F2" { return "{F2}" }
        "F3" { return "{F3}" }
        "F4" { return "{F4}" }
        "F5" { return "{F5}" }
        "F6" { return "{F6}" }
        "F7" { return "{F7}" }
        "F8" { return "{F8}" }
        "F9" { return "{F9}" }
        "F10" { return "{F10}" }
        "F11" { return "{F11}" }
        "F12" { return "{F12}" }
        default { return $null }
    }
}

function Set-StatusMessage {
    param([string]$Message)

    $script:lastStatusMessage = $Message
    $statusValueLabel.Text = $Message
}

function Load-ConfigIntoControls {
    param([pscustomobject]$Config)

    $script:suppressDirtyEvents = $true
    try {
        $lockStaminaCheckbox.Checked = $Config.LockStamina
        $traceModeCheckbox.Checked = $Config.TraceMode
        $freezeDateCheckbox.Checked = $Config.FreezeDate
        $expMultiplierBox.Value = [decimal][Math]::Max($expMultiplierBox.Minimum, [Math]::Min($expMultiplierBox.Maximum, $Config.ExpMultiplier))
        $pointMultiplierBox.Value = [decimal][Math]::Max($pointMultiplierBox.Minimum, [Math]::Min($pointMultiplierBox.Maximum, $Config.PointMultiplier))
    }
    finally {
        $script:suppressDirtyEvents = $false
    }
}

function Invoke-GameHotkey {
    param(
        [string]$HotkeyName,
        [string]$ActionLabel
    )

    $token = Get-SendKeysToken $HotkeyName
    if ([string]::IsNullOrWhiteSpace($token)) {
        Set-StatusMessage "$ActionLabel unavailable: unsupported hotkey $HotkeyName."
        return
    }

    $gameInfo = Get-GameInfo
    if (-not $gameInfo.Running -or $gameInfo.Handle -eq [IntPtr]::Zero) {
        Set-StatusMessage "$ActionLabel unavailable: game not running."
        return
    }

    try {
        [void][LongYinOverlayNative]::ShowWindowAsync($gameInfo.Handle, [LongYinOverlayNative]::SW_RESTORE)
        Start-Sleep -Milliseconds 80
        [void][LongYinOverlayNative]::SetForegroundWindow($gameInfo.Handle)
        [void][Microsoft.VisualBasic.Interaction]::AppActivate($gameInfo.ProcessId)
        Start-Sleep -Milliseconds 120
        [System.Windows.Forms.SendKeys]::SendWait($token)
        Set-StatusMessage "$ActionLabel sent to game via $HotkeyName."
    }
    catch {
        Set-StatusMessage "$ActionLabel failed: $($_.Exception.Message)"
    }
}

function Test-OverlayHotkeyPressed {
    $ctrlDown = ([LongYinOverlayNative]::GetAsyncKeyState([int][System.Windows.Forms.Keys]::ControlKey) -band 0x8000) -ne 0
    $shiftDown = ([LongYinOverlayNative]::GetAsyncKeyState([int][System.Windows.Forms.Keys]::ShiftKey) -band 0x8000) -ne 0
    $keyDown = ([LongYinOverlayNative]::GetAsyncKeyState([int]$overlayHotkeyKey) -band 0x8000) -ne 0
    $down = $ctrlDown -and $shiftDown -and $keyDown

    if ($down -and -not $script:isHotkeyDown) {
        $script:isHotkeyDown = $true
        return $true
    }

    if (-not $down) {
        $script:isHotkeyDown = $false
    }

    return $false
}

function Set-FormClickThrough {
    param(
        [System.Windows.Forms.Form]$Form,
        [bool]$Enabled
    )

    $style = [LongYinOverlayNative]::GetWindowLong($Form.Handle, [LongYinOverlayNative]::GWL_EXSTYLE)
    $style = $style -bor [LongYinOverlayNative]::WS_EX_LAYERED -bor [LongYinOverlayNative]::WS_EX_TOOLWINDOW

    if ($Enabled) {
        $style = $style -bor [LongYinOverlayNative]::WS_EX_TRANSPARENT
    }
    else {
        $style = $style -band (-bnot [LongYinOverlayNative]::WS_EX_TRANSPARENT)
    }

    [void][LongYinOverlayNative]::SetWindowLong($Form.Handle, [LongYinOverlayNative]::GWL_EXSTYLE, $style)
}

function Get-LatestOverlayLogLine {
    if (-not (Test-Path $logPath)) {
        return "No BepInEx log yet."
    }

    try {
        $tail = Get-Content -Path $logPath -Tail 120
        $interesting = $tail | Where-Object {
            $_ -match "LongYin" -or $_ -match "Freeze" -or $_ -match "Outside-battle speed"
        }

        if ($interesting.Count -gt 0) {
            return $interesting[-1].Trim()
        }

        return $tail[-1].Trim()
    }
    catch {
        return "Unable to read BepInEx log."
    }
}

function Get-QuestSnapshot {
    if (-not (Test-Path $questSnapshotPath)) {
        return [pscustomobject]@{
            Available = $false
            Status = "Quest snapshot file not created yet."
            WorldDate = ""
            PlayerName = ""
            Counts = [pscustomobject]@{
                missions = 0
                forceMissions = 0
                worldEvents = 0
                areaEvents = 0
                bigMapEvents = 0
                plotEvents = 0
                activeScene = 0
                total = 0
            }
            Entries = @()
        }
    }

    try {
        return Get-Content -Path $questSnapshotPath -Raw | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{
            Available = $false
            Status = "Quest snapshot unreadable: $($_.Exception.Message)"
            WorldDate = ""
            PlayerName = ""
            Counts = [pscustomobject]@{
                missions = 0
                forceMissions = 0
                worldEvents = 0
                areaEvents = 0
                bigMapEvents = 0
                plotEvents = 0
                activeScene = 0
                total = 0
            }
            Entries = @()
        }
    }
}

function Format-QuestEntryLine {
    param([pscustomobject]$Entry)

    $parts = @()
    if (-not [string]::IsNullOrWhiteSpace($Entry.location) -and $Entry.location -ne "(location unknown)") {
        $parts += $Entry.location
    }

    if (-not [string]::IsNullOrWhiteSpace($Entry.leftTime)) {
        $parts += $Entry.leftTime
    }

    if (-not [string]::IsNullOrWhiteSpace($Entry.status) -and $Entry.status -ne "(status unknown)") {
        $parts += $Entry.status
    }

    $meta = if ($parts.Count -gt 0) { " | " + ($parts -join " | ") } else { "" }
    return "[$($Entry.category)] $($Entry.name)$meta"
}

function Format-QuestSnapshotText {
    param([pscustomobject]$Snapshot)

    if (-not $Snapshot.Available) {
        return $Snapshot.Status
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Tracked total: $($Snapshot.Counts.total)")
    $lines.Add("Missions $($Snapshot.Counts.missions) | Force $($Snapshot.Counts.forceMissions) | World $($Snapshot.Counts.worldEvents)")
    $lines.Add("Area $($Snapshot.Counts.areaEvents) | Travel $($Snapshot.Counts.bigMapEvents) | Plot $($Snapshot.Counts.plotEvents) | Active $($Snapshot.Counts.activeScene)")

    foreach ($entry in @($Snapshot.Entries) | Select-Object -First 12) {
        $lines.Add((Format-QuestEntryLine $entry))
    }

    if (@($Snapshot.Entries).Count -gt 12) {
        $lines.Add("... plus $((@($Snapshot.Entries).Count) - 12) more entries")
    }

    return ($lines -join [Environment]::NewLine)
}

function Get-PanelBounds {
    param([pscustomobject]$GameInfo)

    $width = 360
    $height = 920

    if ($GameInfo.Running -and -not $GameInfo.IsCompanion -and $null -ne $GameInfo.Bounds) {
        $x = [Math]::Max($GameInfo.Bounds.Left + 20, $GameInfo.Bounds.Right - $width - 24)
        $y = $GameInfo.Bounds.Top + 24
        return New-Object System.Drawing.Rectangle($x, $y, $width, $height)
    }

    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $x = $screen.Right - $width - 24
    $y = $screen.Top + 48
    return New-Object System.Drawing.Rectangle($x, $y, $width, $height)
}

function Get-HudBounds {
    param([pscustomobject]$GameInfo)

    $width = 278
    $height = 126

    if ($GameInfo.Running -and -not $GameInfo.IsCompanion -and $null -ne $GameInfo.Bounds) {
        $x = [Math]::Max($GameInfo.Bounds.Left + 20, $GameInfo.Bounds.Right - $width - 24)
        $y = $GameInfo.Bounds.Top + 24
        return New-Object System.Drawing.Rectangle($x, $y, $width, $height)
    }

    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $x = $screen.Right - $width - 24
    $y = $screen.Top + 24
    return New-Object System.Drawing.Rectangle($x, $y, $width, $height)
}

function Set-OverlayMode {
    param([ValidateSet("panel", "hud")] [string]$Mode)

    $script:overlayMode = $Mode
    $script:state.LastMode = $Mode
    Save-OverlayState $script:state

    $isHud = $Mode -eq "hud"
    $hudPanel.Visible = $isHud
    $panelView.Visible = -not $isHud
    $form.Opacity = if ($isHud) { 0.88 } else { 0.98 }

    if ($isHud) {
        Set-FormClickThrough -Form $form -Enabled $true
    }
    else {
        Set-FormClickThrough -Form $form -Enabled $false
    }

    Update-Overlay
}

function Set-OverlayVisible {
    param([bool]$Visible)

    $script:logicalVisible = $Visible
    $script:state.OverlayVisible = $Visible
    Save-OverlayState $script:state

    if ($Visible) {
        $form.Show()
        $form.TopMost = $true
        if ($script:overlayMode -eq "hud") {
            Set-FormClickThrough -Form $form -Enabled $true
        }
        else {
            Set-FormClickThrough -Form $form -Enabled $false
            $form.Activate()
        }
    }
    else {
        $form.Hide()
    }
}

function Toggle-OverlayVisibilityHotkey {
    if (-not $script:logicalVisible) {
        Set-OverlayMode "panel"
        Set-OverlayVisible $true
        return
    }

    if ($script:overlayMode -eq "hud") {
        Set-OverlayMode "panel"
        Set-OverlayVisible $true
        return
    }

    Set-OverlayVisible $false
}

function Update-Overlay {
    $gameInfo = Get-GameInfo
    $config = Get-ConfigSnapshot
    $questSnapshot = Get-QuestSnapshot

    if ($script:overlayMode -eq "hud") {
        $form.Bounds = Get-HudBounds $gameInfo
    }
    else {
        $form.Bounds = Get-PanelBounds $gameInfo
    }

    $statusBadge.Text = if ($gameInfo.Running) { "GAME ONLINE" } else { "GAME OFFLINE" }
    $statusBadge.BackColor = if ($gameInfo.Running) { [System.Drawing.Color]::FromArgb(87, 141, 104) } else { [System.Drawing.Color]::FromArgb(120, 92, 76) }

    $displayValueLabel.Text = $gameInfo.ModeName
    $attachValueLabel.Text = if ($gameInfo.IsCompanion) { "companion" } else { "attached" }
    $warningLabel.Text = $gameInfo.Warning
    $warningLabel.ForeColor = if ($gameInfo.IsCompanion -and $gameInfo.Running) { [System.Drawing.Color]::FromArgb(202, 112, 44) } else { [System.Drawing.Color]::FromArgb(115, 93, 73) }

    $freezeHotkeyValueLabel.Text = $config.FreezeHotkey
    $speedHotkeyValueLabel.Text = $config.SpeedHotkey
    $saveConfigButton.Enabled = $config.Available
    if (-not $script:panelDirty) {
        Load-ConfigIntoControls $config
    }

    $statusValueLabel.Text = $script:lastStatusMessage
    $latestLogValueLabel.Text = Get-LatestOverlayLogLine
    $questSummaryValueLabel.Text = if ($questSnapshot.Available) {
        "Player $($questSnapshot.PlayerName) | $($questSnapshot.WorldDate) | Total $($questSnapshot.Counts.total)"
    }
    else {
        $questSnapshot.Status
    }
    $questListBox.Text = Format-QuestSnapshotText $questSnapshot

    $hudTitleLabel.Text = if ($gameInfo.Running) { "LongYin Quick HUD" } else { "LongYin Companion HUD" }
    $hudLineOne.Text = if ($gameInfo.Running) {
        "Freeze $($config.FreezeHotkey) | Speed $($config.SpeedHotkey)"
    }
    else {
        "Game offline | Press $overlayHotkeyText for panel"
    }
    $hudLineTwo.Text = "Quests $($questSnapshot.Counts.total) | Missions $($questSnapshot.Counts.missions) | Events $($questSnapshot.Counts.worldEvents)"
    $hudHintLabel.Text = if ($gameInfo.IsCompanion -and $gameInfo.Running) {
        "Companion placement only. Open panel for details."
    }
    elseif ($questSnapshot.Available -and @($questSnapshot.Entries).Count -gt 0) {
        (Format-QuestEntryLine (@($questSnapshot.Entries)[0]))
    }
    else {
        "Press $overlayHotkeyText to open the full panel."
    }
}

$script:state = Read-OverlayState
$script:logicalVisible = [bool]$script:state.OverlayVisible
if (-not (Test-Path $overlayStatePath)) {
    Save-OverlayState $script:state
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "LongYin Overlay"
$form.StartPosition = "Manual"
$form.FormBorderStyle = "None"
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(247, 239, 225)
$form.ForeColor = [System.Drawing.Color]::FromArgb(55, 40, 31)

$hudPanel = New-Object System.Windows.Forms.Panel
$hudPanel.Dock = "Fill"
$hudPanel.BackColor = [System.Drawing.Color]::FromArgb(232, 221, 202)
$form.Controls.Add($hudPanel)

$hudTitleLabel = New-Object System.Windows.Forms.Label
$hudTitleLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 12, [System.Drawing.FontStyle]::Bold)
$hudTitleLabel.Location = New-Object System.Drawing.Point(14, 10)
$hudTitleLabel.Size = New-Object System.Drawing.Size(248, 24)
$hudPanel.Controls.Add($hudTitleLabel)

$hudLineOne = New-Object System.Windows.Forms.Label
$hudLineOne.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$hudLineOne.Location = New-Object System.Drawing.Point(16, 42)
$hudLineOne.Size = New-Object System.Drawing.Size(244, 20)
$hudPanel.Controls.Add($hudLineOne)

$hudLineTwo = New-Object System.Windows.Forms.Label
$hudLineTwo.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$hudLineTwo.Location = New-Object System.Drawing.Point(16, 62)
$hudLineTwo.Size = New-Object System.Drawing.Size(244, 18)
$hudPanel.Controls.Add($hudLineTwo)

$hudHintLabel = New-Object System.Windows.Forms.Label
$hudHintLabel.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Italic)
$hudHintLabel.ForeColor = [System.Drawing.Color]::FromArgb(108, 83, 61)
$hudHintLabel.Location = New-Object System.Drawing.Point(16, 82)
$hudHintLabel.Size = New-Object System.Drawing.Size(246, 28)
$hudPanel.Controls.Add($hudHintLabel)

$panelView = New-Object System.Windows.Forms.Panel
$panelView.Dock = "Fill"
$panelView.BackColor = [System.Drawing.Color]::FromArgb(247, 239, 225)
$form.Controls.Add($panelView)

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = "LongYin Overlay"
$titleLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 17, [System.Drawing.FontStyle]::Bold)
$titleLabel.Location = New-Object System.Drawing.Point(16, 14)
$titleLabel.Size = New-Object System.Drawing.Size(200, 36)
$panelView.Controls.Add($titleLabel)

$subtitleLabel = New-Object System.Windows.Forms.Label
$subtitleLabel.Text = "External quick controls only. No game injection, no Unity panel hacks."
$subtitleLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$subtitleLabel.ForeColor = [System.Drawing.Color]::FromArgb(109, 86, 67)
$subtitleLabel.Location = New-Object System.Drawing.Point(18, 48)
$subtitleLabel.Size = New-Object System.Drawing.Size(320, 34)
$panelView.Controls.Add($subtitleLabel)

$statusBadge = New-Object System.Windows.Forms.Label
$statusBadge.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
$statusBadge.ForeColor = [System.Drawing.Color]::White
$statusBadge.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$statusBadge.Location = New-Object System.Drawing.Point(226, 18)
$statusBadge.Size = New-Object System.Drawing.Size(116, 24)
$panelView.Controls.Add($statusBadge)

$hudButton = New-Object System.Windows.Forms.Button
$hudButton.Text = "Switch To HUD"
$hudButton.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$hudButton.Location = New-Object System.Drawing.Point(18, 88)
$hudButton.Size = New-Object System.Drawing.Size(104, 30)
$panelView.Controls.Add($hudButton)

$advancedButton = New-Object System.Windows.Forms.Button
$advancedButton.Text = "Advanced"
$advancedButton.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$advancedButton.Location = New-Object System.Drawing.Point(129, 88)
$advancedButton.Size = New-Object System.Drawing.Size(92, 30)
$panelView.Controls.Add($advancedButton)

$hideButton = New-Object System.Windows.Forms.Button
$hideButton.Text = "Hide"
$hideButton.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$hideButton.Location = New-Object System.Drawing.Point(228, 88)
$hideButton.Size = New-Object System.Drawing.Size(114, 30)
$panelView.Controls.Add($hideButton)

$warningLabel = New-Object System.Windows.Forms.Label
$warningLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$warningLabel.Location = New-Object System.Drawing.Point(18, 126)
$warningLabel.Size = New-Object System.Drawing.Size(324, 34)
$panelView.Controls.Add($warningLabel)

$separator = New-Object System.Windows.Forms.Label
$separator.BorderStyle = "Fixed3D"
$separator.Location = New-Object System.Drawing.Point(18, 163)
$separator.Size = New-Object System.Drawing.Size(324, 2)
$panelView.Controls.Add($separator)

$liveHeader = New-Object System.Windows.Forms.Label
$liveHeader.Text = "LIVE ACTIONS"
$liveHeader.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$liveHeader.ForeColor = [System.Drawing.Color]::FromArgb(126, 55, 39)
$liveHeader.Location = New-Object System.Drawing.Point(18, 172)
$liveHeader.Size = New-Object System.Drawing.Size(120, 20)
$panelView.Controls.Add($liveHeader)

$freezeNowButton = New-Object System.Windows.Forms.Button
$freezeNowButton.Text = "Toggle Freeze Now"
$freezeNowButton.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$freezeNowButton.Location = New-Object System.Drawing.Point(18, 198)
$freezeNowButton.Size = New-Object System.Drawing.Size(154, 30)
$panelView.Controls.Add($freezeNowButton)

$speedNowButton = New-Object System.Windows.Forms.Button
$speedNowButton.Text = "Cycle Speed Now"
$speedNowButton.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$speedNowButton.Location = New-Object System.Drawing.Point(188, 198)
$speedNowButton.Size = New-Object System.Drawing.Size(154, 30)
$panelView.Controls.Add($speedNowButton)

$freezeHotkeyLabel = New-Object System.Windows.Forms.Label
$freezeHotkeyLabel.Text = "Freeze hotkey"
$freezeHotkeyLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$freezeHotkeyLabel.Location = New-Object System.Drawing.Point(18, 236)
$freezeHotkeyLabel.Size = New-Object System.Drawing.Size(100, 20)
$panelView.Controls.Add($freezeHotkeyLabel)

$freezeHotkeyValueLabel = New-Object System.Windows.Forms.Label
$freezeHotkeyValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$freezeHotkeyValueLabel.Location = New-Object System.Drawing.Point(128, 236)
$freezeHotkeyValueLabel.Size = New-Object System.Drawing.Size(60, 20)
$panelView.Controls.Add($freezeHotkeyValueLabel)

$speedHotkeyLabel = New-Object System.Windows.Forms.Label
$speedHotkeyLabel.Text = "Speed hotkey"
$speedHotkeyLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$speedHotkeyLabel.Location = New-Object System.Drawing.Point(188, 236)
$speedHotkeyLabel.Size = New-Object System.Drawing.Size(92, 20)
$panelView.Controls.Add($speedHotkeyLabel)

$speedHotkeyValueLabel = New-Object System.Windows.Forms.Label
$speedHotkeyValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$speedHotkeyValueLabel.Location = New-Object System.Drawing.Point(286, 236)
$speedHotkeyValueLabel.Size = New-Object System.Drawing.Size(56, 20)
$panelView.Controls.Add($speedHotkeyValueLabel)

$launchHeader = New-Object System.Windows.Forms.Label
$launchHeader.Text = "NEXT-LAUNCH SETTINGS"
$launchHeader.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$launchHeader.ForeColor = [System.Drawing.Color]::FromArgb(126, 55, 39)
$launchHeader.Location = New-Object System.Drawing.Point(18, 272)
$launchHeader.Size = New-Object System.Drawing.Size(190, 20)
$panelView.Controls.Add($launchHeader)

$lockStaminaCheckbox = New-Object System.Windows.Forms.CheckBox
$lockStaminaCheckbox.Text = "Lock exploration stamina"
$lockStaminaCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$lockStaminaCheckbox.Location = New-Object System.Drawing.Point(18, 300)
$lockStaminaCheckbox.Size = New-Object System.Drawing.Size(185, 24)
$panelView.Controls.Add($lockStaminaCheckbox)

$traceModeCheckbox = New-Object System.Windows.Forms.CheckBox
$traceModeCheckbox.Text = "Trace mode"
$traceModeCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$traceModeCheckbox.Location = New-Object System.Drawing.Point(210, 300)
$traceModeCheckbox.Size = New-Object System.Drawing.Size(132, 24)
$panelView.Controls.Add($traceModeCheckbox)

$freezeDateCheckbox = New-Object System.Windows.Forms.CheckBox
$freezeDateCheckbox.Text = "Start with freeze date"
$freezeDateCheckbox.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$freezeDateCheckbox.Location = New-Object System.Drawing.Point(18, 329)
$freezeDateCheckbox.Size = New-Object System.Drawing.Size(185, 24)
$panelView.Controls.Add($freezeDateCheckbox)

$expMultiplierLabel = New-Object System.Windows.Forms.Label
$expMultiplierLabel.Text = "Book EXP multiplier"
$expMultiplierLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$expMultiplierLabel.Location = New-Object System.Drawing.Point(18, 362)
$expMultiplierLabel.Size = New-Object System.Drawing.Size(122, 20)
$panelView.Controls.Add($expMultiplierLabel)

$expMultiplierBox = New-Object System.Windows.Forms.NumericUpDown
$expMultiplierBox.Minimum = 1
$expMultiplierBox.Maximum = 999
$expMultiplierBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$expMultiplierBox.Location = New-Object System.Drawing.Point(18, 386)
$expMultiplierBox.Size = New-Object System.Drawing.Size(110, 25)
$panelView.Controls.Add($expMultiplierBox)

$pointMultiplierLabel = New-Object System.Windows.Forms.Label
$pointMultiplierLabel.Text = "Creation points"
$pointMultiplierLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$pointMultiplierLabel.Location = New-Object System.Drawing.Point(188, 362)
$pointMultiplierLabel.Size = New-Object System.Drawing.Size(112, 20)
$panelView.Controls.Add($pointMultiplierLabel)

$pointMultiplierBox = New-Object System.Windows.Forms.NumericUpDown
$pointMultiplierBox.Minimum = 1
$pointMultiplierBox.Maximum = 999
$pointMultiplierBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$pointMultiplierBox.Location = New-Object System.Drawing.Point(188, 386)
$pointMultiplierBox.Size = New-Object System.Drawing.Size(110, 25)
$panelView.Controls.Add($pointMultiplierBox)

$saveConfigButton = New-Object System.Windows.Forms.Button
$saveConfigButton.Text = "Save Next-Launch Settings"
$saveConfigButton.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$saveConfigButton.Location = New-Object System.Drawing.Point(18, 420)
$saveConfigButton.Size = New-Object System.Drawing.Size(324, 30)
$panelView.Controls.Add($saveConfigButton)

$statusHeader = New-Object System.Windows.Forms.Label
$statusHeader.Text = "STATUS + QUESTS"
$statusHeader.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$statusHeader.ForeColor = [System.Drawing.Color]::FromArgb(126, 55, 39)
$statusHeader.Location = New-Object System.Drawing.Point(18, 458)
$statusHeader.Size = New-Object System.Drawing.Size(120, 20)
$panelView.Controls.Add($statusHeader)

$displayLabel = New-Object System.Windows.Forms.Label
$displayLabel.Text = "Display mode"
$displayLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$displayLabel.Location = New-Object System.Drawing.Point(18, 485)
$displayLabel.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($displayLabel)

$displayValueLabel = New-Object System.Windows.Forms.Label
$displayValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$displayValueLabel.Location = New-Object System.Drawing.Point(112, 485)
$displayValueLabel.Size = New-Object System.Drawing.Size(230, 20)
$panelView.Controls.Add($displayValueLabel)

$attachLabel = New-Object System.Windows.Forms.Label
$attachLabel.Text = "Overlay mode"
$attachLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$attachLabel.Location = New-Object System.Drawing.Point(18, 506)
$attachLabel.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($attachLabel)

$attachValueLabel = New-Object System.Windows.Forms.Label
$attachValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$attachValueLabel.Location = New-Object System.Drawing.Point(112, 506)
$attachValueLabel.Size = New-Object System.Drawing.Size(230, 20)
$panelView.Controls.Add($attachValueLabel)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = "Last action"
$statusLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$statusLabel.Location = New-Object System.Drawing.Point(18, 527)
$statusLabel.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($statusLabel)

$statusValueLabel = New-Object System.Windows.Forms.Label
$statusValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$statusValueLabel.Location = New-Object System.Drawing.Point(112, 527)
$statusValueLabel.Size = New-Object System.Drawing.Size(230, 32)
$panelView.Controls.Add($statusValueLabel)

$latestLogLabel = New-Object System.Windows.Forms.Label
$latestLogLabel.Text = "Latest mod log"
$latestLogLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$latestLogLabel.Location = New-Object System.Drawing.Point(18, 559)
$latestLogLabel.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($latestLogLabel)

$latestLogValueLabel = New-Object System.Windows.Forms.Label
$latestLogValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 8.75)
$latestLogValueLabel.Location = New-Object System.Drawing.Point(112, 559)
$latestLogValueLabel.Size = New-Object System.Drawing.Size(230, 38)
$panelView.Controls.Add($latestLogValueLabel)

$questSummaryLabel = New-Object System.Windows.Forms.Label
$questSummaryLabel.Text = "Quest scan"
$questSummaryLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$questSummaryLabel.Location = New-Object System.Drawing.Point(18, 603)
$questSummaryLabel.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($questSummaryLabel)

$questSummaryValueLabel = New-Object System.Windows.Forms.Label
$questSummaryValueLabel.Font = New-Object System.Drawing.Font("Segoe UI", 8.75, [System.Drawing.FontStyle]::Bold)
$questSummaryValueLabel.Location = New-Object System.Drawing.Point(112, 603)
$questSummaryValueLabel.Size = New-Object System.Drawing.Size(230, 34)
$panelView.Controls.Add($questSummaryValueLabel)

$questListBox = New-Object System.Windows.Forms.TextBox
$questListBox.Multiline = $true
$questListBox.ReadOnly = $true
$questListBox.ScrollBars = "Vertical"
$questListBox.BackColor = [System.Drawing.Color]::FromArgb(252, 247, 239)
$questListBox.Font = New-Object System.Drawing.Font("Consolas", 8.6)
$questListBox.Location = New-Object System.Drawing.Point(18, 640)
$questListBox.Size = New-Object System.Drawing.Size(324, 130)
$panelView.Controls.Add($questListBox)

$notesHeader = New-Object System.Windows.Forms.Label
$notesHeader.Text = "Quick notes"
$notesHeader.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$notesHeader.Location = New-Object System.Drawing.Point(18, 778)
$notesHeader.Size = New-Object System.Drawing.Size(90, 20)
$panelView.Controls.Add($notesHeader)

$notesBox = New-Object System.Windows.Forms.TextBox
$notesBox.Multiline = $true
$notesBox.AcceptsReturn = $true
$notesBox.ScrollBars = "Vertical"
$notesBox.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$notesBox.Location = New-Object System.Drawing.Point(18, 802)
$notesBox.Size = New-Object System.Drawing.Size(324, 78)
$panelView.Controls.Add($notesBox)

$hotkeyLabel = New-Object System.Windows.Forms.Label
$hotkeyLabel.Text = "Overlay hotkey: $overlayHotkeyText"
$hotkeyLabel.Font = New-Object System.Drawing.Font("Segoe UI", 8.75, [System.Drawing.FontStyle]::Italic)
$hotkeyLabel.ForeColor = [System.Drawing.Color]::FromArgb(109, 86, 67)
$hotkeyLabel.Location = New-Object System.Drawing.Point(18, 888)
$hotkeyLabel.Size = New-Object System.Drawing.Size(220, 18)
$panelView.Controls.Add($hotkeyLabel)

$markPanelDirty = {
    if (-not $script:suppressDirtyEvents) {
        $script:panelDirty = $true
    }
}

$lockStaminaCheckbox.Add_CheckedChanged($markPanelDirty)
$traceModeCheckbox.Add_CheckedChanged($markPanelDirty)
$freezeDateCheckbox.Add_CheckedChanged($markPanelDirty)
$expMultiplierBox.Add_ValueChanged($markPanelDirty)
$pointMultiplierBox.Add_ValueChanged($markPanelDirty)

$freezeNowButton.Add_Click({
    $config = Get-ConfigSnapshot
    Invoke-GameHotkey -HotkeyName $config.FreezeHotkey -ActionLabel "Freeze toggle"
})

$speedNowButton.Add_Click({
    $config = Get-ConfigSnapshot
    Invoke-GameHotkey -HotkeyName $config.SpeedHotkey -ActionLabel "Speed cycle"
})

$saveConfigButton.Add_Click({
    try {
        Save-ConfigSnapshot `
            -LockStamina $lockStaminaCheckbox.Checked `
            -ExpMultiplier ([int]$expMultiplierBox.Value) `
            -PointMultiplier ([int]$pointMultiplierBox.Value) `
            -TraceMode $traceModeCheckbox.Checked `
            -FreezeDate $freezeDateCheckbox.Checked

        Set-StatusMessage "Saved next-launch settings."
        $script:panelDirty = $false
        Update-Overlay
    }
    catch {
        Set-StatusMessage "Save failed: $($_.Exception.Message)"
    }
})

$advancedButton.Add_Click({
    Start-Process -FilePath "powershell" -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        (Join-Path $PSScriptRoot "LongYinModControl.ps1")
    ) | Out-Null

    Set-StatusMessage "Opened advanced control window."
})

$hideButton.Add_Click({
    $script:state.NotesText = $notesBox.Text
    Save-OverlayState $script:state
    Set-OverlayVisible $false
})

$hudButton.Add_Click({
    $script:state.NotesText = $notesBox.Text
    Save-OverlayState $script:state
    Set-OverlayMode "hud"
})

$form.Add_FormClosing({
    param($sender, $eventArgs)
    $eventArgs.Cancel = $true
    $script:state.NotesText = $notesBox.Text
    Save-OverlayState $script:state
    Set-OverlayVisible $false
})

$refreshTimer = New-Object System.Windows.Forms.Timer
$refreshTimer.Interval = 250
$refreshTimer.Add_Tick({
    if (Test-OverlayHotkeyPressed) {
        Toggle-OverlayVisibilityHotkey
    }

    if ($script:logicalVisible) {
        Update-Overlay
    }
})

$notesBox.Text = [string]$script:state.NotesText

$form.Add_Shown({
    if ($script:state.LastMode -eq "hud") {
        Set-OverlayMode "hud"
    }
    else {
        Set-OverlayMode "panel"
    }

    Update-Overlay
    if (-not $script:logicalVisible) {
        $form.Hide()
    }
    $refreshTimer.Start()
})

[void]$form.ShowDialog()
