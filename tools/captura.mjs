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

// Captura de una página con Edge headless por CDP: espera de verdad (las páginas con SSE nunca
// terminan de «cargar»), recoge los errores de consola y emula un móvil.
// Uso: node tools/captura.mjs <url> <salida.png> [ancho] [alto] [esperaMs] [js-antes-de-la-foto]
import { spawn } from "node:child_process";
import { writeFileSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const [url, salida, ancho = "412", alto = "915", espera = "3000", guion = ""] = process.argv.slice(2);
const edge = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const puerto = 9333;
const perfil = mkdtempSync(join(tmpdir(), "modosofa-captura-"));
const proceso = spawn(edge, ["--headless=new", "--disable-gpu", `--remote-debugging-port=${puerto}`, `--user-data-dir=${perfil}`, "about:blank"]);

const dormir = ms => new Promise(r => setTimeout(r, ms));
let destinos;
for (let i = 0; i < 40 && !destinos; i++) {
  try { destinos = await (await fetch(`http://127.0.0.1:${puerto}/json`)).json(); } catch { await dormir(250); }
}
const pagina = destinos.find(d => d.type === "page");
const ws = new WebSocket(pagina.webSocketDebuggerUrl);
await new Promise(r => ws.onopen = r);

let id = 0;
const pendientes = new Map();
const consola = [];
ws.onmessage = e => {
  const m = JSON.parse(e.data);
  if (m.id && pendientes.has(m.id)) { pendientes.get(m.id)(m.result); pendientes.delete(m.id); }
  if (m.method === "Runtime.consoleAPICalled") consola.push(`${m.params.type}: ${m.params.args.map(a => a.value ?? a.description).join(" ")}`);
  if (m.method === "Runtime.exceptionThrown") consola.push(`EXCEPCIÓN: ${m.params.exceptionDetails.exception?.description ?? m.params.exceptionDetails.text}`);
};
const cdp = (method, params = {}) => new Promise(r => { const n = ++id; pendientes.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });

await cdp("Runtime.enable");
await cdp("Page.enable");
await cdp("Emulation.setDeviceMetricsOverride", { width: +ancho, height: +alto, deviceScaleFactor: 1, mobile: +ancho < 600 });
await cdp("Page.navigate", { url });
await dormir(+espera);
if (guion) { await cdp("Runtime.evaluate", { expression: guion }); await dormir(800); }

const r = await cdp("Runtime.evaluate", { expression: "document.body.scrollWidth + 'x' + document.body.scrollHeight", returnByValue: true });
console.log("tamaño del contenido:", r.result.value, "· viewport:", `${ancho}x${alto}`);
const foto = await cdp("Page.captureScreenshot", { format: "png" });
writeFileSync(salida, Buffer.from(foto.data, "base64"));
console.log(consola.length ? consola.join("\n") : "(consola sin errores)");
ws.close();
proceso.kill();
process.exit(0);
