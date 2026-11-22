// Service worker: la interfaz abre al instante aunque el PC tarde en contestar.
// La API nunca se cachea: siempre va a la red.
const VERSION = "modosofa-1";
const BASE = ["./", "index.html", "app.js", "comun.js", "estilo.css", "manifest.webmanifest", "icono-192.png", "icono-512.png"];

self.addEventListener("install", e => {
  e.waitUntil(caches.open(VERSION).then(c => c.addAll(BASE)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", e => {
  e.waitUntil(caches.keys()
    .then(claves => Promise.all(claves.filter(k => k !== VERSION).map(k => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener("fetch", e => {
  const url = new URL(e.request.url);
  if (url.pathname.startsWith("/api/") || e.request.method !== "GET") return;
  // Red primero (para recibir siempre la versión nueva); caché si el PC no responde.
  e.respondWith(
    fetch(e.request)
      .then(r => {
        const copia = r.clone();
        caches.open(VERSION).then(c => c.put(e.request, copia));
        return r;
      })
      .catch(() => caches.match(e.request))
  );
});
