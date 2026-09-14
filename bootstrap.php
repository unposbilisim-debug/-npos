<?php
declare(strict_types=1);

const SITE_NAME = 'ENS Oto Market';
const SITE_PHONE = '+90 531 351 21 11';
const SITE_EMAIL = 'info@yilmazelektronik.com';
const BANK_INFO = 'Ödeme: Havale / EFT — sipariş onaylandıktan sonra kargoya verilir. (Online ödeme sonra eklenecek.)';
const CATALOG_VERSION = 2;

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
    } else {
        maybe_upgrade_catalog($pdo);
        maybe_upgrade_pricing($pdo);
        maybe_upgrade_accounting($pdo);
    }
    if ($needInstall) {
        maybe_upgrade_accounting($pdo);
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
  approved INTEGER NOT NULL DEFAULT 1,
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
  list_price INTEGER NOT NULL DEFAULT 0,
  stock INTEGER NOT NULL DEFAULT 0,
  cost INTEGER NOT NULL DEFAULT 0,
  min_stock INTEGER NOT NULL DEFAULT 3,
  image TEXT DEFAULT '',
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  FOREIGN KEY (category_id) REFERENCES categories(id)
);
CREATE TABLE ledger (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  dealer_id INTEGER NOT NULL,
  kind TEXT NOT NULL,
  amount INTEGER NOT NULL,
  method TEXT DEFAULT '',
  ref_type TEXT DEFAULT '',
  ref_id INTEGER DEFAULT 0,
  note TEXT DEFAULT '',
  created_at TEXT NOT NULL,
  FOREIGN KEY (dealer_id) REFERENCES users(id)
);
CREATE TABLE stock_moves (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  kind TEXT NOT NULL,
  qty INTEGER NOT NULL,
  unit_cost INTEGER NOT NULL DEFAULT 0,
  ref_type TEXT DEFAULT '',
  ref_id INTEGER DEFAULT 0,
  note TEXT DEFAULT '',
  created_at TEXT NOT NULL,
  FOREIGN KEY (product_id) REFERENCES products(id)
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
CREATE INDEX idx_ledger_dealer ON ledger(dealer_id, created_at);
CREATE INDEX idx_stock_product ON stock_moves(product_id, created_at);
SQL);
}

function seed_data(PDO $pdo): void
{
    $now = date('c');
    $admin = $pdo->prepare('INSERT INTO users (role,name,company,phone,password_hash,city,active,approved,created_at) VALUES (?,?,?,?,?,?,1,1,?)');
    $admin->execute(['admin', 'Patron', 'Yılmaz Elektronik', '05550000000', password_hash('Patron123!', PASSWORD_DEFAULT), 'İstanbul', $now]);
    $admin->execute(['dealer', 'Ahmet Yılmaz', 'Yılmaz Oto Ses', '05551234567', password_hash('Bayi123!', PASSWORD_DEFAULT), 'Ankara', $now]);
    $admin->execute(['dealer', 'Mehmet Kaya', 'Kaya Oto Aksesuar', '05559876543', password_hash('Bayi123!', PASSWORD_DEFAULT), 'İzmir', $now]);
    seed_catalog($pdo, $now);
}

function maybe_upgrade_catalog(PDO $pdo): void
{
    $slug = (string) ($pdo->query('SELECT slug FROM categories WHERE id = 1')->fetchColumn() ?: '');
    if ($slug === 'arac-ses') {
        return;
    }
    $pdo->exec('DELETE FROM order_items');
    $pdo->exec('DELETE FROM orders');
    $pdo->exec('DELETE FROM products');
    $pdo->exec('DELETE FROM categories');
    $pdo->prepare('UPDATE users SET company = ? WHERE role = ?')->execute(['Yılmaz Elektronik', 'admin']);
    seed_catalog($pdo, date('c'));
}

function table_has_column(PDO $pdo, string $table, string $column): bool
{
    $st = $pdo->query('PRAGMA table_info(' . $table . ')');
    foreach ($st as $row) {
        if (strcasecmp((string) $row['name'], $column) === 0) {
            return true;
        }
    }
    return false;
}

function maybe_upgrade_pricing(PDO $pdo): void
{
    if (!table_has_column($pdo, 'products', 'list_price')) {
        $pdo->exec('ALTER TABLE products ADD COLUMN list_price INTEGER NOT NULL DEFAULT 0');
        $pdo->exec('UPDATE products SET list_price = CAST(ROUND(price * 1.2) AS INTEGER) WHERE list_price = 0');
    }
    if (!table_has_column($pdo, 'users', 'approved')) {
        $pdo->exec('ALTER TABLE users ADD COLUMN approved INTEGER NOT NULL DEFAULT 1');
    }
    $pdo->exec('UPDATE products SET list_price = CAST(ROUND(price * 1.2) AS INTEGER) WHERE list_price = 0 AND price > 0');
}

function maybe_upgrade_accounting(PDO $pdo): void
{
    if (!table_has_column($pdo, 'products', 'cost')) {
        $pdo->exec('ALTER TABLE products ADD COLUMN cost INTEGER NOT NULL DEFAULT 0');
    }
    if (!table_has_column($pdo, 'products', 'min_stock')) {
        $pdo->exec('ALTER TABLE products ADD COLUMN min_stock INTEGER NOT NULL DEFAULT 3');
    }
    $pdo->exec(<<<'SQL'
CREATE TABLE IF NOT EXISTS ledger (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  dealer_id INTEGER NOT NULL,
  kind TEXT NOT NULL,
  amount INTEGER NOT NULL,
  method TEXT DEFAULT '',
  ref_type TEXT DEFAULT '',
  ref_id INTEGER DEFAULT 0,
  note TEXT DEFAULT '',
  created_at TEXT NOT NULL,
  FOREIGN KEY (dealer_id) REFERENCES users(id)
);
CREATE TABLE IF NOT EXISTS stock_moves (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  kind TEXT NOT NULL,
  qty INTEGER NOT NULL,
  unit_cost INTEGER NOT NULL DEFAULT 0,
  ref_type TEXT DEFAULT '',
  ref_id INTEGER DEFAULT 0,
  note TEXT DEFAULT '',
  created_at TEXT NOT NULL,
  FOREIGN KEY (product_id) REFERENCES products(id)
);
CREATE INDEX IF NOT EXISTS idx_ledger_dealer ON ledger(dealer_id, created_at);
CREATE INDEX IF NOT EXISTS idx_stock_product ON stock_moves(product_id, created_at);
SQL);
    $hasLedger = (int) $pdo->query('SELECT COUNT(*) FROM ledger')->fetchColumn();
    if ($hasLedger === 0) {
        $ins = $pdo->prepare('INSERT INTO ledger (dealer_id,kind,amount,method,ref_type,ref_id,note,created_at) VALUES (?,?,?,?,?,?,?,?)');
        foreach ($pdo->query('SELECT * FROM orders') as $o) {
            if (($o['status'] ?? '') === 'cancelled') {
                continue;
            }
            $oid = (int) $o['id'];
            $ins->execute([(int) $o['dealer_id'], 'sale', (int) $o['total'], 'acik_hesap', 'order', $oid, 'Sipariş #' . $oid, $o['created_at']]);
            if (in_array($o['status'], ['paid', 'approved', 'shipped'], true)) {
                $when = $o['paid_at'] ?: ($o['approved_at'] ?: $o['created_at']);
                $ins->execute([(int) $o['dealer_id'], 'payment', -((int) $o['total']), 'havale', 'order', $oid, 'Sipariş tahsilat #' . $oid, $when]);
            }
        }
    }
    $hasMoves = (int) $pdo->query('SELECT COUNT(*) FROM stock_moves')->fetchColumn();
    if ($hasMoves === 0) {
        $ins = $pdo->prepare('INSERT INTO stock_moves (product_id,kind,qty,unit_cost,note,created_at) VALUES (?,?,?,?,?,?)');
        foreach ($pdo->query('SELECT id, stock, cost FROM products') as $p) {
            $ins->execute([(int) $p['id'], 'opening', (int) $p['stock'], (int) ($p['cost'] ?? 0), 'Mevcut stok', date('c')]);
        }
    }
}

function seed_catalog(PDO $pdo, string $now): void
{
    $cats = [
        [1, 'Araç Ses Sistemleri', 'arac-ses', 1],
        [2, 'Multimedya Sistemleri', 'multimedya', 2],
        [3, 'Oto Güvenlik Sistemleri', 'oto-guvenlik', 3],
        [4, 'Aydınlatma Ekipmanları', 'aydinlatma', 4],
        [5, 'Oto Aksesuar ve Yedek Parça', 'aksesuar', 5],
        [6, 'Mobil Yaşam ve Araç Aksesuar', 'mobil-yasam', 6],
        [7, 'Adaptör ve Soketler', 'adaptor', 7],
    ];
    $insCat = $pdo->prepare('INSERT INTO categories (id,name,slug,sort) VALUES (?,?,?,?)');
    foreach ($cats as $c) {
        $insCat->execute($c);
    }

    $products = [
        ['8691110000014', 'Pioneer TS-A1670F 16cm Hoparlör', 'Pioneer', '3 yollu 16 cm hoparlör. Kapı ve raf montajı, yüksek bas.', 1, 125000, 40, 'assets/img/products/p1.png'],
        ['8691110000021', 'JBL Stage3 627 16cm Hoparlör Set', 'JBL', 'Koaksiyel hoparlör seti. Net tiz, güçlü mid.', 1, 98000, 32, 'assets/img/products/p2.png'],
        ['8691110000038', 'Pioneer GM-A3702 2x170W Amfi', 'Pioneer', '2 kanal araç amfisi. Sub veya hoparlör sürmeye uygun.', 1, 245000, 18, 'assets/img/products/p3.png'],
        ['8691110000045', 'Alpine SWR-8D2 20cm Subwoofer', 'Alpine', '20 cm çift bobin subwoofer. Bagaj kutu uyumlu.', 1, 320000, 12, 'assets/img/products/p4.png'],
        ['8691110000052', 'Pioneer AVH-Z3200DAB 2DIN Teyp', 'Pioneer', 'Apple CarPlay / Android Auto, Bluetooth, USB.', 2, 890000, 9, 'assets/img/products/p5.png'],
        ['8691110000069', 'Android 10" Teyp 2+32 GB', 'Yılmaz', '10 inç dokunmatik Android teyp. GPS, ayna yansıtma.', 2, 425000, 22, 'assets/img/products/p6.png'],
        ['8691110000076', '9" Android Tablet Ekran', 'Yılmaz', '9 inç 2GB/32GB Android ekran. Kamera girişi hazır.', 2, 380000, 16, 'assets/img/products/p7.png'],
        ['8691110000083', '1080P Ön + Arka Araç Kamerası', 'Yılmaz', 'Gündüz/gece kayıt, park modu, 32GB kart uyumlu.', 3, 145000, 28, 'assets/img/products/p8.png'],
        ['8691110000090', 'LED’li Geri Görüş Kamerası', 'Yılmaz', 'Plaka altı montaj, gece görüş LED, RCA çıkış.', 3, 45000, 50, 'assets/img/products/p9.png'],
        ['8691110000106', '4’lü Park Sensörü Seti', 'Yılmaz', 'Sesli uyarı, ekranlı, çoğu araca uyumlu.', 3, 35000, 40, 'assets/img/products/p10.png'],
        ['8691110000113', 'H7 LED Far Set 6000K', 'Yılmaz', 'Beyaz LED far. Fan soğutmalı, 12V.', 4, 89000, 35, 'assets/img/products/p11.png'],
        ['8691110000120', 'T10 LED İç Aydınlatma (2’li)', 'Yılmaz', 'Tavan ve plaka LED ampul. Canbus uyumlu.', 4, 15000, 80, 'assets/img/products/p12.png'],
        ['8691110000137', '12V USB Araç Şarj 3.1A', 'Yılmaz', 'Çift USB hızlı şarj. 12/24V.', 5, 8500, 100, 'assets/img/products/p13.png'],
        ['8691110000144', 'ISO Soket Adaptörü', 'Yılmaz', 'Teyp montaj ISO fiş / araç soketi.', 7, 6500, 90, 'assets/img/products/p14.png'],
        ['8691110000151', 'Araç İçi Telefon Tutucu', 'Yılmaz', 'Havalandırma ızgarası telefon tutucu.', 6, 12000, 70, 'assets/img/products/p15.png'],
        ['8691110000168', 'Tweeter Set 300W', 'Pioneer', 'Harici tiz hoparlör. A sütunu montaj.', 1, 75000, 24, 'assets/img/products/p16.png'],
    ];
    $insP = $pdo->prepare('INSERT INTO products (barcode,name,brand,description,category_id,price,list_price,stock,image,active,created_at) VALUES (?,?,?,?,?,?,?,?,?,1,?)');
    foreach ($products as $p) {
        $dealer = (int) $p[5];
        $list = (int) round($dealer * 1.2);
        $row = [$p[0], $p[1], $p[2], $p[3], $p[4], $dealer, $list, $p[6], $p[7], $now];
        $insP->execute($row);
    }

    $pdo->prepare('INSERT INTO orders (dealer_id,status,note,total,created_at) VALUES (2,?,?,?,?)')
        ->execute(['pending', 'Bu hafta kargo lütfen', 395000, $now]);
    $oid = (int) $pdo->lastInsertId();
    $insI = $pdo->prepare('INSERT INTO order_items (order_id,product_id,name,barcode,price,qty) VALUES (?,?,?,?,?,?)');
    $insI->execute([$oid, 1, 'Pioneer TS-A1670F 16cm Hoparlör', '8691110000014', 125000, 2]);
    $insI->execute([$oid, 8, '1080P Ön + Arka Araç Kamerası', '8691110000083', 145000, 1]);
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
    $st = db()->prepare('SELECT id, role, name, company, phone, city, active, approved FROM users WHERE id = ?');
    $st->execute([(int) $id]);
    $u = $st->fetch();
    if (!$u || !(int) $u['active']) {
        return null;
    }
    $u['id'] = (int) $u['id'];
    $u['active'] = (int) $u['active'];
    $u['approved'] = (int) ($u['approved'] ?? 1);
    return $u;
}

function is_approved_dealer(?array $user): bool
{
    return $user
        && ($user['role'] ?? '') === 'dealer'
        && (int) ($user['approved'] ?? 0) === 1;
}

function parse_try_to_kurus($value): int
{
    $raw = trim((string) $value);
    if ($raw === '') {
        return 0;
    }
    if (str_contains($raw, ',') && str_contains($raw, '.')) {
        $raw = str_replace('.', '', $raw);
        $raw = str_replace(',', '.', $raw);
    } elseif (str_contains($raw, ',')) {
        $raw = str_replace(',', '.', $raw);
    }
    return (int) round(((float) $raw) * 100);
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

function slugify(string $name): string
{
    $map = [
        'ş' => 's', 'Ş' => 's', 'ı' => 'i', 'İ' => 'i', 'ğ' => 'g', 'Ğ' => 'g',
        'ü' => 'u', 'Ü' => 'u', 'ö' => 'o', 'Ö' => 'o', 'ç' => 'c', 'Ç' => 'c',
    ];
    $s = strtr($name, $map);
    $s = strtolower($s);
    $s = preg_replace('/[^a-z0-9]+/', '-', $s) ?? '';
    $s = trim($s, '-');
    return $s !== '' ? $s : 'kategori';
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

function public_product(array $p, ?array $viewer = null): array
{
    if (func_num_args() < 2) {
        $viewer = current_user();
    }
    $dealerPrice = (int) ($p['price'] ?? 0);
    $listPrice = (int) ($p['list_price'] ?? 0);
    if ($listPrice <= 0) {
        $listPrice = (int) round($dealerPrice * 1.2);
    }
    $isAdmin = ($viewer['role'] ?? '') === 'admin';
    $showDealer = $isAdmin || is_approved_dealer($viewer);
    $display = $showDealer ? $dealerPrice : $listPrice;
    $out = [
        'id' => (int) $p['id'],
        'barcode' => $p['barcode'],
        'name' => $p['name'],
        'brand' => $p['brand'],
        'description' => $p['description'],
        'category_id' => (int) ($p['category_id'] ?? 0),
        'price' => $display,
        'price_text' => money_try($display),
        'list_price' => $listPrice,
        'list_price_text' => money_try($listPrice),
        'is_dealer_price' => $showDealer,
        'stock' => (int) $p['stock'],
        'cost' => (int) ($p['cost'] ?? 0),
        'min_stock' => (int) ($p['min_stock'] ?? 3),
        'image' => product_image_url((string) ($p['image'] ?? '')),
        'active' => (int) ($p['active'] ?? 1),
    ];
    if ($showDealer) {
        $out['dealer_price'] = $dealerPrice;
        $out['dealer_price_text'] = money_try($dealerPrice);
        $out['save'] = max(0, $listPrice - $dealerPrice);
        $out['save_text'] = money_try($out['save']);
    }
    if ($isAdmin) {
        $out['price'] = $dealerPrice;
        $out['price_text'] = money_try($dealerPrice);
    }
    return $out;
}
