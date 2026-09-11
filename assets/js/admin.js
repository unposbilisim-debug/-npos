const APP = window.APP;
const $ = (s, el = document) => el.querySelector(s);

function api(action, opts = {}) {
  const url = APP.api + '?action=' + encodeURIComponent(action);
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

function go(page) {
  S.page = page;
  location.hash = '#/' + page;
  render();
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
  return `<div class="wrap">
    <div class="admin-hero">
      <div class="stat"><span>Bekleyen sipariş</span><b>${pending}</b></div>
      <div class="stat"><span>Ürün</span><b>${S.products.length}</b></div>
    </div>
    <button class="btn orange block" onclick="go('ekle')">📷 Barkod oku, fotoğraf çek, ürün ekle</button>
    <h3>Son siparişler</h3>
    ${S.orders.slice(0, 8).map(orderCard).join('') || '<div class="empty">Sipariş yok</div>'}
  </div>`;
}

function orderCard(o) {
  return `<div class="order">
    <div style="display:flex;justify-content:space-between;gap:8px;align-items:center">
      <div><b>#${o.id} ${o.dealer_company || o.dealer_name}</b><div style="font-size:13px;color:var(--muted)">${o.dealer_phone}</div></div>
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
  return `<div class="wrap" style="padding-top:12px">
    <h2>Ürün ekle / güncelle</h2>
    <p style="color:var(--muted)">Kamerayı ürüne tutun. Barkod okununca varsa ürün dolar, yoksa yeni kayıt açılır.</p>
    <div class="form">
      <button class="btn block" id="scanBtn">📷 Barkodu kameradan oku</button>
      <div id="scanWrap" class="scan-box hidden"><video id="video" autoplay playsinline></video></div>
      <label>Barkod<input id="barcode" placeholder="Elle de yazabilirsiniz" inputmode="numeric"></label>
      <label class="photo-pick" id="photoBox">📷 Ürünün fotoğrafını çekin
        <input id="photo" type="file" accept="image/*" capture="environment" class="hidden">
        <img id="preview" class="hidden" alt="">
      </label>
      <label>Ürün adı<input id="name" placeholder="Örn. Hugin T300"></label>
      <label>Marka<input id="brand" placeholder="Hugin, PAX..."></label>
      <label>Kategori<select id="cat"><option value="">Seçin</option>${S.cats.map(c => `<option value="${c.id}">${c.name}</option>`).join('')}</select></label>
      <label class="big-price">Bayi fiyatı (₺)<input id="price" inputmode="decimal" placeholder="0,00"></label>
      <label>Stok<input id="stock" type="number" value="1"></label>
      <label>Açıklama<textarea id="desc"></textarea></label>
      <input type="hidden" id="pid" value="">
      <button class="btn block" id="save">Kaydet</button>
    </div>
  </div>`;
}

function productsView() {
  return `<div class="wrap" style="padding-top:12px">
    <h2>Ürünler</h2>
    <input id="pq" placeholder="Ara..." style="margin-bottom:10px">
    <div class="grid" id="plist">${S.products.map(p => `<article class="card" data-edit="${p.id}">
      <div class="ph"><img src="${p.image}"></div>
      <div class="body"><div class="brand-l">${p.brand || ''}</div><h3>${p.name}</h3><div class="price">${p.price_text}</div></div>
    </article>`).join('')}</div>
  </div>`;
}

function dealersView() {
  return `<div class="wrap" style="padding-top:12px">
    <h2>Bayiler</h2>
    <div id="dlist" class="empty">Yükleniyor…</div>
    <h3>Yeni bayi</h3>
    <div class="form" style="background:#fff;padding:14px;border-radius:16px">
      <label>Ad<input id="dn"></label>
      <label>Firma<input id="dc"></label>
      <label>Telefon<input id="dp"></label>
      <label>Şehir<input id="dci"></label>
      <label>Şifre<input id="dw" type="password"></label>
      <button class="btn" id="dsave">Bayi kaydet</button>
    </div>
  </div>`;
}

async function render() {
  const page = (location.hash.replace('#/', '') || (S.user ? 'siparis' : 'login'));
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
  else $('#view').innerHTML = dash();
  bindAdmin(page);
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

function bindAdmin(page) {
  document.querySelectorAll('[data-st]').forEach((b) => b.addEventListener('click', async () => {
    try {
      await api('order_status', { body: { id: Number(b.dataset.st), status: b.dataset.to } });
      toast('Sipariş güncellendi');
      await loadAll();
      render();
    } catch (e) { toast(e.message); }
  }));
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
  }
  if (page === 'bayiler') {
    api('dealers').then((d) => {
      $('#dlist').innerHTML = d.dealers.map((x) => `<div class="order"><b>${x.company || x.name}</b><div>${x.phone} · ${x.city || ''}</div></div>`).join('');
    });
    $('#dsave').onclick = async () => {
      try {
        await api('dealer_save', { body: { name: $('#dn').value, company: $('#dc').value, phone: $('#dp').value, city: $('#dci').value, password: $('#dw').value } });
        toast('Bayi kaydedildi');
        render();
      } catch (e) { toast(e.message); }
    };
  }
}

function fillProduct(p) {
  if (!p) return;
  $('#pid').value = p.id;
  $('#barcode').value = p.barcode || '';
  $('#name').value = p.name;
  $('#brand').value = p.brand || '';
  $('#cat').value = p.category_id || '';
  $('#price').value = (p.price / 100).toString().replace('.', ',');
  $('#stock').value = p.stock;
  $('#desc').value = p.description || '';
  if (p.image) {
    $('#preview').src = p.image;
    $('#preview').classList.remove('hidden');
  }
}

function bindAdd() {
  $('#photoBox').onclick = () => $('#photo').click();
  $('#photo').onchange = () => {
    const f = $('#photo').files[0];
    if (!f) return;
    const url = URL.createObjectURL(f);
    $('#preview').src = url;
    $('#preview').classList.remove('hidden');
  };
  $('#barcode').addEventListener('change', () => lookupBarcode($('#barcode').value));
  $('#scanBtn').onclick = toggleScan;
  $('#save').onclick = saveProduct;
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

async function toggleScan() {
  const wrap = $('#scanWrap');
  if (!wrap.classList.contains('hidden')) {
    stopScan();
    return;
  }
  wrap.classList.remove('hidden');
  try {
    scanStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
    const video = $('#video');
    video.srcObject = scanStream;
    await video.play();
    if ('BarcodeDetector' in window) {
      const det = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'qr_code', 'upc_a', 'upc_e', 'code_39'] });
      const tick = async () => {
        try {
          const codes = await det.detect(video);
          if (codes[0]?.rawValue) {
            stopScan();
            $('#barcode').value = codes[0].rawValue;
            lookupBarcode(codes[0].rawValue);
            return;
          }
        } catch (_) {}
        scanTimer = requestAnimationFrame(tick);
      };
      tick();
    } else {
      toast('Bu tarayıcı otomatik barkod okumuyor. Barkodu elle yazın veya Chrome kullanın.');
    }
  } catch (e) {
    wrap.classList.add('hidden');
    toast('Kamera açılamadı. Tarayıcı izni verin.');
  }
}

function stopScan() {
  $('#scanWrap')?.classList.add('hidden');
  if (scanTimer) cancelAnimationFrame(scanTimer);
  scanTimer = null;
  if (scanStream) {
    scanStream.getTracks().forEach((t) => t.stop());
    scanStream = null;
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
