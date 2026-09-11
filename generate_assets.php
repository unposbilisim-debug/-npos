<?php
/**
 * Generates PWA icons, logo, banners and sample product photos with GD.
 */
$dir = __DIR__ . '/assets/img';
@mkdir($dir, 0775, true);
@mkdir($dir . '/products', 0775, true);
@mkdir($dir . '/banners', 0775, true);

function canvas(int $w, int $h, array $rgb): GdImage
{
    $im = imagecreatetruecolor($w, $h);
    imagealphablending($im, true);
    $bg = imagecolorallocate($im, $rgb[0], $rgb[1], $rgb[2]);
    imagefilledrectangle($im, 0, 0, $w, $h, $bg);
    return $im;
}

function rounded(GdImage $im, int $x, int $y, int $w, int $h, int $r, int $color): void
{
    imagefilledrectangle($im, $x + $r, $y, $x + $w - $r, $y + $h, $color);
    imagefilledrectangle($im, $x, $y + $r, $x + $w, $y + $h - $r, $color);
    imagefilledellipse($im, $x + $r, $y + $r, $r * 2, $r * 2, $color);
    imagefilledellipse($im, $x + $w - $r, $y + $r, $r * 2, $r * 2, $color);
    imagefilledellipse($im, $x + $r, $y + $h - $r, $r * 2, $r * 2, $color);
    imagefilledellipse($im, $x + $w - $r, $y + $h - $r, $r * 2, $r * 2, $color);
}

function savePng(GdImage $im, string $path): void
{
    imagepng($im, $path, 6);
    imagedestroy($im);
}

function ttf(): ?string
{
    foreach ([
        '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
        '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
        '/usr/share/fonts/truetype/liberation/DejaVuSans-Bold.ttf',
    ] as $f) {
        if (is_file($f)) {
            return $f;
        }
    }
    return null;
}

$font = ttf();

function text(GdImage $im, ?string $font, int $size, int $x, int $y, int $color, string $str): void
{
    if ($font) {
        imagettftext($im, $size, 0, $x, $y, $color, $font, $str);
    } else {
        imagestring($im, 5, $x, $y - 12, $str, $color);
    }
}

// --- App icon ---
foreach ([192, 512, 180] as $size) {
    $im = canvas($size, $size, [20, 115, 200]);
    $white = imagecolorallocate($im, 255, 255, 255);
    $orange = imagecolorallocate($im, 255, 138, 0);
    $pad = (int) ($size * 0.14);
    rounded($im, $pad, $pad, $size - $pad * 2, $size - $pad * 2, (int) ($size * 0.18), $white);
    $inner = (int) ($size * 0.22);
    rounded($im, $inner, $inner, $size - $inner * 2, $size - $inner * 2, (int) ($size * 0.14), imagecolorallocate($im, 20, 115, 200));
    $dot = (int) ($size * 0.12);
    imagefilledellipse($im, (int) ($size * 0.72), (int) ($size * 0.28), $dot, $dot, $orange);
    $letter = $size >= 256 ? 120 : ($size >= 180 ? 52 : 48);
    $tx = (int) ($size * 0.30);
    $ty = (int) ($size * 0.62);
    text($im, $font, (int) ($size * 0.28), $tx, $ty, $white, 'Y');
    $name = $size === 180 ? "$dir/apple-touch-icon.png" : "$dir/icon-$size.png";
    savePng($im, $name);
}

// --- Logo ---
$im = canvas(640, 160, [255, 255, 255]);
imagecolortransparent($im, imagecolorallocate($im, 255, 255, 255));
$blue = imagecolorallocate($im, 20, 115, 200);
$orange = imagecolorallocate($im, 255, 138, 0);
$dark = imagecolorallocate($im, 26, 35, 50);
rounded($im, 16, 24, 112, 112, 28, $blue);
text($im, $font, 52, 46, 100, imagecolorallocate($im, 255, 255, 255), 'Y');
imagefilledellipse($im, 112, 40, 28, 28, $orange);
text($im, $font, 36, 150, 78, $dark, 'yilmaztoptan');
text($im, $font, 14, 152, 112, $blue, 'B2B  •  Bayi sipariş platformu');
savePng($im, "$dir/logo.png");

// --- Favicon 32 ---
$im = canvas(32, 32, [20, 115, 200]);
$w = imagecolorallocate($im, 255, 255, 255);
text($im, $font, 16, 8, 24, $w, 'Y');
savePng($im, "$dir/favicon.png");

// --- Banners ---
$banners = [
    ['Hugin T300', 'Yeni nesil yazar kasa POS', 'Bayi fiyatıyla stokta', [12, 24, 48], [20, 115, 200]],
    ['PAX A930', 'Android tahsilat terminali', 'Hızlı teslimat • Toptan', [18, 10, 40], [255, 90, 20]],
    ['Mix Para Sayma', 'Karışık para sayma makineleri', 'Servis destekli satış', [8, 40, 36], [0, 140, 110]],
];
foreach ($banners as $i => $b) {
    $im = canvas(1200, 420, $b[3]);
    $white = imagecolorallocate($im, 255, 255, 255);
    $muted = imagecolorallocate($im, 210, 225, 245);
    rounded($im, 820, 50, 300, 320, 40, imagecolorallocate($im, $b[4][0], $b[4][1], $b[4][2]));
    rounded($im, 850, 80, 240, 180, 24, imagecolorallocate($im, 20, 20, 28));
    imagefilledellipse($im, 970, 300, 36, 36, $white);
    text($im, $font, 42, 60, 150, $white, $b[0]);
    text($im, $font, 22, 62, 210, $muted, $b[1]);
    text($im, $font, 18, 62, 280, $white, $b[2]);
    savePng($im, "$dir/banners/b" . ($i + 1) . '.png');
}

// --- Product photos ---
$products = [
    ['Hugin T300', 'Yazar Kasa POS', [20, 115, 200]],
    ['PAX A930', 'Android POS', [40, 40, 55]],
    ['Ingenico Move 5000', 'Taşınabilir POS', [0, 90, 160]],
    ['Paygo N910', 'Android POS', [30, 160, 90]],
    ['Mix 1200', 'Para Sayma', [200, 40, 50]],
    ['Mix 2100', 'Karışık Sayma', [180, 30, 40]],
    ['Honeywell 1450g', 'Barkod Okuyucu', [240, 140, 0]],
    ['Zebra DS2208', '2D Okuyucu', [50, 50, 50]],
    ['Termal 80mm', 'Fiş Yazıcı', [90, 60, 160]],
    ['Nakit Çekmece', '5 Göz Metal', [70, 80, 90]],
    ['Dokunmatik 15.6"', 'POS PC', [15, 90, 140]],
    ['Barkodlu Terazi', '30 kg', [0, 130, 100]],
];

foreach ($products as $i => $p) {
    $im = canvas(800, 800, [244, 247, 250]);
    $card = imagecolorallocate($im, 255, 255, 255);
    rounded($im, 40, 40, 720, 720, 48, $card);
    $accent = imagecolorallocate($im, $p[2][0], $p[2][1], $p[2][2]);
    // device body
    rounded($im, 230, 120, 340, 480, 36, $accent);
    rounded($im, 255, 150, 290, 280, 18, imagecolorallocate($im, 18, 22, 32));
    $white = imagecolorallocate($im, 255, 255, 255);
    imagefilledellipse($im, 400, 520, 28, 28, $white);
    $dark = imagecolorallocate($im, 26, 35, 50);
    $gray = imagecolorallocate($im, 110, 120, 135);
    // center titles roughly
    $name = $p[0];
    text($im, $font, 28, 80, 660, $dark, $name);
    text($im, $font, 16, 80, 700, $gray, $p[1]);
    savePng($im, $dir . '/products/p' . ($i + 1) . '.png');
}

echo "OK assets generated\n";
