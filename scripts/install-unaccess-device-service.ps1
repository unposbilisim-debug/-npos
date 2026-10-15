#Requires -RunAsAdministrator
<#
    UnAccess Device Service örneği.
    Bağlantı ayarı korunan makine yapılandırmasından okunur.
    Bu betiğe SQL parolası, bağlantı dizesi veya cihaz kimlik bilgisi yazmayın.
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $BinaryPath
)

if (-not (Test-Path -LiteralPath $BinaryPath)) {
    throw "DeviceService uygulaması bulunamadı."
}

$serviceName = "UnAccessDeviceService"
$displayName = "UnAccess Device Service"
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    throw "Servis zaten kayıtlı. Kaldırıp yeniden oluşturmadan önce çalışan örneği durdurun."
}

New-Service -Name $serviceName -BinaryPathName $BinaryPath -DisplayName $displayName -StartupType Automatic -Description "UnAccess cihaz senkronizasyonu ve yedekleme zamanlayıcısı"
Write-Output "Servis kaydedildi. Bağlantı ayarını ayrıca korunan yapılandırma dosyasına yazın."
