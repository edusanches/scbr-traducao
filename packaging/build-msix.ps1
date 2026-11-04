<#
.SYNOPSIS
  Gera o MSIX da Tradução SCBR: dotnet publish autocontido (win-x64), o índice das imagens (makepri) e o pacote
  (makeappx). Roda no Windows, no PowerShell 5.1 ou 7.

.DESCRIPTION
  Sem parâmetros, gera um pacote de teste, com uma identidade de teste. Para a loja, passe os três valores de
  Partner Center > Gerenciamento do produto > Identidade do produto. O pacote sai sem assinatura: a loja assina.

  O makeappx e o makepri vêm do Windows SDK, se ele estiver instalado, ou do pacote NuGet
  Microsoft.Windows.SDK.BuildTools (packaging/ferramentas), baixado no primeiro uso.

.EXAMPLE
  powershell -File packaging/build-msix.ps1

.EXAMPLE
  powershell -File packaging/build-msix.ps1 -IdentityName "12345SCBR.TraducaoSCBR" -Publisher "CN=00000000-0000-0000-0000-000000000000" -PublisherDisplayName "SCBR"

.EXAMPLE
  powershell -File packaging/build-msix.ps1 -SoLayout
  Só monta a pasta do pacote (artifacts/layout), para testar com Add-AppxPackage -Register.
#>
param(
  [string]$IdentityName = "SCBR.TraducaoSCBR.Teste",
  [string]$Publisher = "CN=TraducaoSCBR-Teste",
  [string]$PublisherDisplayName = "SCBR (teste)",
  [string]$Version = "",
  [string]$Configuration = "Release",
  [string]$OutDir = "",
  [switch]$SoLayout
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root "artifacts" }
$publish = Join-Path $OutDir "publish"
$layout = Join-Path $OutDir "layout"

function Invoke-Native([string]$exe, [string[]]$arguments) {
  & $exe @arguments
  if ($LASTEXITCODE -ne 0) { throw "$([System.IO.Path]::GetFileName($exe)) falhou (código $LASTEXITCODE)." }
}

function Find-Dotnet {
  $local = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
  if ($env:DOTNET_ROOT -and (Test-Path (Join-Path $env:DOTNET_ROOT "dotnet.exe"))) { return (Join-Path $env:DOTNET_ROOT "dotnet.exe") }
  $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  if (Test-Path $local) { return $local }
  throw "Não achei o dotnet. Instale o SDK do .NET 10."
}

# O makeappx e o makepri: os do Windows SDK, ou os do pacote NuGet.
function Find-SdkTools([string]$dotnet) {
  $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
  if (Test-Path $kits) {
    $dir = Get-ChildItem $kits -Directory | Where-Object { Test-Path (Join-Path $_.FullName "x64\makeappx.exe") } |
      Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($dir) { return (Join-Path $dir.FullName "x64") }
  }

  $project = Join-Path $root "packaging\ferramentas\Ferramentas.csproj"
  $packageVersion = ([xml](Get-Content $project -Raw)).Project.ItemGroup.PackageReference.Version
  Write-Host "Windows SDK não encontrado: usando o Microsoft.Windows.SDK.BuildTools $packageVersion do NuGet."
  Invoke-Native $dotnet @("restore", $project, "--nologo", "-v", "q")
  $globalPackages = (& $dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''
  $bin = Join-Path $globalPackages.Trim() "microsoft.windows.sdk.buildtools\$packageVersion\bin"
  $dir = Get-ChildItem $bin -Directory | Where-Object { Test-Path (Join-Path $_.FullName "x64\makeappx.exe") } | Select-Object -First 1
  if (-not $dir) { throw "Não achei o makeappx em $bin." }
  return (Join-Path $dir.FullName "x64")
}

if (-not $Version) {
  $props = [xml](Get-Content (Join-Path $root "Directory.Build.props") -Raw)
  $Version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
}
$parts = $Version.Split(".")
if ($parts.Count -eq 3) { $Version = "$Version.0" }
if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "Versão inválida: $Version (a loja pede quatro números, com o último zero)." }

$dotnet = Find-Dotnet
Write-Host "1/4 dotnet publish ($Configuration, win-x64, autocontido)"
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
Invoke-Native $dotnet @(
  "publish", (Join-Path $root "src\TraducaoScbr\TraducaoScbr.csproj"),
  "-c", $Configuration, "-r", "win-x64", "--self-contained", "true",
  "-p:DebugType=None", "-p:DebugSymbols=false", "-o", $publish, "--nologo", "-v", "q")

Write-Host "2/4 Pasta do pacote ($Version, $IdentityName)"
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
New-Item -ItemType Directory -Force $layout | Out-Null
Copy-Item -Recurse (Join-Path $publish "*") $layout
Copy-Item -Recurse (Join-Path $root "packaging\Assets") (Join-Path $layout "Assets")
$manifest = [System.IO.File]::ReadAllText((Join-Path $root "packaging\AppxManifest.xml"), [System.Text.Encoding]::UTF8)
$manifest = $manifest.Replace("__IDENTITY_NAME__", [System.Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace("__PUBLISHER__", [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $Version)
[System.IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

$tools = Find-SdkTools $dotnet
Write-Host "3/4 Índice das imagens (makepri, de $tools)"
Invoke-Native (Join-Path $tools "makepri.exe") @(
  "new", "/pr", $layout, "/cf", (Join-Path $root "packaging\priconfig.xml"),
  "/mn", (Join-Path $layout "AppxManifest.xml"), "/of", (Join-Path $layout "resources.pri"), "/o")

if ($SoLayout) {
  Write-Host ""
  Write-Host "Pasta do pacote em $layout. Para testar (com o Modo de Desenvolvedor ligado):"
  Write-Host "  Add-AppxPackage -Register `"$layout\AppxManifest.xml`""
  exit 0
}

Write-Host "4/4 Pacote (makeappx)"
$msix = Join-Path $OutDir ("TraducaoSCBR_{0}_x64.msix" -f $Version)
Invoke-Native (Join-Path $tools "makeappx.exe") @("pack", "/d", $layout, "/p", $msix, "/o")
$size = (Get-Item $msix).Length / 1MB
Write-Host ""
Write-Host ("Pronto: {0} ({1:N1} MB), sem assinatura." -f $msix, $size)
