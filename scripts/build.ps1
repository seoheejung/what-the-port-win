param([switch]$Tests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows x64 with .NET Framework 4.8 is required.' }
$outputRoot = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$coreFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Core') -Filter '*.cs' | ForEach-Object FullName)
& $compiler /nologo /target:library /platform:x64 /optimize+ /warnaserror+ "/out:$outputRoot\WhatThePort.Core.dll" /r:System.Runtime.Serialization.dll $coreFiles
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
$uiFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\App') -Filter '*.cs' | ForEach-Object FullName)
if ($uiFiles.Count -gt 0) {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$outputRoot\WhatThePort.exe" "/win32manifest:$projectRoot\src\app.manifest" "/r:$outputRoot\WhatThePort.Core.dll" "/r:$framework\WPF\PresentationFramework.dll" "/r:$framework\WPF\PresentationCore.dll" "/r:$framework\WPF\WindowsBase.dll" /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "/resource:$projectRoot\src\App\Shell.xaml,Shell.xaml" $uiFiles
    if ($LASTEXITCODE -ne 0) { throw 'App compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\App.config') -Destination (Join-Path $outputRoot 'WhatThePort.exe.config') -Force
}
if (Test-Path -LiteralPath (Join-Path $projectRoot 'src\Cli\Program.cs')) {
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warnaserror+ "/out:$outputRoot\wtp.exe" "/r:$outputRoot\WhatThePort.Core.dll" (Join-Path $projectRoot 'src\Cli\Program.cs')
    if ($LASTEXITCODE -ne 0) { throw 'CLI compilation failed.' }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\fonts') -Destination $outputRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $outputRoot -Force
if (Test-Path -LiteralPath (Join-Path $projectRoot 'README.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputRoot -Force }
if (Test-Path -LiteralPath (Join-Path $projectRoot 'docs')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $outputRoot -Recurse -Force }
if ($Tests) {
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warnaserror+ "/out:$outputRoot\wtp-tests.exe" "/r:$outputRoot\WhatThePort.Core.dll" (Join-Path $projectRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /warnaserror+ "/out:$outputRoot\wtp-ui-tests.exe" "/r:$outputRoot\WhatThePort.Core.dll" "/r:$outputRoot\WhatThePort.exe" "/r:$framework\WPF\PresentationFramework.dll" "/r:$framework\WPF\PresentationCore.dll" "/r:$framework\WPF\WindowsBase.dll" /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $projectRoot 'tests\UiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\App.config') -Destination (Join-Path $outputRoot 'wtp-ui-tests.exe.config') -Force
}
Write-Output "Built: $outputRoot"
