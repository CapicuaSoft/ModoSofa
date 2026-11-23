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

// Panel del móvil (PWA).
(() => {
  const $ = id => document.getElementById(id);
  const ICONOS = {
    "juego.cerrar": "M6 6l12 12M18 6 6 18",
    "juego.enfocar": "M12 5a7 7 0 1 0 0 14 7 7 0 0 0 0-14zm0 4a3 3 0 1 1 0 6 3 3 0 0 1 0-6z",
    "volumen.subir": "M4 9h4l5-4v14l-5-4H4zm12-1v8m-3-4h6",
    "volumen.bajar": "M4 9h4l5-4v14l-5-4H4zm9 3h6",
    "volumen.silenciar": "M4 9h4l5-4v14l-5-4H4zm11 0 5 6m0-6-5 6",
    "steam.bigpicture": "M3 6h18v11H3zM8 20h8",
    "esde.abrir": "M7 8h10a4 4 0 0 1 0 8H7a4 4 0 0 1 0-8zm2 2v4m-2-2h4m4 0h.01m2-1h.01",
    "steam.cerrar": "M3 6h18v11H3zm6 3 6 6m0-6-6 6",
    "overlay.alternar": "M4 5h16v14H4zm3 4h10M7 13h6",
    "guardian.alternar": "M12 3 5 6v6c0 4 3 7.5 7 9 4-1.5 7-5 7-9V6z",
    "tele.encender": "M12 3v8m-5-5a8 8 0 1 0 10 0",
    "tele.apagar": "M3 6h18v11H3zm9 3v4",
    "equipo.suspender": "M20 14A8 8 0 0 1 10 4a8 8 0 1 0 10 10z",
    "equipo.reiniciar": "M4 12a8 8 0 1 0 2.3-5.7M4 4v4h4",
    "equipo.apagar": "M12 3v8m-5-5a8 8 0 1 0 10 0",
  };

  let estado = null;
  let filtroLog = "";
  const entradasLog = [];

  function aviso(texto, error = false) {
    const a = $("aviso");
    a.textContent = texto;
    a.classList.toggle("error", error);
    a.classList.add("visible");
    clearTimeout(aviso.t);
    aviso.t = setTimeout(() => a.classList.remove("visible"), 2600);
  }

  function confirmar(texto) {
    return new Promise(resolver => {
      const d = $("confirmar");
      $("confirmar-texto").textContent = texto;
      d.onclose = () => resolver(d.returnValue === "si");
      d.showModal();
    });
  }

  async function ejecutar(a) {
    if (a.id === "juego.cerrar" && estado?.tipoActivo === "Juego") {
      // Un juego de Steam no guarda al cerrarlo a la fuerza.
      if (!(await confirmar(`¿Cerrar «${estado.activo}»? Se perderá lo que no hayas guardado.`))) return;
      a = { id: "juego.cerrar.confirmado" };
    } else if (a.peligrosa && !(await confirmar(`¿${a.nombre} el equipo?`))) return;
    try {
      await ModoSofa.accion(a.id);
      if (navigator.vibrate) navigator.vibrate(25);
    } catch (e) { manejarError(e); }
  }

  function manejarError(e) {
    if (e.noAutorizado) { mostrarVista("vincular"); return; }
    aviso("Sin conexión con el PC", true);
  }

  // --- Estado ---------------------------------------------------------------------------

  function pintarEstado(e) {
    estado = e;
    const sofa = e.modo === "sofa";
    $("modo").textContent = sofa ? "Modo sofá" : "Modo trabajo";
    $("modo").className = "insignia " + (sofa ? "sofa" : "trabajo");
    $("desde").textContent = sofa ? ModoSofa.hace(e.desde) : "";
    $("tele").textContent = e.teleConectada ? "Conectada" : "No conectada";
    $("audio").textContent = e.audio || "—";
    const tipo = ModoSofa.nombresActivo[e.tipoActivo] || "";
    $("activo").textContent = e.activo ? (tipo && tipo !== e.activo ? `${e.activo} · ${tipo}` : e.activo) : "Nada";
    $("mandos").textContent = e.mandos ? `${e.mandos} conectado${e.mandos > 1 ? "s" : ""}` : "Ninguno";
    $("ocupado").hidden = !e.ocupado;
    $("ocupado-texto").textContent = e.ocupado || "";
    $("principal").textContent = sofa ? "Volver a trabajar" : "Activar modo sofá";
    $("acerca-version").textContent = "versión " + e.version;
    $("acerca-actualizacion").textContent = e.actualizacion || "";
    $("acerca-actualizar").hidden = !e.versionNueva;
    if (e.versionNueva) $("acerca-actualizar").textContent = "Actualizar el PC a la " + e.versionNueva.version;
    const nueva = e.versionNueva;
    $("version-nueva").hidden = !nueva;
    if (nueva) {
      $("version-nueva").href = "#";
      $("version-nueva").onclick = ev => { ev.preventDefault(); mostrarVista("ajustes"); };
      $("version-nueva-texto").textContent = `${nueva.version} (tienes la ${e.version}). Se instala sola en modo trabajo, o desde Ajustes.`;
    }
    $("principal").classList.toggle("sofa", sofa);
    document.querySelectorAll('[data-id="guardian.alternar"] .sub')
      .forEach(s => s.textContent = e.guardian ? "Activo" : "En pausa");
  }

  async function pintarAcciones() {
    const acciones = await ModoSofa.api("/acciones");
    const grupos = new Map();
    for (const a of acciones) {
      if (a.id.startsWith("modo.") || a.id.startsWith("app.") || a.id.endsWith(".confirmado")) continue; // botón principal, «Acerca de» y confirmaciones
      if (!grupos.has(a.grupo)) grupos.set(a.grupo, []);
      grupos.get(a.grupo).push(a);
    }
    const cont = $("grupos");
    cont.replaceChildren();
    for (const [grupo, lista] of grupos) {
      const s = document.createElement("section");
      s.className = "grupo";
      s.innerHTML = `<h2>${grupo}</h2><div class="rejilla"></div>`;
      for (const a of lista) {
        const b = document.createElement("button");
        b.className = "accion" + (a.peligrosa ? " peligro" : "");
        b.dataset.id = a.id;
        b.innerHTML = `<svg viewBox="0 0 24 24"><path d="${ICONOS[a.id] || "M5 12h14"}"/></svg>
          <span>${a.nombre}</span>${a.id === "guardian.alternar" ? '<span class="sub"></span>' : ""}`;
        b.onclick = () => ejecutar(a);
        s.lastElementChild.append(b);
      }
      cont.append(s);
    }
    if (estado) pintarEstado(estado);
  }

  // --- Log ------------------------------------------------------------------------------

  function filaLog(e) {
    const li = document.createElement("li");
    li.className = "nivel-" + e.nivel;
    li.dataset.nivel = e.nivel;
    const hora = new Date(e.hora).toLocaleTimeString("es-ES", { hour12: false });
    li.innerHTML = `<time>${hora}</time><span class="msj"></span>`;
    li.querySelector(".msj").textContent = e.mensaje + (e.excepcion ? "\n" + e.excepcion.split("\n")[0] : "");
    return li;
  }

  function visible(e) {
    return !filtroLog || (filtroLog === "WRN" ? ["WRN", "ERR", "FTL"].includes(e.nivel) : ["ERR", "FTL"].includes(e.nivel));
  }

  function pintarLog() {
    const ol = $("log");
    ol.replaceChildren(...entradasLog.filter(visible).slice(-300).reverse().map(filaLog));
  }

  function nuevaEntrada(e) {
    entradasLog.push(e);
    if (entradasLog.length > 1000) entradasLog.shift();
    if (visible(e)) $("log").prepend(filaLog(e));
    if (e.nivel === "ERR") aviso(e.mensaje, true);
  }

  // --- Ajustes --------------------------------------------------------------------------

  async function cargarDiagnostico() {
    const d = await ModoSofa.api("/diagnostico");
    const lista = (titulo, items) => `<h3>${titulo}</h3><ul>${items.join("")}</ul>`;
    const esc = t => String(t ?? "").replace(/[&<>]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" })[c]);
    $("diagnostico").innerHTML =
      lista("Pantallas conectadas", (Array.isArray(d.pantallas) ? d.pantallas : []).map(p =>
        `<li><code>${esc(p.nombre)}</code>${p.activa ? " · activa" : ""}</li>`)) +
      `<p>Tele («${esc(d.tele.fragmento)}»): <b>${d.tele.conectada === true ? "detectada" : "no detectada"}</b></p>` +
      lista("Salidas de audio", (Array.isArray(d.audio) ? d.audio : []).map(a =>
        `<li><code>${esc(a.nombre)}</code>${a.porDefecto ? " · actual" : ""}</li>`)) +
      `<p>Audio sofá: «${esc(d.audioSofa)}»</p>` +
      `<p>Steam: <code>${esc(d.steam)}</code></p>` +
      `<p>ES-DE: ${d.esde.existe ? "encontrado" : "<b>no encontrado</b>"}</p>` +
      `<p>RetroArch: ${d.retroArch.abierto ? "abierto · comandos de red: " + esc(d.retroArch.comandosDeRed) : "cerrado"}</p>` +
      `<p>Mandos: ${d.mandos} · Versión ${esc(d.version)}</p>`;
  }

  async function cargarConfig() {
    $("config").value = await ModoSofa.api("/config", { texto: true });
  }

  async function guardarConfig() {
    $("config-error").textContent = "";
    try { JSON.parse($("config").value.replace(/^\s*\/\/.*$/gm, "")); }
    catch (e) { $("config-error").textContent = "JSON no válido: " + e.message; return; }
    try {
      await ModoSofa.api("/config", { method: "PUT", body: $("config").value, headers: { "Content-Type": "application/json" } });
      aviso("Guardado. El agente se está reiniciando…");
    } catch (e) { $("config-error").textContent = e.message; }
  }

  // --- Navegación -----------------------------------------------------------------------

  function mostrarVista(nombre) {
    document.querySelectorAll(".vista").forEach(v => v.hidden = v.id !== "vista-" + nombre);
    document.querySelectorAll("#pestanas button").forEach(b => b.classList.toggle("activa", b.dataset.vista === nombre));
    $("pestanas").hidden = nombre === "vincular";
    if (nombre === "ajustes") { cargarDiagnostico().catch(manejarError); cargarConfig().catch(manejarError); }
  }

  document.querySelectorAll("#pestanas button").forEach(b => b.onclick = () => mostrarVista(b.dataset.vista));
  document.querySelectorAll("#filtros .chip").forEach(c => c.onclick = () => {
    document.querySelectorAll("#filtros .chip").forEach(x => x.classList.toggle("activo", x === c));
    filtroLog = c.dataset.nivel;
    pintarLog();
  });
  $("principal").onclick = () => ejecutar({ id: estado?.modo === "sofa" ? "modo.trabajo" : "modo.sofa" });
  $("vaciar-log").onclick = async () => { await ModoSofa.api("/log", { method: "DELETE" }); entradasLog.length = 0; pintarLog(); };
  $("acerca-buscar").onclick = () => ejecutar({ id: "app.buscar" });
  $("acerca-actualizar").onclick = () => ejecutar({ id: "app.actualizar" });
  $("recargar-diag").onclick = () => cargarDiagnostico().catch(manejarError);
  $("guardar-config").onclick = guardarConfig;
  $("desvincular").onclick = async () => {
    if (await confirmar("¿Olvidar la vinculación de este móvil?")) { ModoSofa.olvidar(); mostrarVista("vincular"); }
  };

  // --- Arranque -------------------------------------------------------------------------

  async function arrancar() {
    if (!ModoSofa.token) { mostrarVista("vincular"); return; }
    try {
      pintarEstado(await ModoSofa.api("/estado"));
      await pintarAcciones();
      (await ModoSofa.api("/log?n=300")).forEach(e => entradasLog.push(e));
      pintarLog();
    } catch (e) { manejarError(e); if (e.noAutorizado) return; }

    ModoSofa.eventos({
      estado: pintarEstado,
      log: nuevaEntrada,
      accion: a => { if (a.fase === "fin" && !a.correcta) aviso(a.error || "La acción falló", true); },
    }, conectado => {
      $("conexion").classList.toggle("ok", conectado);
      $("conexion").title = conectado ? "Conectado" : "Sin conexión";
    });
    setInterval(() => estado && pintarEstado(estado), 30000); // refresca el «hace X min»
  }

  // --- Dentro de la app Android ---------------------------------------------------------
  // La app expone window.ModoSofaApp: se le pasa la MAC del PC para que pueda encenderlo por
  // Wake-on-LAN cuando el PC está apagado (y la web, por tanto, no carga).
  async function integrarApp() {
    const app = window.ModoSofaApp;
    if (!app) return;
    $("tarjeta-app").hidden = false;
    $("app-ajustes").onclick = () => app.abrirAjustes();
    $("app-acerca").onclick = () => app.acercaDe();
    try {
      const d = await ModoSofa.api("/diagnostico");
      const red = Array.isArray(d.red) ? d.red : [];
      const elegida = red.find(n => n.ip === location.hostname) || red.find(n => n.cable) || red[0];
      if (elegida) app.recordarMac(elegida.mac);
    } catch { /* sin conexión: ya lo hará otra vez */ }
  }

  if ("serviceWorker" in navigator && isSecureContext) navigator.serviceWorker.register("sw.js").catch(() => {});
  arrancar().then(integrarApp);
})();
