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

const S = { user: null, cats: [], orders: [], products: [], page: 'siparis' };
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
    <p>Telefondan ürün eklemek ve sipariş onaylamak için.</p>
    <div class="form">
      <label>Telefon<input id="phone" value="05550000000" inputmode="tel"></label>
      <label>Şifre<input id="pass" type="password" value="Patron123!"></label>
      <button class="btn block" id="login">Giriş</button>
      <div class="hint">0555 000 00 00 / Patron123!</div>
    </div>
  </div>`;
}

function dash() {
  const pending = S.orders.filter((o) => o.status === 'pending').length;
  return `<div class="wrap admin-page">
    <div class="admin-hero">
      <div class="stat"><span>Bekleyen sipariş</span><b>${pending}</b></div>
      <div class="stat"><span>Ürün</span><b>${S.products.length}</b></div>
    </div>
    <h3>Son siparişler</h3>
    ${S.orders.slice(0, 8).map(orderCard).join('') || '<div class="empty">Sipariş yok</div>'}
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
      ${o.status === 'pending' ? `<button class="btn ok" data-st="${o.id}" data-to="approved">Ödeme alındı, onayla</button>` : ''}
      ${o.status === 'approved' ? `<button class="btn" data-st="${o.id}" data-to="shipped">Kargoya ver</button>` : ''}
      ${o.status !== 'cancelled' && o.status !== 'shipped' ? `<button class="btn ghost" data-st="${o.id}" data-to="cancelled">İptal</button>` : ''}
    </div>
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

function productsView() {
  return `<div class="wrap admin-page">
    <h2>Ürünler</h2>
    <input id="pq" placeholder="Ürün ara..." style="margin-bottom:10px">
    <div class="grid" id="plist">${S.products.map(p => `<article class="card" data-edit="${p.id}">
      <div class="ph"><img src="${p.image}"></div>
      <div class="body"><div class="brand-l">${p.brand || ''}</div><h3>${p.name}</h3><div class="price">${p.dealer_price_text || p.price_text}</div><div class="price-list">Vitrin ${p.list_price_text}</div></div>
    </article>`).join('')}</div>
  </div>`;
}

function dealersView() {
  return `<div class="wrap admin-page">
    <h2>Bayiler</h2>
    <p style="color:var(--muted);margin:0 0 10px">Bir bayiye dokunun, alışveriş geçmişini görün.</p>
    <div id="dlist">Yükleniyor…</div>
    <h3>Yeni bayi</h3>
    <div class="add-card">
      <label>Ad<input id="dn"></label>
      <label>Firma<input id="dc"></label>
      <div class="add-row">
        <label>Telefon<input id="dp"></label>
        <label>Şehir<input id="dci"></label>
      </div>
      <label>Şifre<input id="dw" type="password"></label>
      <button class="btn block" id="dsave">Bayi kaydet</button>
    </div>
  </div>`;
}

function dealerHistoryView() {
  return `<div class="wrap admin-page">
    <button type="button" class="back-link" onclick="go('bayiler')">← Bayilere dön</button>
    <div id="dealerHead" class="empty">Yükleniyor…</div>
    <h3>Alışveriş geçmişi</h3>
    <div id="dealerOrders"></div>
  </div>`;
}

async function render() {
  const raw = location.hash.replace(/^#\/?/, '') || (S.user ? 'siparis' : 'login');
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
        go('siparis');
      } catch (e) { toast(e.message); }
    };
    return;
  }
  if (page === 'ekle') $('#view').innerHTML = addView();
  else if (page === 'urunler') $('#view').innerHTML = productsView();
  else if (page === 'bayiler') $('#view').innerHTML = dealersView();
  else if (page === 'bayi') $('#view').innerHTML = dealerHistoryView();
  else $('#view').innerHTML = dash();
  bindAdmin(page, id);
}

async function loadAll() {
  const [boot, orders, products] = await Promise.all([
    api('boot'),
    api('admin_orders'),
    api('products'),
  ]);
  S.cats = boot.categories;
  S.orders = orders.orders;
  S.products = products.products;
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

function bindAdmin(page, id) {
  document.querySelectorAll('.bottom-nav [data-go]').forEach((b) => {
    const on = b.dataset.go === page || (page === 'bayi' && b.dataset.go === 'bayiler');
    b.classList.toggle('on', on);
  });
  bindStatusButtons();
  if (page === 'ekle') {
    bindAdd();
    if (S.editProduct) {
      fillProduct(S.editProduct);
      S.editProduct = null;
    }
  }
  if (page === 'urunler') {
    document.querySelectorAll('[data-edit]').forEach((el) => el.addEventListener('click', () => {
      S.editProduct = S.products.find((x) => x.id === Number(el.dataset.edit));
      go('ekle');
    }));
    $('#pq')?.addEventListener('input', () => {
      const q = $('#pq').value.toLowerCase();
      document.querySelectorAll('#plist .card').forEach((el) => {
        el.style.display = el.textContent.toLowerCase().includes(q) ? '' : 'none';
      });
    });
  }
  if (page === 'bayiler') {
    api('dealers').then((d) => {
      const list = $('#dlist');
      if (!d.dealers.length) {
        list.innerHTML = '<div class="empty">Bayi yok</div>';
        return;
      }
      list.innerHTML = d.dealers.map((x) => {
        const ok = Number(x.approved) === 1;
        return `<div class="order dealer-card" data-dealer="${x.id}">
        <div style="display:flex;justify-content:space-between;gap:8px;align-items:center">
          <div><b>${x.company || x.name}</b><div style="font-size:13px;color:var(--muted)">${x.phone}${x.city ? ' · ' + x.city : ''}</div></div>
          <span class="pill ${ok ? 'approved' : 'pending'}">${ok ? 'Onaylı' : 'Onay bekliyor'}</span>
        </div>
        ${ok ? '' : `<div style="margin-top:10px"><button type="button" class="btn ok" data-approve="${x.id}">Onayla</button></div>`}
      </div>`;
      }).join('');
      list.querySelectorAll('[data-dealer]').forEach((el) => {
        el.addEventListener('click', () => go('bayi', el.dataset.dealer));
      });
      list.querySelectorAll('[data-approve]').forEach((b) => {
        b.addEventListener('click', async (e) => {
          e.stopPropagation();
          try {
            await api('dealer_approve', { body: { id: Number(b.dataset.approve), approved: 1 } });
            toast('Bayi onaylandı');
            render();
          } catch (err) { toast(err.message); }
        });
      });
    }).catch((e) => { $('#dlist').innerHTML = `<div class="empty">${e.message}</div>`; });
    $('#dsave').onclick = async () => {
      try {
        await api('dealer_save', { body: { name: $('#dn').value, company: $('#dc').value, phone: $('#dp').value, city: $('#dci').value, password: $('#dw').value } });
        toast('Bayi kaydedildi');
        render();
      } catch (e) { toast(e.message); }
    };
  }
  if (page === 'bayi') loadDealerHistory(id);
}

async function loadDealerHistory(id) {
  if (!id) {
    $('#dealerHead').textContent = 'Bayi bulunamadı';
    return;
  }
  try {
    const d = await api('dealer_orders', { params: { id } });
    const x = d.dealer;
    const head = $('#dealerHead');
    head.className = 'dealer-head';
    head.innerHTML = `<h2>${x.company || x.name}</h2>
      <p>${x.name} · ${x.phone}${x.city ? ' · ' + x.city : ''}</p>
      <p><span class="pill ${Number(x.approved) === 1 ? 'approved' : 'pending'}">${Number(x.approved) === 1 ? 'Onaylı bayi' : 'Onay bekliyor'}</span></p>
      ${Number(x.approved) === 1 ? '' : `<p><button type="button" class="btn ok" id="approveDealer">Bayiyi onayla</button></p>`}
      <div class="admin-hero">
        <div class="stat"><span>Sipariş</span><b>${d.order_count}</b></div>
        <div class="stat"><span>Toplam alışveriş</span><b>${d.total_spent_text}</b></div>
      </div>`;
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
  if ($('#listPrice')) {
    $('#listPrice').value = ((p.list_price || 0) / 100).toString().replace('.', ',');
  }
  $('#stock').value = p.stock;
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
  const raw = $('#price').value.replace(/\./g, '').replace(',', '.');
  fd.append('price', raw);
  const listRaw = ($('#listPrice')?.value || '').replace(/\./g, '').replace(',', '.');
  fd.append('list_price', listRaw);
  fd.append('stock', $('#stock').value);
  fd.append('description', $('#desc').value);
  fd.append('active', '1');
  if ($('#photo').files[0]) fd.append('image', $('#photo').files[0]);
  try {
    await api('product_save', { body: fd });
    toast('Ürün kaydedildi');
    await loadAll();
    go('urunler');
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
