# ünpos Vardiya takip

Ünpos POS'tan **bağımsız** C# WinForms masaüstü programı. Türpak XML ve Asis text dosyalarını otomasyon PC'sindeki paylaşılan klasörden okur; raporlama başka bir Windows PC'de yapılır.

Otomasyon yazılımına yazmaz. SQL'e bağlanmaz. Sadece export klasörünü izler.

## Kurulum (raporlama PC)

Windows'ta .NET 8 Desktop Runtime veya SDK gerekir.

```bat
start.bat
```

veya Visual Studio ile `UnposVardiyaTakip.sln` açın, `UnposVardiyaTakip.Win` başlatın.

Yayımlama:

```bat
dotnet publish src\UnposVardiyaTakip.Win\UnposVardiyaTakip.Win.csproj -c Release -r win-x64 --self-contained true
```

## Kullanım

1. Otomasyon PC'de Türpak/Asis export klasörünü **salt okunur** paylaşın: `\\OTOMASYON-PC\shift`
2. Bu programı ofis/kasa PC'sinde çalıştırın
3. **Ayarlar**'a paylaşım yolunu yazın
4. **Örnek vardiya yükle** ile `samples/` dosyalarını deneyin
5. Kasiyer vardiyayı kapatınca dosya klasöre düşer, icmal otomatik gelir

## Raporlar

- Yakıt / ödeme / pompacı icmali
- Tabanca endeks farkı
- Satış listesi
- Excel ve yazdırma

Türpak tam sayı litre/tutar için varsayılan bölen 100'dür (4560 → 45,60 L). Ayarlardan değiştirilir.
