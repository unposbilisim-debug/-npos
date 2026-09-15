# Lisans paneli — Desk katmanı

Canlı kod Plesk’te: `lisans.unposbarkod.com` → `/var/www/vhosts/unposbarkod.com/lisans.unposbarkod.com`.

Bu klasör, o Laravel uygulamasının Desk dilimine ait dosyaların kopyasıdır (vendor, `.env` ve depolama yok).

## Bu dilimde ne var

- Desk iş kuyruğu: 90 güne giren lisans, 90 güne giren yazar kasa, bekleyen teklif
- Bildirim zili bu kuyruğa bağlı
- `/dashboard` → `/Desk` (bayi için `/agent/dashboard`)
- Grafik birimi (`bin` / `Mn`, `B` yok) ve “aktif lisans = paket satırı” dipnotu

## Yayın

Dosyaları yedekledikten sonra aynı göreli yollara kopyalayın; `php artisan view:clear` / `cache:clear` yeterli.
