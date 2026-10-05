$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$auditRoot = Join-Path $projectRoot ('artifacts\security-audit-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $auditRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\WhatThePort.Core.dll') -Destination $auditRoot
& $compiler /nologo /target:exe /platform:x64 "/out:$auditRoot\wt-fixture.exe" (Join-Path $projectRoot 'tests\TerminalAuditFixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
& $compiler /nologo /target:exe /platform:x64 "/out:$auditRoot\audit.exe" "/r:$auditRoot\WhatThePort.Core.dll" (Join-Path $projectRoot 'tests\SecurityAudit.cs')
if ($LASTEXITCODE -ne 0) { throw 'Audit compilation failed.' }
& (Join-Path $auditRoot 'audit.exe') $auditRoot | Tee-Object -FilePath (Join-Path $projectRoot 'artifacts\security-audit.txt')
if ($LASTEXITCODE -ne 0) { throw 'One or more observations did not reproduce; inspect the evidence.' }
