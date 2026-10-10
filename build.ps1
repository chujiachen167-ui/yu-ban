param([switch]$Test)
$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$compilerOptions = @('/codepage:65001')
$outDir = Join-Path $base 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.Security.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll','System.Speech.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$references += '/r:' + (Join-Path $framework 'System.Xaml.dll')
$references += '/resource:' + (Join-Path $base 'src\Settings.xaml') + ',EnglishCompanion.Settings.xaml'
$references += '/resource:' + (Join-Path $base 'assets\wordmark\yuban.path') + ',EnglishCompanion.Wordmark.path'
foreach ($skin in @('ocean','baby')) { $references += '/resource:' + (Join-Path $base ('assets\' + $skin + '.png')) + ',EnglishCompanion.' + $skin + '.png' }
$references += '/resource:' + (Join-Path $base 'assets\pet-atlas.png') + ',EnglishCompanion.PetAtlas.png'
$references += '/win32icon:' + (Join-Path $base 'assets\companion.ico')
$references += '/resource:' + (Join-Path $base 'assets\companion.ico') + ',EnglishCompanion.Icon.ico'
$references += '/resource:' + (Join-Path $base 'assets\dictionary\words.jsonl.gz') + ',EnglishCompanion.Dictionary.gz'
$references += '/resource:' + (Join-Path $base 'assets\dictionary\chinese.jsonl.gz') + ',EnglishCompanion.ChineseDictionary.gz'
& (Join-Path $base 'assets\liquid\compile.ps1')
$references += '/resource:' + (Join-Path $base 'assets\liquid\LiquidSurface.ps') + ',EnglishCompanion.LiquidSurface.ps'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $base 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:winexe /main:EnglishCompanion.Program ('/win32manifest:' + (Join-Path $base 'app.manifest')) ('/out:' + (Join-Path $outDir 'EnglishCompanion.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:exe /main:EnglishCompanion.ProbeHost ('/out:' + (Join-Path $outDir 'CompanionProbe.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
& $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:exe /main:EnglishCompanion.VoiceHost ('/out:' + (Join-Path $outDir 'CompanionVoice.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Voice build failed' }
if ($Test) {
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $base 'tests') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:exe /main:EnglishCompanion.Tests ('/out:' + (Join-Path $outDir 'CompanionTests.exe')) @references @sources @testSources
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & (Join-Path $outDir 'CompanionTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
if ($args -contains '-Diag') {
    & $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:exe /main:EnglishCompanion.InputDiagnostics ('/out:' + (Join-Path $outDir 'CompanionDiag.exe')) @references (Join-Path $base 'src\InputDiagnostics.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic build failed' }
    & $compiler @compilerOptions /nologo /optimize+ /platform:x64 /target:exe /main:EnglishCompanion.TrayDiagnostics ('/out:' + (Join-Path $outDir 'CompanionTrayDiag.exe')) @references (Join-Path $base 'src\TrayDiagnostics.cs') (Join-Path $base 'src\AppIcon.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Tray diagnostic build failed' }
}
Get-Item (Join-Path $outDir 'EnglishCompanion.exe') | Select-Object Name, Length
