<?php
require __DIR__ . '/bootstrap.php';
start_session();
$title = SITE_NAME;
$base = BASE_PATH;
$api = base_url('api.php');
$admin = base_url('admin.php');
$manifest = base_url('manifest.webmanifest');
$logo = asset('assets/img/wordmark.png');
$apple = asset('assets/img/apple-touch-icon.png');
$css = asset('assets/css/app.css');
$js = asset('assets/js/app.js');
?><!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
  <meta name="theme-color" content="#0b0b12">
  <meta name="apple-mobile-web-app-capable" content="yes">
  <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
  <meta name="apple-mobile-web-app-title" content="Yılmaz Elektronik">
  <title><?= htmlspecialchars($title) ?> — Bayi sipariş</title>
  <link rel="manifest" href="<?= htmlspecialchars($manifest) ?>">
  <link rel="icon" href="<?= htmlspecialchars(asset('assets/img/favicon.png')) ?>">
  <link rel="apple-touch-icon" href="<?= htmlspecialchars($apple) ?>">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link href="https://fonts.googleapis.com/css2?family=Manrope:wght@500;700;800&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="<?= htmlspecialchars($css) ?>?v=5">
</head>
<body>
  <div class="topbar"><div class="topbar-inner">
    <span><?= htmlspecialchars(SITE_PHONE) ?> · <?= htmlspecialchars(SITE_EMAIL) ?></span>
    <span>Otomotiv elektronik toptan satış · Güçlü markalar · Hızlı tedarik</span>
  </div></div>
  <header class="head">
    <div class="head-inner">
      <a class="logo" href="<?= htmlspecialchars(base_url()) ?>">
        <span class="logo-mark"><b>YILMAZ</b><i>ELEKTRONİK</i></span>
      </a>
      <form class="search" id="searchForm">
        <input id="q" placeholder="Aramak istediğin ürünü yaz, kolayca bul!">
        <button type="submit">Ara</button>
      </form>
      <div class="head-actions">
        <button class="icon-btn" id="whoBtn"><span>👤</span><span id="who">Giriş Yap</span></button>
        <button class="icon-btn" id="cartBtn"><span>🛒</span><span>Sepetim</span><span class="badge hidden" data-cart-count>0</span></button>
      </div>
    </div>
  </header>
  <nav class="cats"><div class="nav-inner"></div></nav>
  <main id="view"></main>
  <footer class="site wrap">Yılmaz Elektronik · Otomotiv elektronik toptan satış · Bayi sipariş sistemi</footer>
  <nav class="bottom-nav shop">
    <button data-go="#/home">🏠<br>Vitrin</button>
    <button data-go="#/kategori/arac-ses">📦<br>Ürünler</button>
    <button data-go="#/sepet">🛒<br>Sepet</button>
    <button data-go="#/hesabim">👤<br>Hesabım</button>
  </nav>
  <a class="wa" href="https://wa.me/905313512111" target="_blank" rel="noopener">WA</a>
  <div id="toast" class="toast"></div>
  <script>
    window.APP = {
      base: <?= json_encode($base) ?>,
      api: <?= json_encode($api) ?>,
      admin: <?= json_encode($admin) ?>
    };
  </script>
  <script src="<?= htmlspecialchars($js) ?>?v=5"></script>
</body>
</html>
