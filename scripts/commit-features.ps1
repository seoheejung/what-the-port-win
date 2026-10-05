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
    Commit-Feature 'fix(core): protect work sessions and preserve partial stop outcomes' @('src/Core','tests/CoreTests.cs')
    Commit-Feature 'feat(app): add Korean and English UI with aligned server rows' @('src/App','tests/UiTests.cs','README.md','docs/usage.md','docs/images')
    Commit-Feature 'feat(site): move Windows downloads to docs for GitHub Pages' @('.gitignore','site','docs/index.html','docs/guide.html','docs/404.html','docs/.nojekyll','docs/LICENSE.txt','docs/assets','docs/downloads','docs/publishing.md','scripts/build.ps1','scripts/serve-site.mjs','scripts/prepare-site.ps1','scripts/check-site.mjs')
    Commit-Feature 'docs: record localization and Pages verification' @('docs/security-audit.md','docs/accessibility-audit.md','docs/checklist.md','.project/verification.md','.project/commit-plan.md','scripts/commit-features.ps1')
    if ($Push) {
        $branch = (& git branch --show-current).Trim()
        if (-not $branch) { throw 'Cannot push a detached HEAD.' }
        & git push -u origin $branch
        if ($LASTEXITCODE -ne 0) { throw 'Push failed. Local commits are preserved; retry git push after checking network and authentication.' }
    }
} finally { Pop-Location }
