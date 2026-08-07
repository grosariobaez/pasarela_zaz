[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Cardnet", "Azul")]
    [string]$Provider,

    [string]$QueueDestination,

    [switch]$StartService,

    [string]$ExecutablePath = (Join-Path $PSScriptRoot "bin\Release\net6.0\win-x64\publish\EasyPOS_Gateway.exe")
)

$ServiceName = "EasyPOS.PaymentGateway"

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute PowerShell como administrador para instalar el servicio."
}

if ($Provider -eq "Cardnet" -and [string]::IsNullOrWhiteSpace($QueueDestination)) {
    throw "QueueDestination es obligatorio para Cardnet."
}

if (-not [string]::IsNullOrEmpty($QueueDestination) -and $QueueDestination.Contains('"')) {
    throw "QueueDestination no puede contener comillas."
}

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existingService) {
    throw "El servicio '$ServiceName' ya existe. Desinstalelo antes de volver a instalarlo."
}

if ($Provider -eq "Azul") {
    $binaryPath = '"{0}" --service-auto "Azul"' -f $resolvedExecutable
    $destinationDescription = "Automatico segun la ruta hacia SQL Server"
}
else {
    $binaryPath = '"{0}" --service "{1}" "Cardnet"' -f $resolvedExecutable, $QueueDestination
    $destinationDescription = $QueueDestination
}

New-Service `
    -Name $ServiceName `
    -BinaryPathName $binaryPath `
    -DisplayName "EasyPOS Payment Gateway ($Provider)" `
    -Description "Procesa ventas, cancelaciones y cierres de EasyPOS mediante $Provider." `
    -StartupType Automatic

if ($StartService) {
    Start-Service -Name $ServiceName
}
$service = Get-Service -Name $ServiceName

Write-Host "Servicio instalado correctamente."
Write-Host "Nombre: $ServiceName"
Write-Host "Proveedor: $Provider"
Write-Host "Destino de cola SQL: $destinationDescription"
Write-Host "Estado: $($service.Status)"
