<?php
declare(strict_types=1);

function ledger_kind_label(string $kind): string
{
    return match ($kind) {
        'sale' => 'Satış (borç)',
        'payment' => 'Tahsilat',
        'refund' => 'İade / iptal',
        'opening' => 'Açılış',
        'adjust' => 'Düzeltme',
        default => $kind,
    };
}

function pay_method_label(string $method): string
{
    return match ($method) {
        'nakit' => 'Nakit',
        'havale' => 'Havale / EFT',
        'kart' => 'Kart',
        'cek' => 'Çek',
        'acik_hesap' => 'Açık hesap',
        default => $method !== '' ? $method : '—',
    };
}

function stock_kind_label(string $kind): string
{
    return match ($kind) {
        'opening' => 'Açılış',
        'in' => 'Stok giriş',
        'sale' => 'Satış çıkış',
        'return' => 'İade giriş',
        'adjust' => 'Sayım / düzeltme',
        default => $kind,
    };
}

function ledger_has(PDO $pdo, string $refType, int $refId, string $kind): bool
{
    if ($refId <= 0) {
        return false;
    }
    $st = $pdo->prepare('SELECT id FROM ledger WHERE ref_type = ? AND ref_id = ? AND kind = ? LIMIT 1');
    $st->execute([$refType, $refId, $kind]);
    return (bool) $st->fetch();
}

function ledger_post(
    PDO $pdo,
    int $dealerId,
    string $kind,
    int $amount,
    string $method = '',
    string $refType = '',
    int $refId = 0,
    string $note = '',
    ?string $at = null
): int {
    if ($amount === 0) {
        return 0;
    }
    if ($refId > 0 && ledger_has($pdo, $refType, $refId, $kind)) {
        return 0;
    }
    $pdo->prepare('INSERT INTO ledger (dealer_id,kind,amount,method,ref_type,ref_id,note,created_at) VALUES (?,?,?,?,?,?,?,?)')
        ->execute([$dealerId, $kind, $amount, $method, $refType, $refId, $note, $at ?: date('c')]);
    return (int) $pdo->lastInsertId();
}

function dealer_balance(PDO $pdo, int $dealerId): int
{
    $st = $pdo->prepare('SELECT COALESCE(SUM(amount), 0) FROM ledger WHERE dealer_id = ?');
    $st->execute([$dealerId]);
    return (int) $st->fetchColumn();
}

function map_ledger(array $row, int &$running = 0): array
{
    $amount = (int) $row['amount'];
    $running += $amount;
    return [
        'id' => (int) $row['id'],
        'dealer_id' => (int) $row['dealer_id'],
        'kind' => $row['kind'],
        'kind_text' => ledger_kind_label((string) $row['kind']),
        'amount' => $amount,
        'amount_text' => money_try(abs($amount)),
        'signed_text' => ($amount > 0 ? '+' : ($amount < 0 ? '−' : '')) . money_try(abs($amount)),
        'is_debit' => $amount > 0,
        'method' => $row['method'],
        'method_text' => pay_method_label((string) $row['method']),
        'ref_type' => $row['ref_type'],
        'ref_id' => (int) $row['ref_id'],
        'note' => $row['note'],
        'created_at' => $row['created_at'],
        'balance' => $running,
        'balance_text' => money_try(abs($running)),
        'balance_sign' => $running > 0 ? 'borc' : ($running < 0 ? 'alacak' : 'sifir'),
    ];
}

function stock_apply(
    PDO $pdo,
    int $productId,
    int $qtyDelta,
    string $kind,
    string $refType = '',
    int $refId = 0,
    string $note = '',
    int $unitCost = 0
): void {
    $st = $pdo->prepare('SELECT id, name, stock, cost FROM products WHERE id = ?');
    $st->execute([$productId]);
    $p = $st->fetch();
    if (!$p) {
        throw new RuntimeException('Ürün bulunamadı.');
    }
    $new = (int) $p['stock'] + $qtyDelta;
    if ($new < 0) {
        throw new RuntimeException('Stok yetersiz: ' . $p['name'] . ' (kalan ' . (int) $p['stock'] . ')');
    }
    $cost = $unitCost > 0 ? $unitCost : (int) $p['cost'];
    if ($unitCost > 0 && $qtyDelta > 0) {
        $pdo->prepare('UPDATE products SET stock = ?, cost = ? WHERE id = ?')->execute([$new, $unitCost, $productId]);
    } else {
        $pdo->prepare('UPDATE products SET stock = ? WHERE id = ?')->execute([$new, $productId]);
    }
    $pdo->prepare('INSERT INTO stock_moves (product_id,kind,qty,unit_cost,ref_type,ref_id,note,created_at) VALUES (?,?,?,?,?,?,?,?)')
        ->execute([$productId, $kind, $qtyDelta, $cost, $refType, $refId, $note, date('c')]);
}

function order_has_stock_move(PDO $pdo, int $orderId): bool
{
    $st = $pdo->prepare("SELECT id FROM stock_moves WHERE ref_type = 'order' AND ref_id = ? LIMIT 1");
    $st->execute([$orderId]);
    return (bool) $st->fetch();
}

function post_order_sale(PDO $pdo, array $order, array $items, bool $deductStock): void
{
    $oid = (int) $order['id'];
    ledger_post(
        $pdo,
        (int) $order['dealer_id'],
        'sale',
        (int) $order['total'],
        'acik_hesap',
        'order',
        $oid,
        'Sipariş #' . $oid,
        (string) $order['created_at']
    );
    if (!$deductStock) {
        return;
    }
    foreach ($items as $it) {
        $pid = (int) ($it['product_id'] ?? 0);
        $qty = (int) ($it['qty'] ?? 0);
        if ($pid > 0 && $qty > 0) {
            stock_apply($pdo, $pid, -$qty, 'sale', 'order', $oid, 'Sipariş #' . $oid);
        }
    }
}

function reverse_order_sale(PDO $pdo, array $order, array $items): void
{
    $oid = (int) $order['id'];
    if (ledger_has($pdo, 'order', $oid, 'sale') && !ledger_has($pdo, 'order', $oid, 'refund')) {
        ledger_post(
            $pdo,
            (int) $order['dealer_id'],
            'refund',
            -((int) $order['total']),
            '',
            'order',
            $oid,
            'Sipariş iptal #' . $oid
        );
    }
    if (ledger_has($pdo, 'order', $oid, 'payment')) {
        ledger_post(
            $pdo,
            (int) $order['dealer_id'],
            'adjust',
            (int) $order['total'],
            '',
            'order',
            $oid,
            'İptal edilen tahsilat iadesi #' . $oid
        );
    }
    if (order_has_stock_move($pdo, $oid)) {
        foreach ($items as $it) {
            $pid = (int) ($it['product_id'] ?? 0);
            $qty = (int) ($it['qty'] ?? 0);
            if ($pid > 0 && $qty > 0) {
                stock_apply($pdo, $pid, $qty, 'return', 'order', $oid, 'Sipariş iptal #' . $oid);
            }
        }
    }
}

function post_order_payment(PDO $pdo, array $order, string $method = 'havale'): void
{
    $oid = (int) $order['id'];
    ledger_post(
        $pdo,
        (int) $order['dealer_id'],
        'payment',
        -((int) $order['total']),
        $method,
        'order',
        $oid,
        'Sipariş tahsilat #' . $oid
    );
}

function action_admin_summary(): void
{
    require_user('admin');
    $pdo = db();
    $monthStart = date('Y-m-01');
    $pending = (int) $pdo->query("SELECT COUNT(*) FROM orders WHERE status = 'pending'")->fetchColumn();
    $openOrders = (int) $pdo->query("SELECT COUNT(*) FROM orders WHERE status NOT IN ('cancelled','shipped')")->fetchColumn();
    $ms = $pdo->prepare("SELECT COALESCE(SUM(amount),0) FROM ledger WHERE kind = 'sale' AND date(created_at) >= date(?)");
    $ms->execute([$monthStart]);
    $monthSales = (int) $ms->fetchColumn();
    $mp = $pdo->prepare("SELECT COALESCE(SUM(-amount),0) FROM ledger WHERE kind = 'payment' AND date(created_at) >= date(?)");
    $mp->execute([$monthStart]);
    $monthPay = (int) $mp->fetchColumn();
    $openAr = (int) $pdo->query('SELECT COALESCE(SUM(b),0) FROM (SELECT SUM(amount) AS b FROM ledger GROUP BY dealer_id HAVING b > 0) t')->fetchColumn();
    $stockVal = (int) $pdo->query('SELECT COALESCE(SUM(stock * CASE WHEN cost > 0 THEN cost ELSE price END), 0) FROM products WHERE active = 1')->fetchColumn();
    $low = $pdo->query('SELECT id, name, stock, min_stock, barcode FROM products WHERE active = 1 AND stock <= min_stock ORDER BY stock ASC, name LIMIT 8')->fetchAll();
    $debtors = $pdo->query("SELECT u.id, u.name, u.company, u.phone, SUM(l.amount) AS balance FROM ledger l JOIN users u ON u.id = l.dealer_id GROUP BY u.id HAVING balance > 0 ORDER BY balance DESC LIMIT 8")->fetchAll();
    $recent = $pdo->query('SELECT l.*, u.company AS dealer_company, u.name AS dealer_name FROM ledger l JOIN users u ON u.id = l.dealer_id ORDER BY l.id DESC LIMIT 8')->fetchAll();
    $run = 0;
    json_out([
        'ok' => true,
        'summary' => [
            'pending_orders' => $pending,
            'open_orders' => $openOrders,
            'month_sales' => $monthSales,
            'month_sales_text' => money_try($monthSales),
            'month_payments' => $monthPay,
            'month_payments_text' => money_try($monthPay),
            'open_ar' => $openAr,
            'open_ar_text' => money_try($openAr),
            'stock_value' => $stockVal,
            'stock_value_text' => money_try($stockVal),
            'low_stock_count' => (int) $pdo->query('SELECT COUNT(*) FROM products WHERE active = 1 AND stock <= min_stock')->fetchColumn(),
        ],
        'low_stock' => array_map(static function ($p) {
            return [
                'id' => (int) $p['id'],
                'name' => $p['name'],
                'barcode' => $p['barcode'],
                'stock' => (int) $p['stock'],
                'min_stock' => (int) $p['min_stock'],
            ];
        }, $low),
        'debtors' => array_map(static function ($d) {
            $b = (int) $d['balance'];
            return [
                'id' => (int) $d['id'],
                'name' => $d['name'],
                'company' => $d['company'],
                'phone' => $d['phone'],
                'balance' => $b,
                'balance_text' => money_try($b),
            ];
        }, $debtors),
        'recent' => array_map(static function ($r) use (&$run) {
            $row = map_ledger($r, $run);
            $row['dealer_company'] = $r['dealer_company'];
            $row['dealer_name'] = $r['dealer_name'];
            return $row;
        }, $recent),
    ]);
}

function action_admin_orders_filtered(): void
{
    require_user('admin');
    $status = trim((string) ($_GET['status'] ?? ''));
    $dealerId = (int) ($_GET['dealer_id'] ?? 0);
    $q = trim((string) ($_GET['q'] ?? ''));
    $from = trim((string) ($_GET['from'] ?? ''));
    $to = trim((string) ($_GET['to'] ?? ''));
    $sql = 'SELECT DISTINCT o.*, u.name AS dealer_name, u.company AS dealer_company, u.phone AS dealer_phone
            FROM orders o JOIN users u ON u.id = o.dealer_id';
    $args = [];
    $where = [];
    if ($q !== '') {
        $sql .= ' LEFT JOIN order_items i ON i.order_id = o.id';
        $like = '%' . $q . '%';
        $idGuess = ctype_digit($q) ? (int) $q : 0;
        $where[] = '(o.id = ? OR u.name LIKE ? OR u.company LIKE ? OR u.phone LIKE ? OR i.name LIKE ? OR i.barcode LIKE ?)';
        $args[] = $idGuess;
        $args[] = $like;
        $args[] = $like;
        $args[] = '%' . digits($q) . '%';
        $args[] = $like;
        $args[] = '%' . digits($q) . '%';
    }
    if ($dealerId > 0) {
        $where[] = 'o.dealer_id = ?';
        $args[] = $dealerId;
    }
    if ($status !== '') {
        $where[] = 'o.status = ?';
        $args[] = $status;
    }
    if ($from !== '') {
        $where[] = 'date(o.created_at) >= date(?)';
        $args[] = $from;
    }
    if ($to !== '') {
        $where[] = 'date(o.created_at) <= date(?)';
        $args[] = $to;
    }
    if ($where) {
        $sql .= ' WHERE ' . implode(' AND ', $where);
    }
    $sql .= ' ORDER BY o.id DESC';
    $st = db()->prepare($sql);
    $st->execute($args);
    $list = [];
    $sum = 0;
    $counts = ['pending' => 0, 'approved' => 0, 'shipped' => 0, 'cancelled' => 0, 'paid' => 0];
    foreach ($st as $o) {
        $mapped = map_order($o, order_items((int) $o['id']));
        if ($mapped['status'] !== 'cancelled') {
            $sum += (int) $mapped['total'];
        }
        $stt = $mapped['status'];
        if (isset($counts[$stt])) {
            $counts[$stt]++;
        }
        $list[] = $mapped;
    }
    json_out([
        'ok' => true,
        'orders' => $list,
        'count' => count($list),
        'total' => $sum,
        'total_text' => money_try($sum),
        'counts' => $counts,
    ]);
}

function action_cari_list(): void
{
    require_user('admin');
    $q = trim((string) ($_GET['q'] ?? ''));
    $onlyDebt = (string) ($_GET['debt'] ?? '') === '1';
    $sql = "SELECT u.id, u.name, u.company, u.phone, u.city, u.active, u.approved, u.created_at,
            COALESCE((SELECT SUM(amount) FROM ledger l WHERE l.dealer_id = u.id), 0) AS balance,
            COALESCE((SELECT SUM(amount) FROM ledger l WHERE l.dealer_id = u.id AND amount > 0), 0) AS debit,
            COALESCE((SELECT SUM(-amount) FROM ledger l WHERE l.dealer_id = u.id AND amount < 0), 0) AS credit,
            (SELECT MAX(created_at) FROM ledger l WHERE l.dealer_id = u.id) AS last_move
            FROM users u WHERE u.role = 'dealer'";
    $args = [];
    if ($q !== '') {
        $sql .= ' AND (u.name LIKE ? OR u.company LIKE ? OR u.phone LIKE ?)';
        $like = '%' . $q . '%';
        $args[] = $like;
        $args[] = $like;
        $args[] = '%' . digits($q) . '%';
    }
    $sql .= ' ORDER BY balance DESC, u.id DESC';
    $st = db()->prepare($sql);
    $st->execute($args);
    $rows = [];
    $open = 0;
    foreach ($st as $r) {
        $bal = (int) $r['balance'];
        if ($onlyDebt && $bal <= 0) {
            continue;
        }
        if ($bal > 0) {
            $open += $bal;
        }
        $rows[] = [
            'id' => (int) $r['id'],
            'name' => $r['name'],
            'company' => $r['company'],
            'phone' => $r['phone'],
            'city' => $r['city'],
            'active' => (int) $r['active'],
            'approved' => (int) $r['approved'],
            'created_at' => $r['created_at'],
            'balance' => $bal,
            'balance_text' => money_try(abs($bal)),
            'balance_sign' => $bal > 0 ? 'borc' : ($bal < 0 ? 'alacak' : 'sifir'),
            'debit' => (int) $r['debit'],
            'debit_text' => money_try((int) $r['debit']),
            'credit' => (int) $r['credit'],
            'credit_text' => money_try((int) $r['credit']),
            'last_move' => $r['last_move'],
        ];
    }
    json_out(['ok' => true, 'dealers' => $rows, 'open_ar' => $open, 'open_ar_text' => money_try($open)]);
}

function action_cari_card(): void
{
    require_user('admin');
    $id = (int) ($_GET['id'] ?? 0);
    $st = db()->prepare("SELECT id, name, company, phone, city, active, approved, created_at FROM users WHERE id = ? AND role = 'dealer'");
    $st->execute([$id]);
    $dealer = $st->fetch();
    if (!$dealer) {
        json_out(['ok' => false, 'error' => 'Bayi bulunamadı.'], 404);
    }
    $pdo = db();
    $ost = $pdo->prepare('SELECT o.*, u.name AS dealer_name, u.company AS dealer_company, u.phone AS dealer_phone FROM orders o JOIN users u ON u.id = o.dealer_id WHERE o.dealer_id = ? ORDER BY o.id DESC');
    $ost->execute([$id]);
    $orders = [];
    $spent = 0;
    foreach ($ost as $o) {
        $spent += (int) $o['total'];
        $orders[] = map_order($o, order_items((int) $o['id']));
    }
    $lst = $pdo->prepare('SELECT * FROM ledger WHERE dealer_id = ? ORDER BY id ASC');
    $lst->execute([$id]);
    $lines = [];
    $run = 0;
    foreach ($lst as $row) {
        $lines[] = map_ledger($row, $run);
    }
    $lines = array_reverse($lines);
    $bal = dealer_balance($pdo, $id);
    $dealer['approved'] = (int) $dealer['approved'];
    json_out([
        'ok' => true,
        'dealer' => $dealer,
        'balance' => $bal,
        'balance_text' => money_try(abs($bal)),
        'balance_sign' => $bal > 0 ? 'borc' : ($bal < 0 ? 'alacak' : 'sifir'),
        'orders' => $orders,
        'order_count' => count($orders),
        'total_spent' => $spent,
        'total_spent_text' => money_try($spent),
        'ledger' => $lines,
    ]);
}

function action_cari_payment(): void
{
    $u = require_user('admin');
    $in = $_POST ?: json_input();
    $dealerId = (int) ($in['dealer_id'] ?? 0);
    $amount = parse_try_to_kurus($in['amount'] ?? '0');
    $method = (string) ($in['method'] ?? 'havale');
    $note = trim((string) ($in['note'] ?? ''));
    $orderId = (int) ($in['order_id'] ?? 0);
    $kind = (string) ($in['kind'] ?? 'payment');
    if (!in_array($kind, ['payment', 'adjust', 'opening'], true)) {
        $kind = 'payment';
    }
    if ($dealerId <= 0 || $amount === 0) {
        json_out(['ok' => false, 'error' => 'Bayi ve tutar gerekli.'], 400);
    }
    $chk = db()->prepare("SELECT id FROM users WHERE id = ? AND role = 'dealer'");
    $chk->execute([$dealerId]);
    if (!$chk->fetch()) {
        json_out(['ok' => false, 'error' => 'Bayi bulunamadı.'], 404);
    }
    $allowed = ['nakit', 'havale', 'kart', 'cek', 'acik_hesap'];
    if (!in_array($method, $allowed, true)) {
        $method = 'havale';
    }
    $dir = (string) ($in['direction'] ?? 'borc');
    if ($kind === 'payment') {
        $signed = -abs($amount);
    } else {
        $signed = $dir === 'alacak' ? -abs($amount) : abs($amount);
    }
    $label = $kind === 'payment' ? 'Tahsilat' : ($kind === 'opening' ? 'Açılış bakiyesi' : 'Cari düzeltme');
    if ($note === '') {
        $note = $label;
    }
    unset($u);
    ledger_post(db(), $dealerId, $kind, $signed, $method, $orderId ? 'order' : '', $orderId, $note);
    json_out(['ok' => true, 'balance' => dealer_balance(db(), $dealerId), 'balance_text' => money_try(abs(dealer_balance(db(), $dealerId)))]);
}

function action_stock_list(): void
{
    require_user('admin');
    $q = trim((string) ($_GET['q'] ?? ''));
    $low = (string) ($_GET['low'] ?? '') === '1';
    $sql = 'SELECT p.*, c.name AS category_name FROM products p LEFT JOIN categories c ON c.id = p.category_id WHERE p.active = 1';
    $args = [];
    if ($low) {
        $sql .= ' AND p.stock <= p.min_stock';
    }
    if ($q !== '') {
        $sql .= ' AND (p.name LIKE ? OR p.brand LIKE ? OR p.barcode LIKE ?)';
        $like = '%' . $q . '%';
        $args[] = $like;
        $args[] = $like;
        $args[] = '%' . digits($q) . '%';
    }
    $sql .= ' ORDER BY p.stock ASC, p.name';
    $st = db()->prepare($sql);
    $st->execute($args);
    $rows = [];
    $value = 0;
    $qty = 0;
    foreach ($st as $p) {
        $cost = (int) ($p['cost'] ?? 0);
        $unit = $cost > 0 ? $cost : (int) $p['price'];
        $line = $unit * (int) $p['stock'];
        $value += $line;
        $qty += (int) $p['stock'];
        $pub = public_product($p);
        $pub['cost'] = $cost;
        $pub['cost_text'] = money_try($cost);
        $pub['min_stock'] = (int) ($p['min_stock'] ?? 3);
        $pub['stock_value'] = $line;
        $pub['stock_value_text'] = money_try($line);
        $pub['low'] = (int) $p['stock'] <= (int) ($p['min_stock'] ?? 3);
        $pub['category_name'] = (string) ($p['category_name'] ?? '');
        $rows[] = $pub;
    }
    json_out(['ok' => true, 'products' => $rows, 'qty' => $qty, 'value' => $value, 'value_text' => money_try($value)]);
}

function action_stock_card(): void
{
    require_user('admin');
    $id = (int) ($_GET['id'] ?? 0);
    $st = db()->prepare('SELECT p.*, c.name AS category_name FROM products p LEFT JOIN categories c ON c.id = p.category_id WHERE p.id = ?');
    $st->execute([$id]);
    $p = $st->fetch();
    if (!$p) {
        json_out(['ok' => false, 'error' => 'Ürün bulunamadı.'], 404);
    }
    $mv = db()->prepare('SELECT * FROM stock_moves WHERE product_id = ? ORDER BY id DESC LIMIT 80');
    $mv->execute([$id]);
    $moves = [];
    foreach ($mv as $m) {
        $qty = (int) $m['qty'];
        $moves[] = [
            'id' => (int) $m['id'],
            'kind' => $m['kind'],
            'kind_text' => stock_kind_label((string) $m['kind']),
            'qty' => $qty,
            'qty_text' => ($qty > 0 ? '+' : '') . $qty,
            'unit_cost' => (int) $m['unit_cost'],
            'unit_cost_text' => money_try((int) $m['unit_cost']),
            'note' => $m['note'],
            'ref_type' => $m['ref_type'],
            'ref_id' => (int) $m['ref_id'],
            'created_at' => $m['created_at'],
        ];
    }
    $pub = public_product($p);
    $cost = (int) ($p['cost'] ?? 0);
    $unit = $cost > 0 ? $cost : (int) $p['price'];
    $pub['cost'] = $cost;
    $pub['cost_text'] = money_try($cost);
    $pub['min_stock'] = (int) ($p['min_stock'] ?? 3);
    $pub['stock_value'] = $unit * (int) $p['stock'];
    $pub['stock_value_text'] = money_try($pub['stock_value']);
    $pub['low'] = (int) $p['stock'] <= (int) ($p['min_stock'] ?? 3);
    $pub['category_name'] = (string) ($p['category_name'] ?? '');
    json_out(['ok' => true, 'product' => $pub, 'moves' => $moves]);
}

function action_stock_move_save(): void
{
    require_user('admin');
    $in = $_POST ?: json_input();
    $id = (int) ($in['product_id'] ?? 0);
    $kind = (string) ($in['kind'] ?? 'in');
    $qty = (int) ($in['qty'] ?? 0);
    $cost = parse_try_to_kurus($in['cost'] ?? '0');
    $note = trim((string) ($in['note'] ?? ''));
    if ($id <= 0 || $qty === 0) {
        json_out(['ok' => false, 'error' => 'Ürün ve miktar gerekli.'], 400);
    }
    if (!in_array($kind, ['in', 'adjust', 'out'], true)) {
        $kind = 'in';
    }
    $pdo = db();
    $pdo->beginTransaction();
    try {
        if ($kind === 'in') {
            stock_apply($pdo, $id, abs($qty), 'in', '', 0, $note !== '' ? $note : 'Stok giriş', $cost);
        } elseif ($kind === 'out') {
            stock_apply($pdo, $id, -abs($qty), 'adjust', '', 0, $note !== '' ? $note : 'Manuel çıkış');
        } else {
            $st = $pdo->prepare('SELECT stock FROM products WHERE id = ?');
            $st->execute([$id]);
            $cur = (int) $st->fetchColumn();
            $target = (int) ($in['target'] ?? $qty);
            stock_apply($pdo, $id, $target - $cur, 'adjust', '', 0, $note !== '' ? $note : 'Sayım');
        }
        $pdo->commit();
    } catch (Throwable $e) {
        $pdo->rollBack();
        json_out(['ok' => false, 'error' => $e->getMessage()], 400);
    }
    $st = db()->prepare('SELECT * FROM products WHERE id = ?');
    $st->execute([$id]);
    json_out(['ok' => true, 'product' => public_product($st->fetch())]);
}
