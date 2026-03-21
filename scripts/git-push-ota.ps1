[CmdletBinding()]
param(
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$ReleaseNotesPath,
  [switch]$SkipBuild,
  [switch]$SkipPublish,
  [switch]$SkipPush,
  [switch]$DryRun,
  [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) {
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Git {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments
  )

  $output = & git -C $RepoRoot @Arguments 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "git $($Arguments -join ' ') 失败：`n$output"
  }

  return ($output -join "`n").Trim()
}

function Invoke-GitAllowFailure {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments
  )

  $output = & git -C $RepoRoot @Arguments 2>&1
  return [pscustomobject]@{
    ExitCode = $LASTEXITCODE
    Output = ($output -join "`n").Trim()
  }
}

function Invoke-Npm {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Arguments
  )

  Push-Location $ElectronRoot
  try {
    & npm @Arguments
    if ($LASTEXITCODE -ne 0) {
      throw "npm $($Arguments -join ' ') 失败。"
    }
  }
  finally {
    Pop-Location
  }
}

function Get-JsonFile([string]$Path) {
  return Get-Content -Path $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Assert-BuildPrereqs {
  $nodeModules = Join-Path $ElectronRoot 'node_modules'
  if (-not (Test-Path $nodeModules)) {
    throw "未找到 $nodeModules 。请先在 electron-app 目录执行 npm install，再执行 git push ota。"
  }
}

function Get-GitHubToken {
  if ($env:GITHUB_TOKEN) {
    return $env:GITHUB_TOKEN
  }

  if ($env:GH_TOKEN) {
    return $env:GH_TOKEN
  }

  $credentialInput = "protocol=https`nhost=github.com`n`n"
  $credentialResponse = $credentialInput | git credential fill 2>$null

  if (-not $credentialResponse) {
    throw '未找到 GitHub 凭据。请先设置 GITHUB_TOKEN，或确保 git credential manager 已登录 github.com。'
  }

  $passwordLine = $credentialResponse | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
  if (-not $passwordLine) {
    throw 'git credential fill 未返回 GitHub password/token。'
  }

  return ($passwordLine -replace '^password=', '').Trim()
}

function Parse-OriginRepo {
  $origin = Invoke-Git @('remote', 'get-url', 'origin')

  if ($origin -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+?)(?:\.git)?$') {
    return [pscustomobject]@{
      Owner = $Matches.owner
      Repo = $Matches.repo
      Origin = $origin
    }
  }

  throw "无法从 origin 解析 GitHub 仓库：$origin"
}

function New-ReleaseNotes {
  param(
    [string]$Version,
    [string]$Branch,
    [string]$PreviousTag
  )

  if ($ReleaseNotesPath) {
    if (-not (Test-Path $ReleaseNotesPath)) {
      throw "未找到 Release notes 文件：$ReleaseNotesPath"
    }

    return (Get-Content -Path $ReleaseNotesPath -Raw -Encoding UTF8).Trim()
  }

  $range = if ($PreviousTag) { "$PreviousTag..HEAD" } else { 'HEAD' }
  $commitLines = Invoke-GitAllowFailure @('log', '--pretty=format:%s', $range)
  $subjects = @()

  if ($commitLines.ExitCode -eq 0 -and $commitLines.Output) {
    $subjects = @($commitLines.Output -split "`r?`n" | Where-Object { $_.Trim() })
  }

  if ($subjects.Count -eq 0) {
    $subjects = @('本次版本没有检测到新的提交说明，请按需补充发布说明。')
  }

  $bulletLines = $subjects | ForEach-Object { "- $($_.Trim())" }

  return @(
    '## 本次更新'
    $bulletLines
    ''
    '## 发布信息'
    "- 版本：v$Version"
    "- 分支：$Branch"
    if ($PreviousTag) { "- 变更范围：$PreviousTag..HEAD" } else { '- 变更范围：仓库初始发布范围' }
  ) -join "`n"
}

function Invoke-GitHubApi {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('GET', 'POST', 'PATCH', 'DELETE')]
    [string]$Method,
    [Parameter(Mandatory = $true)]
    [string]$Url,
    $Body
  )

  $headers = @{
    Accept                 = 'application/vnd.github+json'
    Authorization          = "Bearer $GitHubToken"
    'User-Agent'           = 'longyin-pro-max-ota-script'
    'X-GitHub-Api-Version' = '2022-11-28'
  }

  $invokeParams = @{
    Method      = $Method
    Uri         = $Url
    Headers     = $headers
    ErrorAction = 'Stop'
  }

  if ($null -ne $Body) {
    $invokeParams.ContentType = 'application/json; charset=utf-8'
    $invokeParams.Body = ($Body | ConvertTo-Json -Depth 8)
  }

  return Invoke-RestMethod @invokeParams
}

function Invoke-GitHubUpload {
  param(
    [Parameter(Mandatory = $true)]
    [string]$UploadUrl,
    [Parameter(Mandatory = $true)]
    [string]$FilePath,
    [Parameter(Mandatory = $true)]
    [string]$AssetName
  )

  $headers = @{
    Accept                 = 'application/vnd.github+json'
    Authorization          = "Bearer $GitHubToken"
    'User-Agent'           = 'longyin-pro-max-ota-script'
    'X-GitHub-Api-Version' = '2022-11-28'
  }

  $targetUrl = "$UploadUrl?name=$([uri]::EscapeDataString($AssetName))"
  Invoke-RestMethod -Method Post -Uri $targetUrl -Headers $headers -InFile $FilePath -ContentType 'application/octet-stream' -ErrorAction Stop | Out-Null
}

$RepoRoot = (Resolve-Path $RepoRoot).Path
$ElectronRoot = Join-Path $RepoRoot 'electron-app'
$ReleaseRoot = Join-Path $ElectronRoot 'release'

if (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
  throw "RepoRoot 不是 Git 仓库：$RepoRoot"
}

if (-not (Test-Path $ElectronRoot)) {
  throw "未找到 electron-app 目录：$ElectronRoot"
}

$repoInfo = Parse-OriginRepo
$packageJsonPath = Join-Path $ElectronRoot 'package.json'
$packageJson = Get-JsonFile $packageJsonPath
$version = [string]$packageJson.version
$tagName = "v$version"
$releaseName = "龙胤立志传 Pro Max $tagName"
$zipName = "LongYinProMaxApp-$version-win-x64.zip"
$zipPath = Join-Path $ReleaseRoot $zipName
$manifestPath = Join-Path $ReleaseRoot 'update-manifest.json'
$branch = Invoke-Git @('branch', '--show-current')
$statusPorcelain = Invoke-Git @('status', '--porcelain')
$tagList = Invoke-GitAllowFailure @('tag', '--sort=-creatordate')
$allTags = if ($tagList.Output) { $tagList.Output -split "`r?`n" | Where-Object { $_.Trim() } } else { @() }
$previousTag = $allTags | Where-Object { $_ -ne $tagName } | Select-Object -First 1
$releaseNotes = New-ReleaseNotes -Version $version -Branch $branch -PreviousTag $previousTag

Write-Step "仓库: $RepoRoot"
Write-Step "版本: $version"
Write-Step "分支: $branch"
Write-Step "origin: $($repoInfo.Origin)"
if ($previousTag) {
  Write-Step "上一个发布 tag: $previousTag"
}
else {
  Write-Step '未找到更早的发布 tag，将按首次发布处理。'
}

$statusSummary = if ($statusPorcelain) { $statusPorcelain } else { '工作树干净。' }
Write-Host $statusSummary

if ($statusPorcelain -and -not $AllowDirty) {
  throw "工作树不是干净状态。请先提交或清理改动，再执行 git push ota。"
}

if (-not $SkipBuild) {
  Assert-BuildPrereqs
  Write-Step '执行 npm run typecheck'
  Invoke-Npm @('run', 'typecheck')
  Write-Step '执行 npm run build'
  Invoke-Npm @('run', 'build')
}
else {
  Write-Step '已跳过构建步骤。'
}

if (-not (Test-Path $zipPath)) {
  throw "未找到 OTA ZIP：$zipPath"
}

if (-not (Test-Path $manifestPath)) {
  throw "未找到 OTA manifest：$manifestPath"
}

$manifest = Get-JsonFile $manifestPath
if ([string]$manifest.version -ne $version) {
  throw "manifest.version 与 package.json.version 不一致：$($manifest.version) vs $version"
}

if ([string]$manifest.zipAsset -ne $zipName) {
  throw "manifest.zipAsset 与预期 ZIP 名称不一致：$($manifest.zipAsset) vs $zipName"
}

$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ([string]$manifest.sha256 -ne $zipHash) {
  throw "manifest.sha256 与 ZIP 实际 SHA256 不一致。"
}

Write-Step "已校验 OTA 资产: $zipName + update-manifest.json"

$releaseNotesPreview = $releaseNotes -split "`r?`n" | Select-Object -First 12
Write-Step '发布说明预览'
$releaseNotesPreview | ForEach-Object { Write-Host $_ }

if ($DryRun) {
  Write-Step 'DryRun 模式：到此为止，不推送代码、不发布 Release。'
  return
}

if ($SkipPush) {
  Write-Step '已跳过 git push。'
}
else {
  Write-Step "推送分支 origin/$branch"
  & git -C $RepoRoot push origin $branch
  if ($LASTEXITCODE -ne 0) {
    throw 'git push origin 分支失败。'
  }

  $tagExists = [bool](Invoke-GitAllowFailure @('rev-parse', '-q', '--verify', "refs/tags/$tagName")).Output
  if (-not $tagExists) {
    Write-Step "创建 tag $tagName"
    & git -C $RepoRoot tag -a $tagName -m $releaseName
    if ($LASTEXITCODE -ne 0) {
      throw "创建 tag $tagName 失败。"
    }
  }
  else {
    $tagCommit = Invoke-Git @('rev-list', '-n', '1', $tagName)
    $headCommit = Invoke-Git @('rev-parse', 'HEAD')
    if ($tagCommit -ne $headCommit) {
      throw "tag $tagName 已存在，但不在当前 HEAD 上。请先处理 tag，再执行发布。"
    }
  }

  Write-Step "推送 tag $tagName"
  & git -C $RepoRoot push origin $tagName
  if ($LASTEXITCODE -ne 0) {
    throw "git push origin $tagName 失败。"
  }
}

if ($SkipPublish) {
  Write-Step '已跳过 GitHub Release 发布。'
  return
}

$GitHubToken = Get-GitHubToken
$apiRoot = "https://api.github.com/repos/$($repoInfo.Owner)/$($repoInfo.Repo)"
$releaseLookup = $null

try {
  $releaseLookup = Invoke-GitHubApi -Method GET -Url "$apiRoot/releases/tags/$tagName"
}
catch {
  $response = $_.Exception.Response
  $statusCode = if ($response) { [int]$response.StatusCode } else { 0 }
  if ($statusCode -ne 404) {
    throw
  }
}

$releaseBodyPayload = @{
  tag_name         = $tagName
  target_commitish = $branch
  name             = $releaseName
  body             = $releaseNotes
  draft            = $false
  prerelease       = $false
}

if ($releaseLookup) {
  Write-Step "更新现有 GitHub Release: $tagName"
  $release = Invoke-GitHubApi -Method PATCH -Url "$apiRoot/releases/$($releaseLookup.id)" -Body $releaseBodyPayload
}
else {
  Write-Step "创建新的 GitHub Release: $tagName"
  $release = Invoke-GitHubApi -Method POST -Url "$apiRoot/releases" -Body $releaseBodyPayload
}

$existingAssets = @($release.assets)
$assetNames = @($zipName, 'update-manifest.json')
foreach ($assetName in $assetNames) {
  foreach ($asset in @($existingAssets | Where-Object { $_.name -eq $assetName })) {
    Write-Step "删除旧资产: $assetName"
    Invoke-GitHubApi -Method DELETE -Url "$apiRoot/releases/assets/$($asset.id)" | Out-Null
  }
}

$cleanUploadUrl = [string]$release.upload_url -replace '\{\?name,label\}$', ''
Write-Step "上传资产: $zipName"
Invoke-GitHubUpload -UploadUrl $cleanUploadUrl -FilePath $zipPath -AssetName $zipName
Write-Step '上传资产: update-manifest.json'
Invoke-GitHubUpload -UploadUrl $cleanUploadUrl -FilePath $manifestPath -AssetName 'update-manifest.json'

$finalRelease = Invoke-GitHubApi -Method GET -Url "$apiRoot/releases/tags/$tagName"
$finalAssets = @($finalRelease.assets | ForEach-Object { $_.name })

if ($finalAssets -notcontains $zipName -or $finalAssets -notcontains 'update-manifest.json') {
  throw 'GitHub Release 已创建，但 OTA 资产不完整。'
}

Write-Step 'OTA 发布完成。'
Write-Host "Release: $($finalRelease.html_url)" -ForegroundColor Green
Write-Host "Assets: $($finalAssets -join ', ')" -ForegroundColor Green
