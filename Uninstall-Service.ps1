[CmdletBinding()]
param()

$ServiceName = "EasyPOS.PaymentGateway"

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute PowerShell como administrador para desinstalar el servicio."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Host "El servicio '$ServiceName' no esta instalado."
    return
}

if ($service.Status -ne 'Stopped') {
    Stop-Service -Name $ServiceName
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

sc.exe delete $ServiceName | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Windows no pudo eliminar el servicio '$ServiceName'."
}

Write-Host "Servicio '$ServiceName' eliminado correctamente."
