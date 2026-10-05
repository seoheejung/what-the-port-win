$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'dist'
$artifactRoot = Join-Path $projectRoot 'artifacts'
& (Join-Path $PSScriptRoot 'build.ps1')
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
$files = @('WhatThePort.exe','WhatThePort.exe.config','WhatThePort.Core.dll','wtp.exe','LICENSE','README.md','fonts','docs') | ForEach-Object { Join-Path $outputRoot $_ }
$archive = Join-Path $artifactRoot 'WhatThePort-Windows-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Output "Portable package: $archive"
