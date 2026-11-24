// ModoSofá (CapicuaSoft) - Convierte el PC de trabajo en una consola de salón.
// Copyright (C) 2026  José Luis Bautista Martín
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

// Comunicación con el agente, compartida por el panel del móvil y el overlay de la tele.
const ModoSofa = (() => {
  const CLAVE = "modosofa.token";

  // El QR trae el token en el fragmento (#t=…), que nunca viaja al servidor ni a los logs.
  const enUrl = new URLSearchParams(location.hash.slice(1)).get("t");
  if (enUrl) {
    try { localStorage.setItem(CLAVE, enUrl); } catch { /* sin almacenamiento */ }
    history.replaceState(null, "", location.pathname);
  }
  let token = enUrl;
  if (!token) { try { token = localStorage.getItem(CLAVE); } catch { token = null; } }

  async function api(ruta, opciones = {}) {
    const r = await fetch("/api" + ruta, {
      ...opciones,
      headers: { Authorization: "Bearer " + token, ...(opciones.headers || {}) },
    });
    if (r.status === 401) throw Object.assign(new Error("No autorizado"), { noAutorizado: true });
    if (!r.ok && r.status !== 202) throw new Error("Error " + r.status);
    const tipo = r.headers.get("content-type") || "";
    // { texto: true }: el contenido tal cual (p. ej. config.json, que lleva comentarios).
    return !opciones.texto && tipo.includes("json") ? r.json() : r.text();
  }

  /** Suscripción a los eventos en vivo. EventSource se reconecta solo. */
  function eventos(manejadores, alCambiarConexion) {
    const es = new EventSource("/api/eventos?t=" + encodeURIComponent(token));
    es.onopen = () => alCambiarConexion?.(true);
    es.onerror = () => alCambiarConexion?.(false);
    for (const [tipo, f] of Object.entries(manejadores))
      es.addEventListener(tipo, e => f(JSON.parse(e.data)));
    return es;
  }

  const accion = id => api("/acciones/" + encodeURIComponent(id), { method: "POST" });

  function olvidar() {
    try { localStorage.removeItem(CLAVE); } catch { /* nada */ }
    token = null;
  }

  function hace(fecha) {
    if (!fecha) return "";
    const min = Math.round((Date.now() - new Date(fecha)) / 60000);
    if (min < 1) return "ahora mismo";
    if (min < 60) return `hace ${min} min`;
    const h = Math.floor(min / 60);
    return `hace ${h} h ${min % 60} min`;
  }

  const nombresActivo = { Juego: "Juego", RetroArch: "RetroArch", Emulador: "Emulador", EsDe: "ES-DE", Steam: "Steam", Ninguno: "" };

  return { get token() { return token; }, api, eventos, accion, olvidar, hace, nombresActivo };
})();
