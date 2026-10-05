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
    Commit-Feature 'feat(core): add Windows listener scanner, process policies and CLI' @('.gitignore','AGENTS.md','LICENSE','src/Core','src/Cli','tests/CoreTests.cs')
    Commit-Feature 'feat(app): add accessible WPF tray UI and saved multi-monitor placement' @('src/App','src/App.config','src/app.manifest','assets/fonts','tests/UiTests.cs','scripts/build.ps1','scripts/test.ps1','scripts/install.ps1','scripts/package.ps1','launch.cmd')
    Commit-Feature 'docs: add Korean product guide and generated screenshots' @('README.md','DESIGN.md','docs/usage.md','docs/checklist.md','docs/images','.project/plan.md','.project/verification.md','.project/readme-image.md')
    Commit-Feature 'feat(site): add responsive Windows download and product pages' @('site','scripts/serve-site.mjs','scripts/prepare-site.ps1','scripts/check-site.mjs')
    Commit-Feature 'test(audit): document security findings and accessibility checks' @('docs/security-audit.md','docs/accessibility-audit.md','tests/SecurityAudit.cs','tests/TerminalAuditFixture.cs','scripts/security-audit.ps1','scripts/commit-features.ps1','.project/commit-plan.md')
    if ($Push) {
        $branch = (& git branch --show-current).Trim()
        if (-not $branch) { throw 'Cannot push a detached HEAD.' }
        & git push -u origin $branch
        if ($LASTEXITCODE -ne 0) { throw 'Push failed. Local commits are preserved; retry git push after checking network and authentication.' }
    }
} finally { Pop-Location }
