# ünpos Vardiya takip

Ünpos POS'tan **bağımsız** çalışan vardiya raporlama programı. Türpak XML ve Asis text dosyalarını otomasyon PC'sindeki paylaşılan klasörden okur; raporlama başka bir PC'de yapılır.

Otomasyon yazılımına yazmaz. SQL'e bağlanmaz. Sadece export klasörünü izler.

## Ne yapar?

- Türpak: `Sales.xml` (açık vardiya) ve vardiya kapanınca oluşan XML/zip
- Asis: text/csv vardiya dökümü
- Pompacı, yakıt, ödeme tipi, tabanca/endeks icmali
- Excel ve yazdırılabilir rapor

## Kurulum (raporlama PC)

1. Otomasyon PC'de Türpak/Asis'in yazdığı klasörü **salt okunur** paylaşın, örneğin `\\OTOMASYON-PC\shift`.
2. Bu programı ofis veya kasa PC'sine kopyalayın.
3. Windows'ta `start.bat` çalıştırın (Python 3 gerekir).
4. Tarayıcı `http://127.0.0.1:8787` adresini açar.
5. **Ayarlar** ekranına paylaşım yolunu yazın: `\\OTOMASYON-PC\shift`.

Kasiyer otomasyonda vardiyayı kapatınca dosya klasöre düşer, program okur, rapor hazır olur.

Linux/macOS: `./start.sh`

## Örnek veri

Kurulumdan sonra ana sayfada **Örnek vardiya yükle** ile `samples/` altındaki Türpak ve Asis dosyaları içeri alınır.

## Geliştirme

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
pytest
python run.py
```

## Notlar

- Türpak miktar/tutar çoğu sürüme göre tam sayıdır. Varsayılan bölen 100'dür (4560 → 45,60 L). Ayarlardan değiştirilir.
- Kaynak dosya silinmez; kopyası `data/processed/` altına alınır.
- POS, kasa sayımı ve muhasebe aktarımı bu ürünün kapsamında değildir.
