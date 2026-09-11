<?php
require __DIR__ . '/bootstrap.php';
start_session();
$title = SITE_NAME . ' Yönetim';
$base = BASE_PATH;
$api = base_url('api.php');
$home = base_url();
$manifest = base_url('manifest.webmanifest');
$icon = asset('assets/img/icon-192.png');
$apple = asset('assets/img/apple-touch-icon.png');
$css = asset('assets/css/app.css');
$js = asset('assets/js/admin.js');
?><!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
  <meta name="theme-color" content="#0b0b12">
  <meta name="apple-mobile-web-app-capable" content="yes">
  <meta name="apple-mobile-web-app-title" content="ENS Yönetim">
  <title><?= htmlspecialchars($title) ?></title>
  <link rel="manifest" href="<?= htmlspecialchars($manifest) ?>">
  <link rel="icon" type="image/png" href="<?= htmlspecialchars(asset('assets/img/favicon.png')) ?>?v=12">
  <link rel="apple-touch-icon" href="<?= htmlspecialchars($apple) ?>?v=12">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link href="https://fonts.googleapis.com/css2?family=Manrope:wght@500;700;800&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="<?= htmlspecialchars($css) ?>?v=12">
</head>
<body>
  <header class="head">
    <div class="head-inner" style="grid-template-columns:1fr auto">
      <a class="logo" href="<?= htmlspecialchars($home) ?>">
        <img class="logo-img" src="<?= htmlspecialchars(asset('assets/img/logo-ens.png')) ?>?v=12" alt="ENS Oto Market">
        <span class="logo-mark"><strong>ENS OTO MARKET</strong><em>Yılmaz Elektronik iştirakidir</em></span>
      </a>
      <div class="head-actions">
        <button class="icon-btn" onclick="go('siparis')">Sipariş</button>
        <button class="icon-btn" onclick="go('ekle')">Ürün ekle</button>
        <button class="icon-btn" onclick="go('urunler')">Ürünler</button>
        <button class="icon-btn" onclick="go('bayiler')">Bayiler</button>
        <button class="icon-btn" id="logout">Çıkış</button>
      </div>
    </div>
  </header>
  <main id="view"></main>
  <nav class="bottom-nav">
    <button data-go="siparis">📋<br>Sipariş</button>
    <button class="fab" data-go="ekle">+</button>
    <button data-go="urunler">📦<br>Ürünler</button>
    <button data-go="bayiler">👥<br>Bayiler</button>
  </nav>
  <div id="toast" class="toast"></div>
  <script>
    window.APP = {
      base: <?= json_encode($base) ?>,
      api: <?= json_encode($api) ?>
    };
  </script>
  <script src="<?= htmlspecialchars($js) ?>?v=4"></script>
</body>
</html>
