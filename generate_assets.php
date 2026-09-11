<?php
/**
 * Red/black PWA icons + oto elektronik product photos.
 */
$dir = __DIR__ . '/assets/img';
@mkdir($dir . '/products', 0775, true);
@mkdir($dir . '/banners', 0775, true);

function canvas(int $w, int $h, array $rgb): GdImage
{
    $im = imagecreatetruecolor($w, $h);
    imagealphablending($im, true);
    imagesavealpha($im, false);
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
function ttf(): ?string
{
    foreach (['/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'] as $f) {
        if (is_file($f)) return $f;
    }
    return null;
}
function text(GdImage $im, ?string $font, float $size, int $x, int $y, int $color, string $str): void
{
    if ($font) imagettftext($im, $size, 0, $x, $y, $color, $font, $str);
    else imagestring($im, 5, $x, $y - 12, $str, $color);
}
function savePng(GdImage $im, string $path): void
{
    imagepng($im, $path, 6);
    imagedestroy($im);
}

$font = ttf();
$red = [225, 6, 0];
$black = [14, 14, 18];

foreach ([192, 512, 180] as $size) {
    $im = canvas($size, $size, $black);
    $r = imagecolorallocate($im, 225, 6, 0);
    $w = imagecolorallocate($im, 255, 255, 255);
    imagefilledellipse($im, (int)($size/2), (int)($size/2), (int)($size*0.86), (int)($size*0.86), $r);
    imagefilledellipse($im, (int)($size/2), (int)($size/2), (int)($size*0.62), (int)($size*0.62), imagecolorallocate($im, 14, 14, 18));
    text($im, $font, (int)($size * 0.28), (int)($size * 0.32), (int)($size * 0.62), $w, 'Y');
    $name = $size === 180 ? "$dir/apple-touch-icon.png" : "$dir/icon-$size.png";
    savePng($im, $name);
}
$im = canvas(32, 32, $black);
text($im, $font, 16, 8, 24, imagecolorallocate($im, 255, 255, 255), 'Y');
savePng($im, "$dir/favicon.png");

// Shrink provided logo for web
$srcPath = $dir . '/logo-full.png';
if (is_file($srcPath)) {
    $src = imagecreatefrompng($srcPath);
    $scaled = imagescale($src, 900, 900);
    imagepng($scaled, $dir . '/logo.png', 6);
    imagedestroy($src);
    imagedestroy($scaled);
}

$products = [
    ['Pioneer 16cm Hoparlör', 'Araç Ses', [30, 30, 32], 'speaker'],
    ['JBL Stage3 627', 'Hoparlör Set', [40, 20, 20], 'speaker'],
    ['Pioneer GM Amfi', '2 Kanal Amfi', [20, 20, 28], 'amp'],
    ['Alpine 20cm Sub', 'Subwoofer', [18, 18, 22], 'sub'],
    ['Pioneer 2DIN Teyp', 'Multimedya', [12, 24, 48], 'screen'],
    ['Android 10" Teyp', 'Tablet Ekran', [20, 20, 30], 'screen'],
    ['9" Android Ekran', 'Multimedya', [24, 18, 40], 'screen'],
    ['Ön+Arka Kamera', 'Oto Güvenlik', [28, 28, 32], 'cam'],
    ['Geri Görüş Kamerası', 'Güvenlik', [22, 32, 28], 'cam'],
    ['Park Sensörü 4lü', 'Güvenlik', [32, 32, 20], 'sensor'],
    ['H7 LED Far Set', 'Aydınlatma', [40, 36, 10], 'led'],
    ['T10 İç Aydınlatma', 'LED', [50, 40, 12], 'led'],
    ['USB Araç Şarj', 'Aksesuar', [24, 24, 30], 'usb'],
    ['ISO Soket Adaptör', 'Adaptör', [30, 24, 20], 'plug'],
    ['Telefon Tutucu', 'Mobil Yaşam', [20, 28, 36], 'hold'],
    ['Tweeter Set 300W', 'Araç Ses', [36, 16, 16], 'tweet'],
];

foreach ($products as $i => $p) {
    $im = canvas(800, 800, [245, 246, 248]);
    $white = imagecolorallocate($im, 255, 255, 255);
    $dark = imagecolorallocate($im, 20, 20, 24);
    $gray = imagecolorallocate($im, 110, 118, 130);
    $accent = imagecolorallocate($im, $p[2][0], $p[2][1], $p[2][2]);
    rounded($im, 40, 40, 720, 720, 40, $white);
    $kind = $p[3];
    if ($kind === 'speaker' || $kind === 'sub' || $kind === 'tweet') {
        imagefilledellipse($im, 400, 340, 340, 340, $accent);
        imagefilledellipse($im, 400, 340, 220, 340, imagecolorallocate($im, 18, 18, 20));
        imagefilledellipse($im, 400, 340, 90, 90, imagecolorallocate($im, 225, 6, 0));
        imagefilledellipse($im, 400, 340, 36, 36, $white);
    } elseif ($kind === 'screen') {
        rounded($im, 170, 140, 460, 300, 18, $accent);
        rounded($im, 190, 158, 420, 230, 10, imagecolorallocate($im, 10, 14, 28));
        text($im, $font, 18, 250, 290, $white, 'ANDROID AUTO');
    } elseif ($kind === 'cam') {
        rounded($im, 250, 180, 300, 180, 24, $accent);
        imagefilledellipse($im, 400, 270, 90, 90, imagecolorallocate($im, 10, 10, 12));
        imagefilledellipse($im, 400, 270, 40, 40, $white);
    } elseif ($kind === 'led') {
        imagefilledellipse($im, 400, 300, 160, 160, imagecolorallocate($im, 255, 220, 80));
        rounded($im, 330, 360, 140, 160, 16, $accent);
    } else {
        rounded($im, 240, 180, 320, 280, 28, $accent);
    }
    text($im, $font, 22, 80, 660, $dark, $p[0]);
    text($im, $font, 14, 80, 700, $gray, $p[1]);
    savePng($im, $dir . '/products/p' . ($i + 1) . '.png');
}

echo "OK car assets\n";
