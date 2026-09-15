# Lisans paneli — Desk katmanı

Canlı kod Plesk’te: `lisans.unposbarkod.com` → `/var/www/vhosts/unposbarkod.com/lisans.unposbarkod.com`.

Bu klasör, o Laravel uygulamasının Desk dilimine ait dosyaların kopyasıdır (vendor, `.env` ve depolama yok).

## Bu dilimde ne var

- Desk: sıkı KPI şeridi + kısa 90 günlük lisans/yazar kasa bloğu (teklif yok; teklif yalnızca `/OfferList`)
- Zil yalnızca yaklaşan süreler; teklif sayısı rozet değil
- `/dashboard` → `/Desk` (bayi için `/agent/dashboard`)
- Grafik birimi (`bin` / `Mn`) ve “aktif paket = paket satırı” dipnotu

## Yayın

Dosyaları yedekledikten sonra aynı göreli yollara kopyalayın; `php artisan view:clear` / `cache:clear` yeterli.
