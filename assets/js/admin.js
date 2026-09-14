const APP = window.APP;
const $ = (s, el = document) => el.querySelector(s);

function api(action, opts = {}) {
  const q = new URLSearchParams({ action, ...(opts.params || {}) });
  const url = APP.api + '?' + q.toString();
  const isForm = opts.body instanceof FormData;
  return fetch(url, {
    method: opts.method || (opts.body ? 'POST' : 'GET'),
    credentials: 'same-origin',
    headers: isForm || !opts.body ? {} : { 'Content-Type': 'application/json' },
    body: opts.body ? (isForm ? opts.body : JSON.stringify(opts.body)) : undefined,
  }).then(async (r) => {
    const data = await r.json().catch(() => ({}));
    if (!r.ok || data.ok === false) throw new Error(data.error || 'İşlem başarısız');
    return data;
  });
}
function toast(msg) {
  const t = $('#toast');
  t.textContent = msg;
  t.style.display = 'block';
  setTimeout(() => { t.style.display = 'none'; }, 2200);
}
function money(kurus) {
  return (Math.abs(kurus) / 100).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ₺';
}
function balHtml(sign, text) {
  if (sign === 'borc') return `<b class="amt-debt">${text} borç</b>`;
  if (sign === 'alacak') return `<b class="amt-ok">${text} alacak</b>`;
  return `<b>0,00 ₺</b>`;
}

const S = {
  user: null,
  cats: [],
  orders: [],
  products: [],
  dealers: [],
  page: 'ozet',
  filters: { status: '', dealer_id: '', q: '', from: '', to: '' },
};
let scanStream = null;
let scanTimer = null;
let html5Scanner = null;

function go(page, id) {
  S.page = page;
  location.hash = '#/' + page + (id ? '/' + id : '');
  render();
}

function fmtDate(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function loginView() {
  return `<div class="auth">
    <h2>Patron / Admin girişi</h2>
    <p>Ön muhasebe: cari, stok, sipariş.</p>
    <div class="form">
      <label>Telefon<input id="phone" value="05550000000" inputmode="tel"></label>
      <label>Şifre<input id="pass" type="password" value="Patron123!"></label>
      <button class="btn block" id="login">Giriş</button>
      <div class="hint">0555 000 00 00 / Patron123!</div>
    </div>
  </div>`;
}

function dashView(sum) {
  const s = sum?.summary || {};
  return `<div class="wrap admin-page">
    <h2>Ön muhasebe</h2>
    <div class="admin-hero four">
      <div class="stat"><span>Açık cari</span><b class="amt-debt">${s.open_ar_text || '0,00 ₺'}</b></div>
      <div class="stat"><span>Bu ay satış</span><b>${s.month_sales_text || '0,00 ₺'}</b></div>
      <div class="stat"><span>Bu ay tahsilat</span><b class="amt-ok">${s.month_payments_text || '0,00 ₺'}</b></div>
      <div class="stat"><span>Stok değeri</span><b>${s.stock_value_text || '0,00 ₺'}</b></div>
    </div>
    <div class="admin-hero">
      <div class="stat"><span>Bekleyen sipariş</span><b>${s.pending_orders || 0}</b></div>
      <div class="stat"><span>Kritik stok</span><b class="${(s.low_stock_count || 0) ? 'amt-debt' : ''}">${s.low_stock_count || 0}</b></div>
    </div>
    <h3>Borçlu cariler</h3>
    ${(sum?.debtors || []).map((d) => `<div class="order dealer-card" data-dealer="${d.id}">
      <div style="display:flex;justify-content:space-between;gap:8px"><div><b>${d.company || d.name}</b><div class="order-meta">${d.phone}</div></div>${balHtml('borc', d.balance_text)}</div>
    </div>`).join('') || '<div class="empty">Açık borç yok</div>'}
    <h3>Kritik stok</h3>
    ${(sum?.low_stock || []).map((p) => `<div class="order dealer-card" data-stock="${p.id}">
      <div style="display:flex;justify-content:space-between"><div><b>${p.name}</b><div class="order-meta">${p.barcode || ''}</div></div><b class="stock-low">${p.stock} adet</b></div>
    </div>`).join('') || '<div class="empty">Kritik stok yok</div>'}
    <h3>Son cari hareket</h3>
    ${(sum?.recent || []).map((r) => `<div class="ledger-line">
      <div><b>${r.kind_text}</b><div class="order-meta">${r.dealer_company || r.dealer_name} · ${fmtDate(r.created_at)}</div></div>
      <span class="${r.is_debit ? 'amt-debt' : 'amt-ok'}">${r.signed_text}</span>
    </div>`).join('') || '<div class="empty">Hareket yok</div>'}
  </div>`;
}

function ordersView() {
  const f = S.filters;
  const dealerOpts = (S.dealers || []).map((d) => `<option value="${d.id}" ${String(f.dealer_id) === String(d.id) ? 'selected' : ''}>${d.company || d.name}</option>`).join('');
  return `<div class="wrap admin-page">
    <h2>Siparişler</h2>
    <div class="filters">
      <input id="oq" placeholder="No, bayi, ürün, telefon" value="${f.q || ''}">
      <div class="add-row">
        <label>Durum
          <select id="ost">
            <option value="">Tümü</option>
            <option value="pending" ${f.status === 'pending' ? 'selected' : ''}>Ödeme bekleniyor</option>
            <option value="approved" ${f.status === 'approved' ? 'selected' : ''}>Onaylandı</option>
            <option value="shipped" ${f.status === 'shipped' ? 'selected' : ''}>Kargoda</option>
            <option value="cancelled" ${f.status === 'cancelled' ? 'selected' : ''}>İptal</option>
          </select>
        </label>
        <label>Bayi
          <select id="odl"><option value="">Tüm bayiler</option>${dealerOpts}</select>
        </label>
      </div>
      <div class="add-row">
        <label>Başlangıç<input id="ofrom" type="date" value="${f.from || ''}"></label>
        <label>Bitiş<input id="oto" type="date" value="${f.to || ''}"></label>
      </div>
      <button type="button" class="btn block" id="ofilter">Filtrele</button>
    </div>
    <div id="orderSum" class="hint" style="margin-bottom:10px">Yükleniyor…</div>
    <div id="olist"></div>
  </div>`;
}

function orderCard(o) {
  return `<div class="order">
    <div style="display:flex;justify-content:space-between;gap:8px;align-items:center">
      <div><b>#${o.id} ${o.dealer_company || o.dealer_name}</b><div style="font-size:13px;color:var(--muted)">${o.dealer_phone}</div><div class="order-meta">${fmtDate(o.created_at)}</div></div>
      <span class="pill ${o.status}">${o.status_text}</span>
    </div>
    <div style="margin:8px 0">${o.items.map((i) => `${i.qty}× ${i.name}`).join('<br>')}</div>
    <b>${o.total_text}</b>
    <div style="display:flex;gap:8px;margin-top:10px;flex-wrap:wrap">
      ${o.status === 'pending' ? `<button class="btn ok" data-st="${o.id}" data-to="approved">Tahsilat al, onayla</button>` : ''}
      ${o.status === 'approved' ? `<button class="btn" data-st="${o.id}" data-to="shipped">Kargoya ver</button>` : ''}
      ${o.status !== 'cancelled' && o.status !== 'shipped' ? `<button class="btn ghost" data-st="${o.id}" data-to="cancelled">İptal</button>` : ''}
    </div>
  </div>`;
}

function stockView() {
  return `<div class="wrap admin-page">
    <h2>Stok</h2>
    <div class="filters">
      <input id="sq" placeholder="Ürün, marka, barkod">
      <label class="chk"><input type="checkbox" id="slow"> Sadece kritik stok</label>
    </div>
    <div id="stockSum" class="hint" style="margin-bottom:10px"></div>
    <div id="slist"></div>
  </div>`;
}

function stockCardView() {
  return `<div class="wrap admin-page">
    <button type="button" class="back-link" onclick="go('stok')">← Stoka dön</button>
    <div id="stockHead" class="empty">Yükleniyor…</div>
    <h3>Stok işlemi</h3>
    <div class="add-card">
      <label>İşlem
        <select id="skind">
          <option value="in">Stok giriş (alış)</option>
          <option value="out">Manuel çıkış</option>
          <option value="adjust">Sayıma çek (yeni adet)</option>
        </select>
      </label>
      <div class="add-row">
        <label>Miktar / yeni adet<input id="sqty" type="number" min="0" value="1"></label>
        <label>Alış fiyatı (₺)<input id="scost" type="text" inputmode="decimal" placeholder="0,00"></label>
      </div>
      <label>Not<input id="snote" placeholder="İrsaliye / sayım notu"></label>
      <button type="button" class="btn block" id="smove">Kaydet</button>
    </div>
    <h3>Hareketler</h3>
    <div id="smoves"></div>
  </div>`;
}

function cariView() {
  return `<div class="wrap admin-page">
    <h2>Cariler</h2>
    <div class="filters">
      <input id="cq" placeholder="Firma, ad, telefon">
      <label class="chk"><input type="checkbox" id="cdebt"> Sadece borçlular</label>
    </div>
    <div id="cariSum" class="hint" style="margin-bottom:10px"></div>
    <div id="clist">Yükleniyor…</div>
    <h3>Yeni bayi / cari</h3>
    <div class="add-card">
      <label>Ad<input id="dn"></label>
      <label>Firma<input id="dc"></label>
      <div class="add-row">
        <label>Telefon<input id="dp"></label>
        <label>Şehir<input id="dci"></label>
      </div>
      <label>Şifre<input id="dw" type="password"></label>
      <button class="btn block" id="dsave">Cari kaydet</button>
    </div>
  </div>`;
}

function dealerHistoryView() {
  return `<div class="wrap admin-page">
    <button type="button" class="back-link" onclick="go('cari')">← Carilere dön</button>
    <div id="dealerHead" class="empty">Yükleniyor…</div>
    <h3>Tahsilat / düzeltme</h3>
    <div class="add-card">
      <label>İşlem
        <select id="lkind">
          <option value="payment">Tahsilat</option>
          <option value="adjust">Düzeltme</option>
          <option value="opening">Açılış bakiyesi</option>
        </select>
      </label>
      <div class="add-row">
        <label>Tutar (₺)<input id="lamount" type="text" inputmode="decimal" placeholder="0,00"></label>
        <label>Yön
          <select id="ldir">
            <option value="borc">Bayi borçlu</option>
            <option value="alacak">Bayi alacaklı</option>
          </select>
        </label>
      </div>
      <label>Ödeme türü
        <select id="lmethod">
          <option value="havale">Havale / EFT</option>
          <option value="nakit">Nakit</option>
          <option value="kart">Kart</option>
          <option value="cek">Çek</option>
        </select>
      </label>
      <label>Not<input id="lnote" placeholder="Dekont no, açıklama"></label>
      <button type="button" class="btn block" id="lpay">Kaydet</button>
    </div>
    <h3>Cari ekstre</h3>
    <div id="ledgerBox"></div>
    <h3>Siparişler</h3>
    <div id="dealerOrders"></div>
  </div>`;
}

function addView() {
  const catOpts = S.cats.map((c) => `<option value="${c.id}">${c.name}</option>`).join('');
  return `<div class="wrap admin-page">
    <h2>Ürün ekle</h2>
    <div class="add-card">
      <button class="btn ghost" id="scanBtn" type="button">Barkodu kameradan oku</button>
      <div id="scanWrap" class="scan-box hidden">
        <video id="video" autoplay playsinline></video>
        <div id="reader"></div>
      </div>
      <label>Barkod<input id="barcode" type="text" placeholder="Elle de yazabilirsiniz" inputmode="numeric" autocomplete="off"></label>
      <label class="photo-pick" id="photoBox">
        <span class="photo-hint">Fotoğraf çek veya galeriden seç</span>
        <input id="photo" type="file" accept="image/*" capture="environment" class="hidden">
        <img id="preview" class="hidden" alt="">
      </label>
      <label>Ürün adı<input id="name" type="text" placeholder="Örn. Pioneer 16cm hoparlör"></label>
      <div class="add-row">
        <label>Marka<input id="brand" type="text" placeholder="Pioneer, JBL..."></label>
        <label>Stok<input id="stock" type="number" value="1" min="0"></label>
      </div>
      <div class="add-row">
        <label>Alış (₺)<input id="cost" type="text" inputmode="decimal" placeholder="0,00"></label>
        <label>Kritik stok<input id="minStock" type="number" value="3" min="0"></label>
      </div>
      <div class="cat-inline">
        <label>Kategori
          <select id="cat"><option value="">Seçin</option>${catOpts}</select>
        </label>
        <button type="button" class="btn ghost" id="newCatBtn">+ Yeni</button>
      </div>
      <div id="newCatBox" class="cat-new hidden">
        <label>Yeni kategori adı<input id="newCatName" type="text" placeholder="Örn. Sis Lambası"></label>
        <button type="button" class="btn" id="saveCat">Kaydet</button>
      </div>
      <div class="add-row">
        <label class="big-price">Bayi fiyatı (₺)<input id="price" type="text" inputmode="decimal" placeholder="0,00"></label>
        <label class="big-price">Vitrin fiyatı (₺)<input id="listPrice" type="text" inputmode="decimal" placeholder="0,00"></label>
      </div>
      <label>Açıklama<textarea id="desc" rows="3"></textarea></label>
      <input type="hidden" id="pid" value="">
      <button class="btn block" id="save">Ürünü kaydet</button>
    </div>
  </div>`;
}

async function render() {
  const raw = location.hash.replace(/^#\/?/, '') || (S.user ? 'ozet' : 'login');
  const [page, id] = raw.split('/');
  S.page = page;
  if (!S.user) {
    $('#view').innerHTML = loginView();
    $('#login').onclick = async () => {
      try {
        const d = await api('login', { body: { phone: $('#phone').value, password: $('#pass').value } });
        if (d.user.role !== 'admin') throw new Error('Bu ekran sadece patron içindir.');
        S.user = d.user;
        await loadAll();
        go('ozet');
      } catch (e) { toast(e.message); }
    };
    return;
  }
  if (page === 'ekle') $('#view').innerHTML = addView();
  else if (page === 'siparis') $('#view').innerHTML = ordersView();
  else if (page === 'stok' && id) $('#view').innerHTML = stockCardView();
  else if (page === 'stok') $('#view').innerHTML = stockView();
  else if (page === 'cari') $('#view').innerHTML = cariView();
  else if (page === 'bayi' || page === 'bayiler') {
    if (page === 'bayiler' && !id) {
      go('cari');
      return;
    }
    $('#view').innerHTML = dealerHistoryView();
  } else if (page === 'urunler') {
    go('stok');
    return;
  } else $('#view').innerHTML = `<div class="wrap admin-page"><div class="empty">Yükleniyor…</div></div>`;
  bindAdmin(page, id);
}

async function loadAll() {
  const [boot, orders, products, dealers] = await Promise.all([
    api('boot'),
    api('admin_orders'),
    api('products'),
    api('cari_list').catch(() => ({ dealers: [] })),
  ]);
  S.cats = boot.categories;
  S.orders = orders.orders;
  S.products = products.products;
  S.dealers = dealers.dealers || [];
}

function bindStatusButtons() {
  document.querySelectorAll('[data-st]').forEach((b) => b.addEventListener('click', async () => {
    try {
      await api('order_status', { body: { id: Number(b.dataset.st), status: b.dataset.to } });
      toast('Sipariş güncellendi');
      await loadAll();
      render();
    } catch (e) { toast(e.message); }
  }));
}

function bindNav(page) {
  document.querySelectorAll('.bottom-nav [data-go]').forEach((b) => {
    const on = b.dataset.go === page
      || ((page === 'bayi' || page === 'bayiler') && b.dataset.go === 'cari')
      || (page === 'stok' && b.dataset.go === 'stok');
    b.classList.toggle('on', on);
  });
}

function bindAdmin(page, id) {
  bindNav(page);
  bindStatusButtons();
  if (page === 'ekle') {
    bindAdd();
    if (S.editProduct) {
      fillProduct(S.editProduct);
      S.editProduct = null;
    }
  }
  if (page === 'ozet' || !page || page === 'home' || page === 'siparis' && false) {
    /* placeholder */
  }
  if (!page || page === 'ozet' || page === 'home') loadOzet();
  if (page === 'siparis') bindOrders();
  if (page === 'stok' && id) loadStockCard(id);
  else if (page === 'stok') bindStock();
  if (page === 'cari') bindCari();
  if (page === 'bayi') loadDealerHistory(id);
}

async function loadOzet() {
  try {
    const d = await api('admin_summary');
    $('#view').innerHTML = dashView(d);
    bindNav('ozet');
    document.querySelectorAll('[data-dealer]').forEach((el) => el.addEventListener('click', () => go('bayi', el.dataset.dealer)));
    document.querySelectorAll('[data-stock]').forEach((el) => el.addEventListener('click', () => go('stok', el.dataset.stock)));
  } catch (e) {
    $('#view').innerHTML = `<div class="wrap admin-page"><div class="empty">${e.message}</div></div>`;
  }
}

function bindOrders() {
  const apply = async () => {
    S.filters = {
      q: $('#oq')?.value || '',
      status: $('#ost')?.value || '',
      dealer_id: $('#odl')?.value || '',
      from: $('#ofrom')?.value || '',
      to: $('#oto')?.value || '',
    };
    const d = await api('admin_orders', { params: S.filters });
    S.orders = d.orders;
    $('#orderSum').innerHTML = `<b>${d.count}</b> sipariş · Toplam ${d.total_text} · Bekleyen ${d.counts?.pending || 0} · Onaylı ${d.counts?.approved || 0} · Kargo ${d.counts?.shipped || 0}`;
    $('#olist').innerHTML = d.orders.map(orderCard).join('') || '<div class="empty">Kayıt yok</div>';
    bindStatusButtons();
  };
  $('#ofilter')?.addEventListener('click', () => apply().catch((e) => toast(e.message)));
  $('#oq')?.addEventListener('keydown', (e) => { if (e.key === 'Enter') apply().catch((err) => toast(err.message)); });
  apply().catch((e) => { $('#olist').innerHTML = `<div class="empty">${e.message}</div>`; });
}

function bindStock() {
  const apply = async () => {
    const d = await api('stock_list', { params: { q: $('#sq')?.value || '', low: $('#slow')?.checked ? '1' : '' } });
    $('#stockSum').innerHTML = `<b>${d.products.length}</b> ürün · ${d.qty} adet · Değer ${d.value_text}`;
    $('#slist').innerHTML = d.products.map((p) => `<div class="order dealer-card" data-stock="${p.id}">
      <div style="display:flex;justify-content:space-between;gap:8px">
        <div><b>${p.name}</b><div class="order-meta">${p.brand || ''} ${p.barcode || ''}</div></div>
        <div style="text-align:right"><b class="${p.low ? 'stock-low' : ''}">${p.stock} adet</b><div class="order-meta">${p.stock_value_text}</div></div>
      </div>
    </div>`).join('') || '<div class="empty">Ürün yok</div>';
    document.querySelectorAll('[data-stock]').forEach((el) => el.addEventListener('click', () => go('stok', el.dataset.stock)));
  };
  $('#sq')?.addEventListener('input', () => apply().catch((e) => toast(e.message)));
  $('#slow')?.addEventListener('change', () => apply().catch((e) => toast(e.message)));
  apply().catch((e) => { $('#slist').innerHTML = `<div class="empty">${e.message}</div>`; });
}

async function loadStockCard(id) {
  try {
    const d = await api('stock_card', { params: { id } });
    const p = d.product;
    const head = $('#stockHead');
    head.className = 'dealer-head';
    head.innerHTML = `<h2>${p.name}</h2>
      <p>${p.brand || ''} · ${p.barcode || '-'} · ${p.category_name || ''}</p>
      <div class="admin-hero">
        <div class="stat"><span>Stok</span><b class="${p.low ? 'stock-low' : ''}">${p.stock}</b></div>
        <div class="stat"><span>Stok değeri</span><b>${p.stock_value_text}</b></div>
      </div>
      <p class="order-meta">Bayi ${p.price_text} · Alış ${p.cost_text || '—'} · Kritik ${p.min_stock}</p>
      <p><button type="button" class="btn ghost" id="editP">Ürünü düzenle</button></p>`;
    $('#smoves').innerHTML = d.moves.length
      ? d.moves.map((m) => `<div class="ledger-line"><div><b>${m.kind_text}</b><div class="order-meta">${fmtDate(m.created_at)}${m.note ? ' · ' + m.note : ''}</div></div><span class="${m.qty < 0 ? 'amt-debt' : 'amt-ok'}">${m.qty_text}</span></div>`).join('')
      : '<div class="empty">Hareket yok</div>';
    $('#editP')?.addEventListener('click', () => {
      S.editProduct = p;
      go('ekle');
    });
    if (p.cost) $('#scost').value = (p.cost / 100).toString().replace('.', ',');
    $('#smove').onclick = async () => {
      try {
        const body = {
          product_id: Number(id),
          kind: $('#skind').value,
          qty: Number($('#sqty').value || 0),
          target: Number($('#sqty').value || 0),
          cost: $('#scost').value,
          note: $('#snote').value,
        };
        await api('stock_move', { body });
        toast('Stok güncellendi');
        loadStockCard(id);
      } catch (e) { toast(e.message); }
    };
  } catch (e) {
    $('#stockHead').textContent = e.message;
  }
}

function bindCari() {
  const apply = async () => {
    const d = await api('cari_list', { params: { q: $('#cq')?.value || '', debt: $('#cdebt')?.checked ? '1' : '' } });
    S.dealers = d.dealers;
    $('#cariSum').innerHTML = `<b>${d.dealers.length}</b> cari · Açık borç ${d.open_ar_text}`;
    $('#clist').innerHTML = d.dealers.map((x) => {
      const ok = Number(x.approved) === 1;
      return `<div class="order dealer-card" data-dealer="${x.id}">
        <div style="display:flex;justify-content:space-between;gap:8px;align-items:flex-start">
          <div><b>${x.company || x.name}</b><div class="order-meta">${x.phone}${x.city ? ' · ' + x.city : ''}</div></div>
          <div style="text-align:right">${balHtml(x.balance_sign, x.balance_text)}<div class="order-meta">${ok ? 'Onaylı' : 'Onay bekliyor'}</div></div>
        </div>
        ${ok ? '' : `<div style="margin-top:10px"><button type="button" class="btn ok" data-approve="${x.id}">Onayla</button></div>`}
      </div>`;
    }).join('') || '<div class="empty">Cari yok</div>';
    document.querySelectorAll('[data-dealer]').forEach((el) => el.addEventListener('click', () => go('bayi', el.dataset.dealer)));
    document.querySelectorAll('[data-approve]').forEach((b) => {
      b.addEventListener('click', async (e) => {
        e.stopPropagation();
        try {
          await api('dealer_approve', { body: { id: Number(b.dataset.approve), approved: 1 } });
          toast('Bayi onaylandı');
          apply();
        } catch (err) { toast(err.message); }
      });
    });
  };
  $('#cq')?.addEventListener('input', () => apply().catch((e) => toast(e.message)));
  $('#cdebt')?.addEventListener('change', () => apply().catch((e) => toast(e.message)));
  apply().catch((e) => { $('#clist').innerHTML = `<div class="empty">${e.message}</div>`; });
  $('#dsave').onclick = async () => {
    try {
      await api('dealer_save', { body: { name: $('#dn').value, company: $('#dc').value, phone: $('#dp').value, city: $('#dci').value, password: $('#dw').value } });
      toast('Cari kaydedildi');
      apply();
    } catch (e) { toast(e.message); }
  };
}

async function loadDealerHistory(id) {
  if (!id) {
    $('#dealerHead').textContent = 'Bayi bulunamadı';
    return;
  }
  try {
    const d = await api('cari_card', { params: { id } });
    const x = d.dealer;
    const head = $('#dealerHead');
    head.className = 'dealer-head';
    head.innerHTML = `<h2>${x.company || x.name}</h2>
      <p>${x.name} · ${x.phone}${x.city ? ' · ' + x.city : ''}</p>
      <p><span class="pill ${Number(x.approved) === 1 ? 'approved' : 'pending'}">${Number(x.approved) === 1 ? 'Onaylı bayi' : 'Onay bekliyor'}</span></p>
      ${Number(x.approved) === 1 ? '' : `<p><button type="button" class="btn ok" id="approveDealer">Bayiyi onayla</button></p>`}
      <div class="admin-hero">
        <div class="stat"><span>Cari bakiye</span>${balHtml(d.balance_sign, d.balance_text)}</div>
        <div class="stat"><span>Sipariş / ciro</span><b>${d.order_count} · ${d.total_spent_text}</b></div>
      </div>`;
    $('#ledgerBox').innerHTML = d.ledger.length
      ? d.ledger.map((r) => `<div class="ledger-line">
          <div><b>${r.kind_text}</b><div class="order-meta">${fmtDate(r.created_at)} · ${r.method_text}${r.note ? ' · ' + r.note : ''}</div></div>
          <div style="text-align:right"><span class="${r.is_debit ? 'amt-debt' : 'amt-ok'}">${r.signed_text}</span><div class="order-meta">${r.balance_text}</div></div>
        </div>`).join('')
      : '<div class="empty">Ekstre boş</div>';
    $('#dealerOrders').innerHTML = d.orders.length
      ? d.orders.map(orderCard).join('')
      : '<div class="empty">Bu bayinin henüz siparişi yok</div>';
    bindStatusButtons();
    $('#approveDealer')?.addEventListener('click', async () => {
      try {
        await api('dealer_approve', { body: { id: Number(id), approved: 1 } });
        toast('Bayi onaylandı');
        loadDealerHistory(id);
      } catch (err) { toast(err.message); }
    });
    const kind = $('#lkind');
    const dirWrap = $('#ldir')?.closest('label');
    const syncKind = () => {
      if (dirWrap) dirWrap.style.display = kind.value === 'payment' ? 'none' : '';
    };
    kind?.addEventListener('change', syncKind);
    syncKind();
    $('#lpay')?.addEventListener('click', async () => {
      try {
        await api('cari_payment', { body: {
          dealer_id: Number(id),
          kind: $('#lkind').value,
          amount: $('#lamount').value,
          method: $('#lmethod').value,
          direction: $('#ldir').value,
          note: $('#lnote').value,
        } });
        toast('Cari işlendi');
        $('#lamount').value = '';
        loadDealerHistory(id);
      } catch (e) { toast(e.message); }
    });
  } catch (e) {
    $('#dealerHead').textContent = e.message;
  }
}

function fillProduct(p) {
  if (!p) return;
  $('#pid').value = p.id;
  $('#barcode').value = p.barcode || '';
  $('#name').value = p.name;
  $('#brand').value = p.brand || '';
  $('#cat').value = p.category_id || '';
  $('#price').value = ((p.dealer_price ?? p.price) / 100).toString().replace('.', ',');
  if ($('#listPrice')) $('#listPrice').value = ((p.list_price || 0) / 100).toString().replace('.', ',');
  $('#stock').value = p.stock;
  if ($('#cost')) $('#cost').value = ((p.cost || 0) / 100).toString().replace('.', ',');
  if ($('#minStock')) $('#minStock').value = p.min_stock ?? 3;
  $('#desc').value = p.description || '';
  if (p.image) {
    $('#preview').src = p.image;
    $('#preview').classList.remove('hidden');
    $('.photo-hint')?.classList.add('hidden');
  }
}

function bindAdd() {
  $('#photo').onchange = () => {
    const f = $('#photo').files[0];
    if (!f) return;
    const url = URL.createObjectURL(f);
    $('#preview').src = url;
    $('#preview').classList.remove('hidden');
    $('.photo-hint')?.classList.add('hidden');
  };
  $('#barcode').addEventListener('change', () => lookupBarcode($('#barcode').value));
  $('#scanBtn').onclick = toggleScan;
  $('#save').onclick = saveProduct;
  $('#newCatBtn').onclick = () => $('#newCatBox').classList.toggle('hidden');
  $('#saveCat').onclick = saveCategory;
}

async function saveCategory() {
  const name = ($('#newCatName').value || '').trim();
  if (!name) {
    toast('Kategori adı yazın');
    return;
  }
  try {
    const d = await api('category_save', { body: { name } });
    S.cats.push(d.category);
    const opt = document.createElement('option');
    opt.value = String(d.category.id);
    opt.textContent = d.category.name;
    $('#cat').appendChild(opt);
    $('#cat').value = String(d.category.id);
    $('#newCatName').value = '';
    $('#newCatBox').classList.add('hidden');
    toast('Kategori eklendi');
  } catch (e) { toast(e.message); }
}

async function lookupBarcode(code) {
  const digits = String(code || '').replace(/\D/g, '');
  if (digits.length < 6) return;
  try {
    const r = await fetch(APP.api + '?action=product_by_barcode&barcode=' + encodeURIComponent(digits), { credentials: 'same-origin' });
    const d = await r.json();
    if (d.product) {
      fillProduct(d.product);
      toast('Ürün bulundu, güncelleyebilirsiniz');
    } else {
      $('#barcode').value = digits;
      toast('Yeni ürün — fotoğraf ve fiyat girin');
    }
  } catch (e) { toast(e.message); }
}

async function saveProduct() {
  const fd = new FormData();
  fd.append('id', $('#pid').value);
  fd.append('barcode', $('#barcode').value);
  fd.append('name', $('#name').value);
  fd.append('brand', $('#brand').value);
  fd.append('category_id', $('#cat').value);
  fd.append('price', ($('#price').value || '').replace(/\./g, '').replace(',', '.'));
  fd.append('list_price', ($('#listPrice')?.value || '').replace(/\./g, '').replace(',', '.'));
  fd.append('cost', ($('#cost')?.value || '').replace(/\./g, '').replace(',', '.'));
  fd.append('min_stock', $('#minStock')?.value || '3');
  fd.append('stock', $('#stock').value);
  fd.append('description', $('#desc').value);
  fd.append('active', '1');
  if ($('#photo').files[0]) fd.append('image', $('#photo').files[0]);
  try {
    await api('product_save', { body: fd });
    toast('Ürün kaydedildi');
    await loadAll();
    go('stok');
  } catch (e) { toast(e.message); }
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    if ([...document.scripts].some((s) => s.src.includes('html5-qrcode'))) return resolve();
    const s = document.createElement('script');
    s.src = src;
    s.onload = resolve;
    s.onerror = reject;
    document.head.appendChild(s);
  });
}

function onCode(value) {
  stopScan();
  $('#barcode').value = value;
  lookupBarcode(value);
}

async function toggleScan() {
  const wrap = $('#scanWrap');
  if (!wrap.classList.contains('hidden')) {
    stopScan();
    return;
  }
  wrap.classList.remove('hidden');
  try {
    if ('BarcodeDetector' in window) {
      scanStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
      const video = $('#video');
      video.classList.remove('hidden');
      video.srcObject = scanStream;
      await video.play();
      const det = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'qr_code', 'upc_a', 'upc_e', 'code_39'] });
      const tick = async () => {
        try {
          const codes = await det.detect(video);
          if (codes[0]?.rawValue) {
            onCode(codes[0].rawValue);
            return;
          }
        } catch (_) {}
        scanTimer = requestAnimationFrame(tick);
      };
      tick();
      return;
    }
    $('#video')?.classList.add('hidden');
    await loadScript('https://cdn.jsdelivr.net/npm/html5-qrcode@2.3.8/html5-qrcode.min.js');
    html5Scanner = new Html5Qrcode('reader');
    await html5Scanner.start(
      { facingMode: 'environment' },
      { fps: 10, qrbox: { width: 240, height: 140 } },
      (decoded) => onCode(decoded)
    );
  } catch (e) {
    wrap.classList.add('hidden');
    toast('Kamera açılamadı. Barkodu elle yazabilirsiniz.');
  }
}

async function stopScan() {
  $('#scanWrap')?.classList.add('hidden');
  if (scanTimer) cancelAnimationFrame(scanTimer);
  scanTimer = null;
  if (scanStream) {
    scanStream.getTracks().forEach((t) => t.stop());
    scanStream = null;
  }
  if (html5Scanner) {
    try { await html5Scanner.stop(); } catch (_) {}
    html5Scanner = null;
  }
}

window.go = go;

document.querySelectorAll('.bottom-nav [data-go]').forEach((b) => {
  b.addEventListener('click', () => go(b.dataset.go));
});
$('#logout')?.addEventListener('click', async () => {
  await api('logout', { method: 'POST', body: {} });
  S.user = null;
  location.hash = '';
  render();
});

(async function boot() {
  try {
    const d = await api('boot');
    S.user = d.user && d.user.role === 'admin' ? d.user : null;
    if (S.user) await loadAll();
  } catch (_) {}
  render();
  window.addEventListener('hashchange', render);
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register(APP.base + '/sw.js', { scope: APP.base + '/' }).catch(() => {});
  }
})();
