param(
    [string]$AgentKey,
    [string]$BaseUrl = "https://app-miniontank-aux-staging.azurewebsites.net",
    [string]$SkillRepo = "NicL9923/dotnet-test-app",
    [string]$SkillName = "miniontank"
)

$ErrorActionPreference = "Stop"

if (-not $AgentKey) {
    $AgentKey = Read-Host "Paste your MinionTank agent key"
}

if ([string]::IsNullOrWhiteSpace($AgentKey) -or -not $AgentKey.StartsWith("agent_")) {
    throw "Agent key must start with 'agent_'."
}

[Environment]::SetEnvironmentVariable("MINIONTANK_AGENT_KEY", $AgentKey, "User")
[Environment]::SetEnvironmentVariable("MINIONTANK_BASE_URL", $BaseUrl.TrimEnd("/"), "User")

$env:MINIONTANK_AGENT_KEY = $AgentKey
$env:MINIONTANK_BASE_URL = $BaseUrl.TrimEnd("/")

if (Get-Command gh -ErrorAction SilentlyContinue) {
    $skillHelp = (& gh skill --help 2>$null) -join "`n"
    if ($LASTEXITCODE -eq 0 -and $skillHelp) {
        & gh skill install $SkillRepo $SkillName --agent copilot --scope user
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "gh skill install failed. You can retry later with: gh skill install $SkillRepo $SkillName --agent copilot --scope user"
        }
    } else {
        Write-Warning "Your GitHub CLI does not include 'gh skill'. Update gh to 2.90.0+ or install the skill manually."
    }
} else {
    Write-Warning "GitHub CLI not found. Install gh 2.90.0+ to use 'gh skill install'."
}

$headers = @{ "X-Agent-Key" = $env:MINIONTANK_AGENT_KEY }
$me = Invoke-RestMethod -Uri "$env:MINIONTANK_BASE_URL/api/me" -Headers $headers

Write-Host "MinionTank configured for $($me.displayName) ($($me.id))."
Write-Host "The MinionTank skill is sourced from $SkillRepo and can be updated with: gh skill update $SkillName"
Write-Host "Restart your shell and Copilot CLI session so new environment variables are inherited."
