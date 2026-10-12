# Builds the distribution files in dist\:
#   WinSense-<versão>-setup.exe      instalador (Arquivos de Programas + desinstalador no Painel de Controle)
#   WinSense-<versão>-win-x64.zip    versão portátil
#   SHA256SUMS.txt
# Uso: .\scripts\publish.ps1   (requer o Inno Setup 6: winget install JRSoftware.InnoSetup)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = $props.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
$publishDir = Join-Path $root 'publish\win-x64'
$distDir = Join-Path $root 'dist'
$zip = Join-Path $distDir "WinSense-$version-win-x64.zip"
$setup = Join-Path $distDir "WinSense-$version-setup.exe"
# Build in a separate tree: the IDE (C# Dev Kit) builds the same projects in obj\ at the same time.
$artifacts = Join-Path $root 'artifacts'

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup' }

if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }

dotnet test (Join-Path $root 'WinSense.slnx') --artifacts-path $artifacts --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam; pacote não gerado.' }

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish (Join-Path $root 'src\WinSense.App\WinSense.App.csproj') -p:PublishProfile=Release-win-x64 `
    --artifacts-path $artifacts --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Falha no publish.' }

if (Test-Path $distDir) { Remove-Item -Recurse -Force $distDir }
New-Item -ItemType Directory -Force $distDir | Out-Null

& $iscc /Q "/DAppVersion=$version" (Join-Path $root 'installer\WinSense.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }

Copy-Item (Join-Path $root 'CHANGELOG.md') $publishDir
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip -Force

$sums = foreach ($file in $setup, $zip) {
    "$((Get-FileHash $file -Algorithm SHA256).Hash.ToLower())  $(Split-Path -Leaf $file)"
}
$sums | Set-Content -Encoding ascii (Join-Path $distDir 'SHA256SUMS.txt')

Write-Host "Instalador: $setup"
Write-Host "Portátil:   $zip"
$sums | ForEach-Object { Write-Host $_ }
