param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework 4.8 compiler is required.' }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$exe = Join-Path $OutputDirectory 'WordAss.exe'
$references = @('PresentationCore', 'PresentationFramework', 'WindowsBase', 'UIAutomationClient', 'UIAutomationTypes', 'System.Speech') |
    ForEach-Object { '/reference:' + (Join-Path $framework "WPF\$_.dll") }
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$exe" `
    "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\assets\wordass.ico" `
    /reference:System.Xaml.dll /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll `
    /reference:System.Security.dll /reference:System.Xml.Linq.dll @references @sources
if ($LASTEXITCODE -ne 0) { throw 'WordAss build failed.' }
Write-Output $exe
