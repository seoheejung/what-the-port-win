param([switch]$Push)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Existing staged changes detected. Review them before using this feature commit script.' }
    function Commit-Feature([string]$message, [string[]]$paths) {
        & git add -- $paths
        if ($LASTEXITCODE -ne 0) { throw "Could not stage: $message" }
        & git diff --cached --check
        if ($LASTEXITCODE -ne 0) { throw "Whitespace errors in: $message" }
        & git diff --cached --quiet
        if ($LASTEXITCODE -eq 0) { Write-Output "Already committed: $message"; return }
        & git commit -m $message
        if ($LASTEXITCODE -ne 0) { throw "Could not commit: $message" }
    }
    Commit-Feature 'docs: link README to the published GitHub Pages site' @('README.md')
    Commit-Feature 'fix(app): reveal the existing panel on executable relaunch' @('src/App/InstanceActivation.cs','src/App/Program.cs','src/App/Panel.cs','tests/UiTests.cs','scripts/test.ps1','scripts/test-startup.ps1','docs/usage.md','docs/guide.html','docs/index.html','docs/downloads','docs/accessibility-audit.md','.project/verification.md','.project/commit-plan.md','scripts/commit-features.ps1')
    if ($Push) {
        $branch = (& git branch --show-current).Trim()
        if (-not $branch) { throw 'Cannot push a detached HEAD.' }
        & git push -u origin $branch
        if ($LASTEXITCODE -ne 0) { throw 'Push failed. Local commits are preserved; retry git push after checking network and authentication.' }
    }
} finally { Pop-Location }
