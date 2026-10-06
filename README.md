# Akaryakıt Proje — ünpos Vardiya takip

Ünpos POS'tan bağımsız C# WinForms programı. Türpak XML ve Asis text dosyalarını otomasyon klasöründen okur, başka PC'de vardiya raporu üretir.

## Masaüstüne kurulum

Hazır paket GitHub Actions artifact'ında:

1. [Actions](https://github.com/unposbilisim-debug/-npos/actions) → **Windows EXE**
2. **AkaryakitProje** indirin
3. `AkaryakitProje-Setup.exe` çalıştırın  
   Varsayılan klasör: **Masaüstü\Akaryakit Proje**

Setup şunları koyar:

- `UnposVardiyaTakip.exe` — program
- `samples\` — örnek vardiya dosyaları
- `Kaynak\` — tüm kaynak kod (Visual Studio)

Zip ile: `AkaryakitProje-Masaustu.zip` açın, içindeki `MasaustuneKur.bat` çalıştırın.

## Visual Studio

`Kaynak\UnposVardiyaTakip.sln` veya depodaki `UnposVardiyaTakip.sln`  
Başlangıç projesi: **UnposVardiyaTakip.Win** (.NET 8 Desktop)

## Kullanım

1. Otomasyon PC'de klasörü paylaşın: `\\OTOMASYON-PC\shift`
2. Programda **Ayarlar**'a yolu yazın
3. İlk deneme: **Örnek vardiya yükle**

Otomasyona yazmaz. SQL'e bağlanmaz.
