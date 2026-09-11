const BASE = self.location.pathname.replace(/\/sw\.js$/, '') || '';

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open('yilmaz-v5').then((cache) =>
      cache.addAll([
        BASE + '/',
        BASE + '/index.php',
        BASE + '/admin.php',
        BASE + '/assets/css/app.css?v=9',
        BASE + '/assets/js/app.js?v=9',
        BASE + '/assets/js/admin.js?v=4',
        BASE + '/assets/img/icon-192.png',
        BASE + '/assets/img/icon-512.png',
        BASE + '/manifest.webmanifest',
      ]).catch(() => {})
    )
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== 'yilmaz-v5').map((k) => caches.delete(k)))).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.pathname.includes('api.php')) {
    event.respondWith(fetch(req).catch(() => new Response(JSON.stringify({ ok: false, error: 'Çevrimdışı' }), { headers: { 'Content-Type': 'application/json' } })));
    return;
  }
  event.respondWith(
    fetch(req).then((res) => {
      const copy = res.clone();
      caches.open('yilmaz-v5').then((c) => c.put(req, copy)).catch(() => {});
      return res;
    }).catch(() => caches.match(req).then((r) => r || caches.match(BASE + '/')))
  );
});
