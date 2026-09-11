# Yılmaz Toptan — B2B bayi sitesi (PWA)

Bayiler telefondan veya bilgisayardan ürün bakıp sepete atar, sipariş verir.
Patron kendi telefonundan barkod okutup fotoğraf çekerek ürün ekler.
Siparişler yönetim ekranına düşer. Ödeme alındıktan sonra onaylanır.
Online kart ödemesi **bilerek sonra** bırakıldı.

## Adresler

- Vitrin: `https://unposbarkod.com/yilmaz/`
- Yönetim: `https://unposbarkod.com/yilmaz/admin.php`
- Plesk’te ayrıca `unposyazilim.com.tr` alanı açıldı. DNS bu isme henüz bakmıyor; isim bağlanınca `http://www.unposyazilim.com.tr/yilmaz/` da aynı siteyi açar.

## Girişler (örnek)

| Kim | Telefon | Şifre |
| --- | --- | --- |
| Patron | 0555 000 00 00 | Patron123! |
| Bayi | 0555 123 45 67 | Bayi123! |

Telefonun ana ekranına eklemek için sitede “Ana ekrana ekle / Add to Home Screen” deyin. PWA olarak açılır.

## Patron telefonundan ürün nasıl girer?

1. `admin.php` açılır, giriş yapılır.
2. Ortadaki **+** düğmesine basılır.
3. **Barkodu kameradan oku** — kamera ürüne tutulur.
4. **Fotoğrafı çek** — ürün resmi alınır.
5. Ad, fiyat, stok yazılır, **Kaydet**.

Sipariş gelince listede görünür. Havale düşünce **Ödeme alındı, onayla** denir.

## Yerelde çalıştırma

```bash
php -S 127.0.0.1:8080 -t /workspace
```

Aç: http://127.0.0.1:8080/
