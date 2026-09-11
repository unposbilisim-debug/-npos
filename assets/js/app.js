const APP = window.APP;
const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];

const state = {
  user: null,
  cats: [],
  products: [],
  cart: JSON.parse(localStorage.getItem('yt_cart') || '[]'),
  view: 'home',
};

function api(action, opts = {}) {
  const url = APP.api + (APP.api.includes('?') ? '&' : '?') + 'action=' + encodeURIComponent(action);
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

function money(kurus) {
  return (kurus / 100).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ₺';
}
function saveCart() {
  localStorage.setItem('yt_cart', JSON.stringify(state.cart));
  const n = state.cart.reduce((s, i) => s + i.qty, 0);
  $$('[data-cart-count]').forEach((el) => { el.textContent = n; el.classList.toggle('hidden', n === 0); });
}
function toast(msg) {
  const t = $('#toast');
  t.textContent = msg;
  t.style.display = 'block';
  setTimeout(() => { t.style.display = 'none'; }, 2200);
}
function addCart(p, qty = 1) {
  if (!state.user || state.user.role !== 'dealer') {
    toast('Sipariş için bayi girişi yapın');
    location.hash = '#/giris';
    return;
  }
  const row = state.cart.find((i) => i.id === p.id);
  if (row) row.qty += qty;
  else state.cart.push({ id: p.id, name: p.name, price: p.price, image: p.image, qty });
  saveCart();
  toast('Sepete eklendi');
}

function setNav() {
  const u = state.user;
  $('#who').textContent = u ? (u.role === 'admin' ? 'Yönetim' : u.company || u.name) : 'Giriş Yap';
  $('#whoBtn').onclick = () => {
    if (u?.role === 'admin') location.href = APP.admin;
    else location.hash = u ? '#/hesabim' : '#/giris';
  };
}

function productCard(p) {
  return `<article class="card" data-open="${p.id}">
    <div class="ph"><img src="${p.image}" alt=""></div>
    <div class="body">
      <div class="brand-l">${p.brand || ''}</div>
      <h3>${p.name}</h3>
      <div class="price">${p.price_text}</div>
      <button class="add" data-add="${p.id}">Sepete at</button>
    </div>
  </article>`;
}

function viewHome() {
  const campaigns = [
    { img: APP.base + '/assets/img/banners/kampanya.jpg?v=11' },
    { img: APP.base + '/assets/img/banners/kampanya2.jpg?v=11' },
    { img: APP.base + '/assets/img/banners/kampanya3.jpg?v=11' },
    { img: APP.base + '/assets/img/banners/kampanya4.jpg?v=11' },
  ];
  const slides = campaigns.concat(campaigns[0]);
  const catImg = (slug) => APP.base + '/assets/img/cats/' + slug + '.png';
  return `
    <section class="hero wrap" aria-label="Kampanyalar">
      <div class="hero-pop" id="heroPop">
        <div class="hero-track" id="heroTrack">
          ${slides.map((c) => `<div class="hero-slide">
            <img src="${c.img}" alt="ENS Oto Market kampanya">
          </div>`).join('')}
        </div>
      </div>
    </section>
    <div class="wrap"><div class="section-title"><h2>Kategoriler</h2></div></div>
    <div class="cat-strip wrap">
      ${state.cats.map((c) => `<button class="cat-tile" onclick="location.hash='#/kategori/${c.slug}'"><img src="${catImg(c.slug)}" alt=""><span>${c.name}</span></button>`).join('')}
    </div>
    <div class="brands wrap">
      ${['PIONEER','JBL','ALPINE','KENWOOD','SONY','PHILIPS','BLAUPUNKT','FOCAL'].map(b => `<div class="brand">${b}</div>`).join('')}
    </div>
    <div class="wrap">
      <div class="section-title"><h2>Tüm ürünler</h2></div>
      <div class="grid">${state.products.map(productCard).join('')}</div>
    </div>`;
}

function viewList(catId, q) {
  let list = state.products;
  const cat = state.cats.find((c) => String(c.id) === String(catId) || c.slug === catId);
  if (cat) list = list.filter((p) => p.category_id === cat.id);
  if (q) {
    const s = q.toLowerCase();
    list = list.filter((p) => (p.name + p.brand + p.barcode).toLowerCase().includes(s));
  }
  const title = cat ? cat.name : (q ? `Arama: ${q}` : 'Tüm ürünler');
  return `<div class="wrap" style="padding-top:16px">
    <div class="section-title"><h2>${title}</h2><span>${list.length} ürün</span></div>
    <div class="grid">${list.map(productCard).join('') || '<div class="empty">Ürün yok</div>'}</div>
  </div>`;
}

function viewProduct(id) {
  const p = state.products.find((x) => x.id === Number(id));
  if (!p) return `<div class="empty">Ürün bulunamadı</div>`;
  return `<div class="wrap" style="padding-top:16px"><div class="detail">
    <img src="${p.image}" alt="">
    <div>
      <div class="brand-l">${p.brand || ''}</div>
      <h1>${p.name}</h1>
      <p style="color:var(--muted)">${p.description || ''}</p>
      <p>Barkod: <b>${p.barcode || '-'}</b> · Stok: <b>${p.stock}</b></p>
      <p class="price" style="font-size:28px">${p.price_text}</p>
      <div class="qty"><button data-q="-">−</button><input id="qty" value="1"><button data-q="+">+</button></div>
      <p><button class="btn" id="addOne">Sepete ekle ve sipariş ver</button></p>
    </div>
  </div></div>`;
}

function viewCart() {
  const total = state.cart.reduce((s, i) => s + i.price * i.qty, 0);
  if (!state.cart.length) return `<div class="empty">Sepetiniz boş</div>`;
  return `<div class="wrap" style="padding-top:16px">
    <h2>Sepetim</h2>
    ${state.cart.map((i, idx) => `<div class="cart-line">
      <img src="${i.image}" alt="">
      <div><b>${i.name}</b><div>${money(i.price)} × ${i.qty}</div>
        <div class="qty"><button data-cq="${idx}" data-d="-">−</button><span>${i.qty}</span><button data-cq="${idx}" data-d="+">+</button></div>
      </div>
      <b>${money(i.price * i.qty)}</b>
    </div>`).join('')}
    <div class="total-box form">
      <div>Toplam: <b style="font-size:22px">${money(total)}</b></div>
      <p class="hint">${APP.bank || ''}</p>
      <label>Sipariş notu<textarea id="note" placeholder="Teslimat veya fatura notu"></textarea></label>
      <button class="btn block" id="place">Siparişi gönder (ödeme sonra)</button>
    </div>
  </div>`;
}

function viewAuth() {
  return `<div class="auth">
    <h2>Bayi girişi</h2>
    <div class="form">
      <label>Telefon<input id="phone" value="05551234567"></label>
      <label>Şifre<input id="pass" type="password" value="Bayi123!"></label>
      <button class="btn block" id="doLogin">Giriş yap</button>
      <div class="hint">Örnek bayi: 0555 123 45 67 / Bayi123!<br>Patron paneli: <a href="${APP.admin}">admin girişi</a> (0555 000 00 00 / Patron123!)</div>
      <hr>
      <h3>Yeni bayi kaydı</h3>
      <label>Ad soyad<input id="rname"></label>
      <label>Firma<input id="rcompany"></label>
      <label>Şehir<input id="rcity"></label>
      <label>Telefon<input id="rphone"></label>
      <label>Şifre<input id="rpass" type="password"></label>
      <button class="btn ghost block" id="doReg">Kayıt ol</button>
    </div>
  </div>`;
}

function viewAccount() {
  const u = state.user;
  if (!u) return viewAuth();
  return `<div class="wrap" style="padding-top:16px">
    <div class="auth" style="margin:0">
      <h2>${u.company || u.name}</h2>
      <p>${u.phone} · ${u.city || ''}</p>
      ${u.role === 'admin' ? `<p><a class="btn" href="${APP.admin}">Yönetim paneli</a></p>` : ''}
      <button class="btn ghost" id="out">Çıkış</button>
    </div>
    <h3 style="margin-top:18px">Siparişlerim</h3>
    <div id="myorders" class="empty">Yükleniyor…</div>
  </div>`;
}

async function render() {
  const hash = location.hash.replace(/^#\/?/, '');
  const [page, id] = hash.split('/');
  let html = '';
  if (!page || page === 'home') html = viewHome();
  else if (page === 'kategori') html = viewList(id);
  else if (page === 'ara') html = viewList(null, decodeURIComponent(id || $('#q').value || ''));
  else if (page === 'urun') html = viewProduct(id);
  else if (page === 'sepet') html = viewCart();
  else if (page === 'giris') html = viewAuth();
  else if (page === 'hesabim') html = viewAccount();
  else html = viewHome();
  $('#view').innerHTML = html;
  $$('.nav-inner [data-cat]').forEach((a) => a.classList.toggle('on', a.dataset.cat && hash.includes(a.dataset.cat)));
  bindView(page);
  if (page === 'hesabim' && state.user?.role === 'dealer') {
    try {
      const d = await api('my_orders');
      $('#myorders').innerHTML = d.orders.map(orderHtml).join('') || '<div class="empty">Henüz sipariş yok</div>';
    } catch (e) { $('#myorders').textContent = e.message; }
  }
}

function orderHtml(o) {
  return `<div class="order">
    <div style="display:flex;justify-content:space-between"><b>#${o.id}</b><span class="pill ${o.status}">${o.status_text}</span></div>
    <div>${o.total_text}</div>
    <small>${o.items.map(i => `${i.qty}× ${i.name}`).join('<br>')}</small>
  </div>`;
}

function bindView(page) {
  $$('[data-open]').forEach((el) => el.addEventListener('click', (e) => {
    if (e.target.closest('[data-add]')) return;
    location.hash = '#/urun/' + el.dataset.open;
  }));
  $$('[data-add]').forEach((el) => el.addEventListener('click', (e) => {
    e.stopPropagation();
    const p = state.products.find((x) => x.id === Number(el.dataset.add));
    if (p) addCart(p);
  }));
  const qty = $('#qty');
  $$('[data-q]').forEach((b) => b.addEventListener('click', () => {
    qty.value = String(Math.max(1, Number(qty.value) + (b.dataset.q === '+' ? 1 : -1)));
  }));
  $('#addOne')?.addEventListener('click', () => {
    const p = state.products.find((x) => location.hash.endsWith('/' + x.id));
    addCart(p, Number(qty.value || 1));
    location.hash = '#/sepet';
  });
  $$('[data-cq]').forEach((b) => b.addEventListener('click', () => {
    const i = Number(b.dataset.cq);
    state.cart[i].qty += b.dataset.d === '+' ? 1 : -1;
    if (state.cart[i].qty < 1) state.cart.splice(i, 1);
    saveCart();
    render();
  }));
  $('#place')?.addEventListener('click', async () => {
    try {
      const d = await api('order_create', { body: { items: state.cart.map(({ id, qty }) => ({ id, qty })), note: $('#note').value } });
      state.cart = [];
      saveCart();
      toast(d.message);
      location.hash = '#/hesabim';
    } catch (e) { toast(e.message); }
  });
  $('#doLogin')?.addEventListener('click', async () => {
    try {
      const d = await api('login', { body: { phone: $('#phone').value, password: $('#pass').value } });
      state.user = d.user;
      setNav();
      location.hash = d.user.role === 'admin' ? '' : '#/home';
      if (d.user.role === 'admin') location.href = APP.admin;
      else render();
    } catch (e) { toast(e.message); }
  });
  $('#doReg')?.addEventListener('click', async () => {
    try {
      const d = await api('register', { body: { name: $('#rname').value, company: $('#rcompany').value, city: $('#rcity').value, phone: $('#rphone').value, password: $('#rpass').value } });
      state.user = d.user;
      setNav();
      location.hash = '#/home';
      render();
    } catch (e) { toast(e.message); }
  });
  $('#out')?.addEventListener('click', async () => {
    await api('logout', { method: 'POST', body: {} });
    state.user = null;
    setNav();
    location.hash = '#/giris';
    render();
  });
  if (!page || page === 'home') startHeroRotate();
}

let heroTimer = null;
let heroOnEnd = null;
function startHeroRotate() {
  if (heroTimer) {
    clearInterval(heroTimer);
    heroTimer = null;
  }
  const track = $('#heroTrack');
  const slides = $$('.hero-slide');
  if (!track || slides.length < 2) return;
  if (heroOnEnd) track.removeEventListener('transitionend', heroOnEnd);
  const last = slides.length - 1;
  let i = 0;
  const apply = (n, instant) => {
    if (instant) {
      track.style.transition = 'none';
      track.style.transform = 'translateX(-' + (n * 100) + '%)';
      void track.offsetHeight;
      track.style.transition = '';
    } else {
      track.style.transition = '';
      track.style.transform = 'translateX(-' + (n * 100) + '%)';
    }
    i = n;
  };
  heroOnEnd = (e) => {
    if (e.target !== track) return;
    if (i >= last) apply(0, true);
  };
  track.addEventListener('transitionend', heroOnEnd);
  heroTimer = setInterval(() => apply(i + 1, false), 4200);
}

function bindChrome() {
  $('#searchForm').addEventListener('submit', (e) => {
    e.preventDefault();
    location.hash = '#/ara/' + encodeURIComponent($('#q').value);
  });
  $('#cartBtn').onclick = () => location.hash = '#/sepet';
  $$('.bottom-nav [data-go]').forEach((b) => b.addEventListener('click', () => { location.hash = b.dataset.go; }));
}

async function boot() {
  bindChrome();
  saveCart();
  const d = await api('boot');
  state.user = d.user;
  state.cats = d.categories;
  state.products = d.products;
  APP.bank = d.site.bank;
  const nav = $('.nav-inner');
  nav.innerHTML = `<button data-cat="" onclick="location.hash='#/home'">Tüm Ürünler</button>` +
    state.cats.map((c) => `<button data-cat="${c.slug}" onclick="location.hash='#/kategori/${c.slug}'">${c.name}</button>`).join('');
  setNav();
  render();
  window.addEventListener('hashchange', render);
}

boot().catch((e) => { $('#view').innerHTML = `<div class="empty">${e.message}</div>`; });

if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register(APP.base + '/sw.js', { scope: APP.base + '/' }).catch(() => {});
}
