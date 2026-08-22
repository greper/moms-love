[CmdletBinding()]
param(
    [ValidateSet("major", "minor", "build")]
    [string]$Bump = "build",
    [string]$Remote = "origin",
    [string]$Branch = "main",
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$csproj = Join-Path $repoRoot "MomsLove\MomsLove.csproj"
$publishDir = Join-Path $repoRoot "publish\MomsLove"
$releaseDir = Join-Path $repoRoot "publish"

function Invoke-Native {
    param([string]$File, [string[]]$Arguments)
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet CLI was not found" }
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "git CLI was not found" }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "GitHub CLI (gh) was not found. Install it and run gh auth login first." }

    $status = git status --porcelain
    $managedPaths = @("MomsLove/MomsLove.csproj", "scripts/release.ps1")
    $unexpected = @($status | Where-Object {
        $line = $_.Trim()
        $line -and (($managedPaths -notcontains $line.Substring(3)))
    })
    if ($unexpected.Count -gt 0) {
        Write-Warning "工作区存在其他改动，脚本只会提交版本文件和 release 脚本："
        $unexpected | ForEach-Object { Write-Warning $_ }
    }

    [xml]$xml = Get-Content -Raw -Encoding UTF8 $csproj
    $versionNode = $xml.SelectSingleNode("//PropertyGroup/Version")
    if ($null -eq $versionNode) { throw "Version element not found in $csproj" }
    $current = [Version]$versionNode.InnerText
    switch ($Bump) {
        "major" { $new = [Version]::new($current.Major + 1, 0, 0) }
        "minor" { $new = [Version]::new($current.Major, $current.Minor + 1, 0) }
        "build" { $new = [Version]::new($current.Major, $current.Minor, $current.Build + 1) }
    }
    $tag = "v$new"
    if (git tag --list $tag) { throw "Tag $tag already exists" }
    Write-Host "Version: $current -> $new" -ForegroundColor Cyan
    $versionNode.InnerText = $new.ToString()
    $xml.Save($csproj)

    Write-Host "Running tests ..." -ForegroundColor Cyan
    Invoke-Native "dotnet" @("test", ".\MomsLoveApp.sln", "--no-restore")

    if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
    Write-Host "Publishing ..." -ForegroundColor Cyan
    Invoke-Native "dotnet" @("publish", ".\MomsLove\MomsLove.csproj", "-c", "Release", "--self-contained", "false", "-o", ".\publish\MomsLove")

    $archive = Join-Path $releaseDir "MomsLove-$tag-win-x64.zip"
    if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $archive

    git add -- MomsLove/MomsLove.csproj scripts/release.ps1
    $staged = git diff --cached --name-only
    if (-not ($staged -contains "MomsLove/MomsLove.csproj")) { throw "Version file was not staged" }
    Invoke-Native "git" @("commit", "-m", "chore: release $tag")
    Invoke-Native "git" @("tag", "-a", $tag, "-m", "Release $tag")
    Invoke-Native "git" @("push", $Remote, $Branch)
    Invoke-Native "git" @("push", $Remote, $tag)

    $releaseArgs = @("release", "create", $tag, $archive, "--title", "MomsLove $tag", "--generate-notes")
    if ($Draft) { $releaseArgs += "--draft" }
    if ($Prerelease) { $releaseArgs += "--prerelease" }
    Write-Host "Creating GitHub Release ..." -ForegroundColor Cyan
    Invoke-Native "gh" $releaseArgs
    Write-Host "Release $tag published successfully." -ForegroundColor Green
} finally {
    Pop-Location
}
