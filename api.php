<?php
declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

header('Cache-Control: no-store');
start_session();
db();

$action = $_GET['action'] ?? $_POST['action'] ?? '';

try {
    match ($action) {
        'boot' => action_boot(),
        'login' => action_login(),
        'logout' => action_logout(),
        'register' => action_register(),
        'me' => json_out(['ok' => true, 'user' => current_user()]),
        'products' => action_products(),
        'product' => action_product(),
        'product_by_barcode' => action_product_by_barcode(),
        'product_save' => action_product_save(),
        'product_delete' => action_product_delete(),
        'order_create' => action_order_create(),
        'my_orders' => action_my_orders(),
        'admin_orders' => action_admin_orders(),
        'order_status' => action_order_status(),
        'dealers' => action_dealers(),
        'dealer_save' => action_dealer_save(),
        default => json_out(['ok' => false, 'error' => 'Bilinmeyen istek.'], 404),
    };
} catch (Throwable $e) {
    json_out(['ok' => false, 'error' => 'Sunucu hatası: ' . $e->getMessage()], 500);
}

function action_boot(): void
{
    $cats = db()->query('SELECT id, name, slug FROM categories ORDER BY sort, id')->fetchAll();
    $st = db()->query('SELECT * FROM products WHERE active = 1 ORDER BY id DESC');
    $products = array_map('public_product', $st->fetchAll());
    json_out([
        'ok' => true,
        'site' => [
            'name' => SITE_NAME,
            'phone' => SITE_PHONE,
            'email' => SITE_EMAIL,
            'bank' => BANK_INFO,
            'base' => BASE_PATH,
        ],
        'user' => current_user(),
        'categories' => $cats,
        'products' => $products,
    ]);
}

function action_login(): void
{
    $in = $_POST ?: json_input();
    $phone = digits((string) ($in['phone'] ?? ''));
    $pass = (string) ($in['password'] ?? '');
    if (strlen($phone) < 10 || $pass === '') {
        json_out(['ok' => false, 'error' => 'Telefon ve şifre gerekli.'], 400);
    }
    $st = db()->prepare('SELECT * FROM users WHERE phone = ? OR phone = ?');
    $st->execute([$phone, '0' . $phone]);
    $u = $st->fetch();
    if (!$u) {
        $st = db()->prepare('SELECT * FROM users WHERE phone LIKE ?');
        $st->execute(['%' . substr($phone, -10)]);
        $u = $st->fetch();
    }
    if (!$u || !password_verify($pass, $u['password_hash']) || !(int) $u['active']) {
        json_out(['ok' => false, 'error' => 'Telefon veya şifre hatalı.'], 401);
    }
    start_session();
    $_SESSION['uid'] = (int) $u['id'];
    json_out(['ok' => true, 'user' => current_user()]);
}

function action_logout(): void
{
    start_session();
    $_SESSION = [];
    if (ini_get('session.use_cookies')) {
        $p = session_get_cookie_params();
        setcookie(session_name(), '', time() - 42000, $p['path']);
    }
    session_destroy();
    json_out(['ok' => true]);
}

function action_register(): void
{
    $in = $_POST ?: json_input();
    $name = trim((string) ($in['name'] ?? ''));
    $company = trim((string) ($in['company'] ?? ''));
    $phone = digits((string) ($in['phone'] ?? ''));
    $city = trim((string) ($in['city'] ?? ''));
    $pass = (string) ($in['password'] ?? '');
    if ($name === '' || strlen($phone) < 10 || strlen($pass) < 6) {
        json_out(['ok' => false, 'error' => 'Ad, telefon ve en az 6 karakter şifre gerekli.'], 400);
    }
    $st = db()->prepare('SELECT id FROM users WHERE phone = ? OR phone LIKE ?');
    $st->execute([$phone, '%' . substr($phone, -10)]);
    if ($st->fetch()) {
        json_out(['ok' => false, 'error' => 'Bu telefon zaten kayıtlı.'], 400);
    }
    db()->prepare('INSERT INTO users (role,name,company,phone,password_hash,city,active,created_at) VALUES (?,?,?,?,?,?,1,?)')
        ->execute(['dealer', $name, $company, $phone, password_hash($pass, PASSWORD_DEFAULT), $city, date('c')]);
    start_session();
    $_SESSION['uid'] = (int) db()->lastInsertId();
    json_out(['ok' => true, 'user' => current_user()]);
}

function action_products(): void
{
    $q = trim((string) ($_GET['q'] ?? ''));
    $cat = (int) ($_GET['category_id'] ?? 0);
    $user = current_user();
    $admin = ($user['role'] ?? '') === 'admin';
    $sql = 'SELECT * FROM products WHERE 1=1';
    $args = [];
    if (!$admin) {
        $sql .= ' AND active = 1';
    }
    if ($cat) {
        $sql .= ' AND category_id = ?';
        $args[] = $cat;
    }
    if ($q !== '') {
        $sql .= ' AND (name LIKE ? OR brand LIKE ? OR barcode LIKE ?)';
        $like = '%' . $q . '%';
        $args[] = $like;
        $args[] = $like;
        $args[] = '%' . digits($q) . '%';
    }
    $sql .= ' ORDER BY id DESC';
    $st = db()->prepare($sql);
    $st->execute($args);
    json_out(['ok' => true, 'products' => array_map('public_product', $st->fetchAll())]);
}

function action_product(): void
{
    $id = (int) ($_GET['id'] ?? 0);
    $st = db()->prepare('SELECT * FROM products WHERE id = ?');
    $st->execute([$id]);
    $p = $st->fetch();
    if (!$p) {
        json_out(['ok' => false, 'error' => 'Ürün bulunamadı.'], 404);
    }
    $p['category_name'] = '';
    if ($p['category_id']) {
        $c = db()->prepare('SELECT name FROM categories WHERE id = ?');
        $c->execute([$p['category_id']]);
        $p['category_name'] = (string) ($c->fetchColumn() ?: '');
    }
    json_out(['ok' => true, 'product' => public_product($p) + ['category_name' => $p['category_name'], 'description' => $p['description']]]);
}

function action_product_by_barcode(): void
{
    require_user('admin');
    $code = digits((string) ($_GET['barcode'] ?? ''));
    if ($code === '') {
        json_out(['ok' => false, 'error' => 'Barkod yok.'], 400);
    }
    $st = db()->prepare('SELECT * FROM products WHERE barcode = ? OR barcode LIKE ?');
    $st->execute([$code, '%' . $code]);
    $p = $st->fetch();
    json_out(['ok' => true, 'product' => $p ? public_product($p) : null]);
}

function action_product_save(): void
{
    require_user('admin');
    $id = (int) ($_POST['id'] ?? 0);
    $name = trim((string) ($_POST['name'] ?? ''));
    $barcode = digits((string) ($_POST['barcode'] ?? ''));
    $brand = trim((string) ($_POST['brand'] ?? ''));
    $description = trim((string) ($_POST['description'] ?? ''));
    $categoryId = (int) ($_POST['category_id'] ?? 0) ?: null;
    $raw = trim((string) ($_POST['price'] ?? '0'));
    if (str_contains($raw, ',') && str_contains($raw, '.')) {
        $raw = str_replace('.', '', $raw);
        $raw = str_replace(',', '.', $raw);
    } elseif (str_contains($raw, ',')) {
        $raw = str_replace(',', '.', $raw);
    }
    $price = (int) round(((float) $raw) * 100);
    if (isset($_POST['price_kurus'])) {
        $price = (int) $_POST['price_kurus'];
    }
    $stock = (int) ($_POST['stock'] ?? 0);
    $active = isset($_POST['active']) ? (int) $_POST['active'] : 1;
    if ($name === '') {
        json_out(['ok' => false, 'error' => 'Ürün adı yazın.'], 400);
    }
    $image = save_upload($_FILES['image'] ?? null);
    if ($id) {
        $st = db()->prepare('SELECT image FROM products WHERE id = ?');
        $st->execute([$id]);
        $old = $st->fetch();
        if (!$old) {
            json_out(['ok' => false, 'error' => 'Ürün yok.'], 404);
        }
        $img = $image ?: $old['image'];
        db()->prepare('UPDATE products SET barcode=?, name=?, brand=?, description=?, category_id=?, price=?, stock=?, image=?, active=? WHERE id=?')
            ->execute([$barcode, $name, $brand, $description, $categoryId, $price, $stock, $img, $active, $id]);
    } else {
        db()->prepare('INSERT INTO products (barcode,name,brand,description,category_id,price,stock,image,active,created_at) VALUES (?,?,?,?,?,?,?,?,?,?)')
            ->execute([$barcode, $name, $brand, $description, $categoryId, $price, $stock, $image ?? '', $active, date('c')]);
        $id = (int) db()->lastInsertId();
    }
    $st = db()->prepare('SELECT * FROM products WHERE id = ?');
    $st->execute([$id]);
    json_out(['ok' => true, 'product' => public_product($st->fetch())]);
}

function action_product_delete(): void
{
    require_user('admin');
    $id = (int) ($_POST['id'] ?? 0);
    db()->prepare('UPDATE products SET active = 0 WHERE id = ?')->execute([$id]);
    json_out(['ok' => true]);
}

function action_order_create(): void
{
    $u = require_user('dealer');
    $in = $_POST ?: json_input();
    $items = $in['items'] ?? [];
    $note = trim((string) ($in['note'] ?? ''));
    if (!is_array($items) || !$items) {
        json_out(['ok' => false, 'error' => 'Sepet boş.'], 400);
    }
    $pdo = db();
    $pdo->beginTransaction();
    $total = 0;
    $rows = [];
    $pst = $pdo->prepare('SELECT * FROM products WHERE id = ? AND active = 1');
    foreach ($items as $it) {
        $pid = (int) ($it['id'] ?? 0);
        $qty = max(1, (int) ($it['qty'] ?? 1));
        $pst->execute([$pid]);
        $p = $pst->fetch();
        if (!$p) {
            $pdo->rollBack();
            json_out(['ok' => false, 'error' => 'Ürün bulunamadı.'], 400);
        }
        $total += ((int) $p['price']) * $qty;
        $rows[] = [$p, $qty];
    }
    $pdo->prepare('INSERT INTO orders (dealer_id,status,note,total,created_at) VALUES (?,?,?,?,?)')
        ->execute([(int) $u['id'], 'pending', $note, $total, date('c')]);
    $oid = (int) $pdo->lastInsertId();
    $ins = $pdo->prepare('INSERT INTO order_items (order_id,product_id,name,barcode,price,qty) VALUES (?,?,?,?,?,?)');
    foreach ($rows as [$p, $qty]) {
        $ins->execute([$oid, $p['id'], $p['name'], $p['barcode'], $p['price'], $qty]);
    }
    $pdo->commit();
    json_out(['ok' => true, 'order_id' => $oid, 'total_text' => money_try($total), 'message' => 'Siparişiniz alındı. Ödeme sonrası onaylanacak.']);
}

function map_order(array $o, array $items): array
{
    $labels = [
        'pending' => 'Ödeme bekleniyor',
        'paid' => 'Ödeme alındı',
        'approved' => 'Onaylandı',
        'shipped' => 'Kargoda',
        'cancelled' => 'İptal',
    ];
    return [
        'id' => (int) $o['id'],
        'status' => $o['status'],
        'status_text' => $labels[$o['status']] ?? $o['status'],
        'note' => $o['note'],
        'total' => (int) $o['total'],
        'total_text' => money_try((int) $o['total']),
        'created_at' => $o['created_at'],
        'dealer_name' => $o['dealer_name'] ?? '',
        'dealer_company' => $o['dealer_company'] ?? '',
        'dealer_phone' => $o['dealer_phone'] ?? '',
        'items' => array_map(static function ($i) {
            return [
                'name' => $i['name'],
                'barcode' => $i['barcode'],
                'qty' => (int) $i['qty'],
                'price' => (int) $i['price'],
                'price_text' => money_try((int) $i['price']),
                'line_text' => money_try(((int) $i['price']) * (int) $i['qty']),
            ];
        }, $items),
    ];
}

function order_items(int $orderId): array
{
    $st = db()->prepare('SELECT * FROM order_items WHERE order_id = ?');
    $st->execute([$orderId]);
    return $st->fetchAll();
}

function action_my_orders(): void
{
    $u = require_user('dealer');
    $st = db()->prepare('SELECT o.*, u.name AS dealer_name, u.company AS dealer_company, u.phone AS dealer_phone FROM orders o JOIN users u ON u.id = o.dealer_id WHERE o.dealer_id = ? ORDER BY o.id DESC');
    $st->execute([(int) $u['id']]);
    $list = [];
    foreach ($st as $o) {
        $list[] = map_order($o, order_items((int) $o['id']));
    }
    json_out(['ok' => true, 'orders' => $list]);
}

function action_admin_orders(): void
{
    require_user('admin');
    $status = trim((string) ($_GET['status'] ?? ''));
    $sql = 'SELECT o.*, u.name AS dealer_name, u.company AS dealer_company, u.phone AS dealer_phone FROM orders o JOIN users u ON u.id = o.dealer_id';
    $args = [];
    if ($status !== '') {
        $sql .= ' WHERE o.status = ?';
        $args[] = $status;
    }
    $sql .= ' ORDER BY o.id DESC';
    $st = db()->prepare($sql);
    $st->execute($args);
    $list = [];
    foreach ($st as $o) {
        $list[] = map_order($o, order_items((int) $o['id']));
    }
    json_out(['ok' => true, 'orders' => $list]);
}

function action_order_status(): void
{
    require_user('admin');
    $in = $_POST ?: json_input();
    $id = (int) ($in['id'] ?? 0);
    $status = (string) ($in['status'] ?? '');
    $allowed = ['pending', 'paid', 'approved', 'shipped', 'cancelled'];
    if (!in_array($status, $allowed, true)) {
        json_out(['ok' => false, 'error' => 'Geçersiz durum.'], 400);
    }
    $paid = $status === 'paid' || $status === 'approved' ? date('c') : null;
    $appr = $status === 'approved' ? date('c') : null;
    db()->prepare('UPDATE orders SET status=?, paid_at=COALESCE(?, paid_at), approved_at=COALESCE(?, approved_at) WHERE id=?')
        ->execute([$status, $paid, $appr, $id]);
    json_out(['ok' => true]);
}

function action_dealers(): void
{
    require_user('admin');
    $rows = db()->query("SELECT id, name, company, phone, city, active, created_at FROM users WHERE role = 'dealer' ORDER BY id DESC")->fetchAll();
    json_out(['ok' => true, 'dealers' => $rows]);
}

function action_dealer_save(): void
{
    require_user('admin');
    $in = $_POST ?: json_input();
    $id = (int) ($in['id'] ?? 0);
    $name = trim((string) ($in['name'] ?? ''));
    $company = trim((string) ($in['company'] ?? ''));
    $phone = digits((string) ($in['phone'] ?? ''));
    $city = trim((string) ($in['city'] ?? ''));
    $pass = (string) ($in['password'] ?? '');
    $active = isset($in['active']) ? (int) $in['active'] : 1;
    if ($name === '' || strlen($phone) < 10) {
        json_out(['ok' => false, 'error' => 'Ad ve telefon gerekli.'], 400);
    }
    if ($id) {
        if ($pass !== '') {
            db()->prepare('UPDATE users SET name=?, company=?, phone=?, city=?, active=?, password_hash=? WHERE id=? AND role="dealer"')
                ->execute([$name, $company, $phone, $city, $active, password_hash($pass, PASSWORD_DEFAULT), $id]);
        } else {
            db()->prepare('UPDATE users SET name=?, company=?, phone=?, city=?, active=? WHERE id=? AND role="dealer"')
                ->execute([$name, $company, $phone, $city, $active, $id]);
        }
    } else {
        if (strlen($pass) < 6) {
            json_out(['ok' => false, 'error' => 'Yeni bayi için şifre yazın.'], 400);
        }
        db()->prepare('INSERT INTO users (role,name,company,phone,password_hash,city,active,created_at) VALUES ("dealer",?,?,?,?,?,?,?)')
            ->execute([$name, $company, $phone, password_hash($pass, PASSWORD_DEFAULT), $city, $active, date('c')]);
    }
    json_out(['ok' => true]);
}
