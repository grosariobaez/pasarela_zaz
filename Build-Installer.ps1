[CmdletBinding()]
param(
    [string]$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "EasyPOS_Cardnet.csproj"
$installerScript = Join-Path $projectRoot "installer\EasyPOS.PaymentGateway.iss"

if (-not (Test-Path -LiteralPath $InnoCompiler)) {
    throw "No se encontro Inno Setup en '$InnoCompiler'. Instale Inno Setup 6 o indique -InnoCompiler."
}

dotnet publish $projectFile -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish no pudo generar la aplicacion autocontenida."
}

& $InnoCompiler $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup no pudo generar el instalador."
}

$installer = Join-Path $projectRoot "installer\output\EasyPOS-Gateway-Setup.exe"
Write-Host "Instalador generado: $installer"
