[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $OutputDirectory,
    [string] $PackagePath
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "dotnet-test-app.csproj"
$artifactsDirectory = Join-Path $repoRoot "artifacts"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactsDirectory "publish"
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $artifactsDirectory "dotnet-test-app.zip"
}

$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$PackagePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PackagePath)

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}

$packageDirectory = Split-Path -Parent $PackagePath
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

if (Test-Path $PackagePath) {
    Remove-Item $PackagePath -Force
}

dotnet publish $projectPath --configuration $Configuration --output $OutputDirectory

Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $PackagePath -Force

Write-Host "Created App Service ZIP package:"
Write-Host $PackagePath
