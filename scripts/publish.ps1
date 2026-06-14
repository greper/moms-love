param(
    [ValidateSet("major", "minor", "build")]
    [string]$Bump = "build"
)

$csproj = "$PSScriptRoot\..\MomsLove\MomsLove.csproj"
[xml]$xml = Get-Content $csproj

$versionNode = $xml.SelectSingleNode("//PropertyGroup/Version")
if ($null -eq $versionNode) {
    $versionNode = $xml.SelectSingleNode("//Version")
}
if ($null -eq $versionNode) {
    Write-Host "ERROR: Version element not found in csproj" -ForegroundColor Red
    exit 1
}

$current = [Version]$versionNode.InnerText
switch ($Bump) {
    "major" { $new = [Version]::new($current.Major + 1, 0, 0) }
    "minor" { $new = [Version]::new($current.Major, $current.Minor + 1, 0) }
    "build" { $new = [Version]::new($current.Major, $current.Minor, $current.Build + 1) }
}

Write-Host "Version: $current -> $new" -ForegroundColor Cyan
$versionNode.InnerText = $new.ToString()
$xml.Save($csproj)

Write-Host "Publishing to publish\MomsLove\ ..." -ForegroundColor Cyan
Push-Location "$PSScriptRoot\.."
try {
    dotnet publish .\MomsLove\MomsLove.csproj -c Release --self-contained false -o ".\publish\MomsLove"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    Write-Host "Done. Output: publish\MomsLove\" -ForegroundColor Green
} finally {
    Pop-Location
}
