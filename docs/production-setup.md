# UnAccess üretim kurulumu

Bu belge Aşama 5 kurulum gereksinimlerini tanımlar. Gerçek MSI kurulumu ve Windows ACL uygulaması kurulum aracı aşamasına bırakılmıştır. Bağlantı dizesi, SQL parolası ve cihaz kimlik bilgisi bu belgede ve kurulum betiklerinde yer almaz.

## Süreç yerleşimi

- `UnAccess.Desktop` istemci bilgisayarlarda çalışır ve SQL Server'a bağlanır.
- `UnAccess.DeviceService` SQL Server'ın erişilebildiği bir makinede çalışır. Otomatik yedekleme zamanlaması bu servisin içindedir.
- Zamanlama SQL Server Agent veya Windows Task Scheduler kullanmaz. SQL Server Express kurulumlarında Agent bulunmayabilir.
- Yedek dosyasını servis kendisi kopyalamaz. SQL Server `BACKUP DATABASE` komutu dosyayı yazar.
- Yedek klasörü, SQL Server servis hesabının gördüğü yoldur. Desktop bilgisayarının disk yolu ile aynı olmak zorunda değildir.

## Korunan bağlantı ayarı

Üretimde bağlantı dizesi kaynak koduna, `appsettings.json` dosyasına veya Git deposuna yazılmaz. Desktop ve DeviceService aynı makine düzeyindeki korunan dosyayı okur:

- Konum mantığı `IAppPathProvider` üzerinden gelir. Windows'ta bu, ortak uygulama verisi altındaki `UnAccess/Configuration/connection.protected` dosyasıdır.
- Değer Windows DPAPI `LocalMachine` kapsamı ile korunur. Anahtar dosyanın yanına düz metin olarak konmaz.
- Geliştirmede `ConnectionStrings__UnAccess` ortam değişkeni veya `appsettings.Local.json` kullanılabilir. Ortam değişkeni doluysa o değer kullanılır.
- Dosya sistemi izinleri kurulumu gerçek installer aşamasındadır. Gereksinim: normal hastane kullanıcısı dosyayı değiştiremez; `UnAccess Device Service` hesabı okuyabilir; Desktop yalnızca kurulum ve okuma için gereken erişime sahip olur. Önerilen ACL: SYSTEM ve Administrators tam denetim, DeviceService hesabı okuma, kullanıcı hesabına yazma yok.

## Yedekleme ve geri yükleme kaydı

`RESTORE DATABASE` başarılı olursa veritabanının içindeki `RestoreHistory`, `BackupHistory` ve `AuditLog` seçilen yedeğin zamanına döner. Bu nedenle geri yükleme başlangıcı ve sonucu yalnız veritabanı tablosuna bırakılmaz. Aynı kayıt uygulama günlüğüne de yazılır. Başarılı geri yüklemeden sonra servis, yeni veritabanı bağlantısı üzerinde tamamlanma kaydını yeniden oluşturur.

Başarısız geri yüklemede üretim veritabanı `DROP` edilmez. Mümkünse geri yükleme öncesi alınan güvenlik yedeği yeniden uygulanır ve veritabanı çok kullanıcılı moda alınır.

## Bakım kilidi

Yedekleme ve geri yükleme `master` veritabanında `sp_getapplock` kullanır. Kaynak adı `UnAccess.Maintenance:` artı veritabanı adıdır; kilit geri yüklenen veritabanının içinde değildir ve oturum kapanınca SQL Server bırakır. Aynı veritabanını kullanan Desktop ve DeviceService birbirini bekler. Farklı veritabanları birbirini kilitlemez. DeviceService senkronizasyon turu paylaşılan kilit alamazsa yeni iş başlatmaz, heartbeat yazmaya devam eder. Geri yükleme bitince kilit kalkar ve senkronizasyon sürer.

## Servis

Geliştirmede DeviceService konsol olarak çalışır. Windows Service adı: `UnAccess Device Service`. Örnek kurulum betiği `scripts/install-unaccess-device-service.ps1` dosyasındadır ve kimlik bilgisi içermez.

## Sürüm ve güncelleme

Sürümün tek kaynağı derleme metadata'sıdır (`Directory.Build.props`). Bu aşamada güncelleme indirme, MSI/EXE indirme veya kendi kendini güncelleme yoktur. `IUpdateCheckService` üretimde `NotConfigured` döner.
