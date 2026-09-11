<?php
declare(strict_types=1);

const SITE_NAME = 'Yılmaz Toptan';
const SITE_PHONE = '0532 783 74 86';
const SITE_EMAIL = 'info@unposbarkod.com';
const BANK_INFO = 'Ödeme: Havale / EFT — sipariş onaylandıktan sonra kargoya verilir. (Online ödeme sonra eklenecek.)';

$scriptDir = str_replace('\\', '/', dirname($_SERVER['SCRIPT_NAME'] ?? '/'));
if ($scriptDir === '/' || $scriptDir === '.' || $scriptDir === '\\') {
    $scriptDir = '';
}
define('BASE_PATH', $scriptDir);
define('ROOT_DIR', __DIR__);

$local = ROOT_DIR . '/config.local.php';
if (is_file($local)) {
    require $local;
}
if (!defined('DATA_DIR')) {
    define('DATA_DIR', ROOT_DIR . '/data');
}
if (!defined('UPLOAD_DIR')) {
    define('UPLOAD_DIR', ROOT_DIR . '/assets/uploads');
}
if (!defined('DB_FILE')) {
    define('DB_FILE', DATA_DIR . '/app.sqlite');
}

if (!is_dir(DATA_DIR)) {
    mkdir(DATA_DIR, 0775, true);
}
if (!is_dir(UPLOAD_DIR)) {
    mkdir(UPLOAD_DIR, 0775, true);
}

function base_url(string $path = ''): string
{
    $path = ltrim($path, '/');
    return BASE_PATH . ($path === '' ? '/' : '/' . $path);
}

function asset(string $path): string
{
    return base_url(ltrim($path, '/'));
}

function json_input(): array
{
    $raw = file_get_contents('php://input') ?: '';
    $data = json_decode($raw, true);
    return is_array($data) ? $data : [];
}

function db(): PDO
{
    static $pdo = null;
    if ($pdo instanceof PDO) {
        return $pdo;
    }
    $needInstall = !file_exists(DB_FILE);
    $pdo = new PDO('sqlite:' . DB_FILE, null, null, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
    ]);
    $pdo->exec('PRAGMA foreign_keys = ON');
    $pdo->exec('PRAGMA journal_mode = WAL');
    if ($needInstall) {
        install_schema($pdo);
        seed_data($pdo);
    }
    return $pdo;
}

function install_schema(PDO $pdo): void
{
    $pdo->exec(<<<'SQL'
CREATE TABLE users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  role TEXT NOT NULL CHECK(role IN ('admin','dealer')),
  name TEXT NOT NULL,
  company TEXT DEFAULT '',
  phone TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  city TEXT DEFAULT '',
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL
);
CREATE TABLE categories (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  slug TEXT NOT NULL UNIQUE,
  sort INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE products (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  barcode TEXT NOT NULL DEFAULT '',
  name TEXT NOT NULL,
  brand TEXT DEFAULT '',
  description TEXT DEFAULT '',
  category_id INTEGER,
  price INTEGER NOT NULL DEFAULT 0,
  stock INTEGER NOT NULL DEFAULT 0,
  image TEXT DEFAULT '',
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  FOREIGN KEY (category_id) REFERENCES categories(id)
);
CREATE TABLE orders (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  dealer_id INTEGER NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending',
  note TEXT DEFAULT '',
  total INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL,
  paid_at TEXT,
  approved_at TEXT,
  FOREIGN KEY (dealer_id) REFERENCES users(id)
);
CREATE TABLE order_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  order_id INTEGER NOT NULL,
  product_id INTEGER,
  name TEXT NOT NULL,
  barcode TEXT DEFAULT '',
  price INTEGER NOT NULL,
  qty INTEGER NOT NULL,
  FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
);
CREATE INDEX idx_products_barcode ON products(barcode);
CREATE INDEX idx_orders_status ON orders(status);
SQL);
}

function seed_data(PDO $pdo): void
{
    $now = date('c');
    $admin = $pdo->prepare('INSERT INTO users (role,name,company,phone,password_hash,city,active,created_at) VALUES (?,?,?,?,?,?,1,?)');
    $admin->execute(['admin', 'Patron', 'Yılmaz Toptan', '05550000000', password_hash('Patron123!', PASSWORD_DEFAULT), 'İstanbul', $now]);
    $admin->execute(['dealer', 'Ahmet Yılmaz', 'Yılmaz Market', '05551234567', password_hash('Bayi123!', PASSWORD_DEFAULT), 'Ankara', $now]);
    $admin->execute(['dealer', 'Mehmet Kaya', 'Kaya Ticaret', '05559876543', password_hash('Bayi123!', PASSWORD_DEFAULT), 'İzmir', $now]);

    $cats = [
        [1, 'Yazarkasa POS', 'yazarkasa-pos', 1],
        [2, 'Para Sayma', 'para-sayma', 2],
        [3, 'Çekmece', 'cekmece', 3],
        [4, 'Okuyucular', 'okuyucular', 4],
        [5, 'Teraziler', 'teraziler', 5],
        [6, 'Dokunmatik Cihaz', 'dokunmatik', 6],
        [7, 'Yazıcılar', 'yazicilar', 7],
    ];
    $insCat = $pdo->prepare('INSERT INTO categories (id,name,slug,sort) VALUES (?,?,?,?)');
    foreach ($cats as $c) {
        $insCat->execute($c);
    }

    $products = [
        ['8690000000012', 'Hugin T300 Yeni Nesil Yazar Kasa POS', 'Hugin', 'Mali onaylı yeni nesil yazar kasa POS. Restoran ve markete uygun, fiş + Adisyon.', 1, 1850000, 14, 'assets/img/products/p1.png'],
        ['8690000000029', 'PAX A930 Android POS', 'PAX', 'Android işletim sistemli el terminali. Temassız, chip ve manyetik okuma.', 1, 1425000, 22, 'assets/img/products/p2.png'],
        ['8690000000036', 'Ingenico Move 5000', 'Ingenico', 'Taşınabilir tahsilat cihazı. 4G / Wi-Fi, uzun pil ömrü.', 1, 1280000, 18, 'assets/img/products/p3.png'],
        ['8690000000043', 'Paygo N910 Android POS', 'Paygo', 'Kompakt Android POS. Kolay kurulum, bayi dostu fiyat.', 1, 980000, 30, 'assets/img/products/p4.png'],
        ['8690000000050', 'Mix 1200 Para Sayma Makinesi', 'Mix', 'TL sayımı, sahte tespit, müşteri ekranı.', 2, 750000, 9, 'assets/img/products/p5.png'],
        ['8690000000067', 'Mix 2100 Karışık Para Sayma', 'Mix', 'Karışık kupür sayımı, değer hesaplama, sahte alarmı.', 2, 1120000, 6, 'assets/img/products/p6.png'],
        ['8690000000074', 'Honeywell 1450g 2D Barkod Okuyucu', 'Honeywell', 'Kablolu 2D okuyucu. Ekran barkodu ve kâğıt etiket.', 4, 185000, 40, 'assets/img/products/p7.png'],
        ['8690000000081', 'Zebra DS2208 2D Okuyucu', 'Zebra', 'Hızlı 2D tarayıcı. Kasa ve depo için dayanıklı gövde.', 4, 210000, 25, 'assets/img/products/p8.png'],
        ['8690000000098', '80mm Termal Fiş Yazıcı', 'UNPOS', 'USB + Ethernet termal yazıcı. POS ve adisyon uyumlu.', 7, 145000, 35, 'assets/img/products/p9.png'],
        ['8690000000104', 'Metal Nakit Çekmecesi 5 Göz', 'UNPOS', 'RJ11 bağlı metal kasa çekmecesi. 5 banknot + 8 bozuk para.', 3, 89000, 20, 'assets/img/products/p10.png'],
        ['8690000000111', '15.6" Dokunmatik POS Bilgisayar', 'dbPOS', 'i5 işlemcili dokunmatik all-in-one POS PC.', 6, 1650000, 8, 'assets/img/products/p11.png'],
        ['8690000000128', 'Barkodlu Terazi 30 kg', 'UNPOS', 'Etiket basan barkodlu terazi. Manav, kasap, şarküteri.', 5, 420000, 11, 'assets/img/products/p12.png'],
    ];
    $insP = $pdo->prepare('INSERT INTO products (barcode,name,brand,description,category_id,price,stock,image,active,created_at) VALUES (?,?,?,?,?,?,?,?,1,?)');
    foreach ($products as $p) {
        $p[] = $now;
        $insP->execute($p);
    }

    $pdo->prepare('INSERT INTO orders (dealer_id,status,note,total,created_at) VALUES (2,?,?,?,?)')
        ->execute(['pending', 'Hafta içi teslim lütfen', 1605000, $now]);
    $oid = (int) $pdo->lastInsertId();
    $insI = $pdo->prepare('INSERT INTO order_items (order_id,product_id,name,barcode,price,qty) VALUES (?,?,?,?,?,?)');
    $insI->execute([$oid, 7, 'Honeywell 1450g 2D Barkod Okuyucu', '8690000000074', 185000, 3]);
    $insI->execute([$oid, 9, '80mm Termal Fiş Yazıcı', '8690000000098', 145000, 2]);
    $insI->execute([$oid, 10, 'Metal Nakit Çekmecesi 5 Göz', '8690000000104', 89000, 5]);
}

function start_session(): void
{
    if (session_status() === PHP_SESSION_ACTIVE) {
        return;
    }
    session_set_cookie_params([
        'lifetime' => 60 * 60 * 24 * 30,
        'path' => BASE_PATH === '' ? '/' : BASE_PATH . '/',
        'httponly' => true,
        'samesite' => 'Lax',
    ]);
    session_name('yilmaz_sid');
    session_start();
}

function current_user(): ?array
{
    start_session();
    $id = $_SESSION['uid'] ?? null;
    if (!$id) {
        return null;
    }
    $st = db()->prepare('SELECT id, role, name, company, phone, city, active FROM users WHERE id = ?');
    $st->execute([(int) $id]);
    $u = $st->fetch();
    if (!$u || !(int) $u['active']) {
        return null;
    }
    return $u;
}

function require_user(?string $role = null): array
{
    $u = current_user();
    if (!$u) {
        json_out(['ok' => false, 'error' => 'Giriş yapmalısınız.'], 401);
    }
    if ($role && $u['role'] !== $role) {
        json_out(['ok' => false, 'error' => 'Bu işlem için yetkiniz yok.'], 403);
    }
    return $u;
}

function json_out(array $data, int $code = 200): never
{
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_UNESCAPED_UNICODE);
    exit;
}

function money_try(int $kurus): string
{
    return number_format($kurus / 100, 2, ',', '.') . ' ₺';
}

function digits(string $s): string
{
    return preg_replace('/\D+/', '', $s) ?? '';
}

function product_image_url(string $image): string
{
    if ($image === '') {
        return asset('assets/img/products/p1.png');
    }
    if (str_starts_with($image, 'http')) {
        return $image;
    }
    return asset($image);
}

function save_upload(?array $file): ?string
{
    if (!$file || ($file['error'] ?? UPLOAD_ERR_NO_FILE) === UPLOAD_ERR_NO_FILE) {
        return null;
    }
    if (($file['error'] ?? 0) !== UPLOAD_ERR_OK) {
        json_out(['ok' => false, 'error' => 'Fotoğraf yüklenemedi.'], 400);
    }
    $tmp = $file['tmp_name'];
    $info = @getimagesize($tmp);
    if (!$info) {
        json_out(['ok' => false, 'error' => 'Sadece resim yükleyin.'], 400);
    }
    $ext = match ($info[2]) {
        IMAGETYPE_JPEG => 'jpg',
        IMAGETYPE_PNG => 'png',
        IMAGETYPE_WEBP => 'webp',
        default => null,
    };
    if (!$ext) {
        json_out(['ok' => false, 'error' => 'JPG, PNG veya WEBP kullanın.'], 400);
    }
    $name = 'u' . date('YmdHis') . '_' . bin2hex(random_bytes(4)) . '.' . $ext;
    $dest = UPLOAD_DIR . '/' . $name;
    if (!move_uploaded_file($tmp, $dest)) {
        json_out(['ok' => false, 'error' => 'Dosya kaydedilemedi.'], 500);
    }
    return 'assets/uploads/' . $name;
}

function public_product(array $p): array
{
    return [
        'id' => (int) $p['id'],
        'barcode' => $p['barcode'],
        'name' => $p['name'],
        'brand' => $p['brand'],
        'description' => $p['description'],
        'category_id' => (int) $p['category_id'],
        'price' => (int) $p['price'],
        'price_text' => money_try((int) $p['price']),
        'stock' => (int) $p['stock'],
        'image' => product_image_url((string) $p['image']),
        'active' => (int) $p['active'],
    ];
}
