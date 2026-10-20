[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CommonAssembly,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
Get-ChildItem -LiteralPath (Split-Path -Parent $CommonAssembly) -Filter '*.dll' |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $OutputDirectory }
$exe = Join-Path $OutputDirectory 'TeknoNaomiOnlineChecks.exe'
& $Compiler /nologo /langversion:9 /target:exe /platform:x86 "/reference:$CommonAssembly" "/out:$exe" (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Naomi online checks did not compile.' }
& $exe $root $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Naomi online checks failed.' }
