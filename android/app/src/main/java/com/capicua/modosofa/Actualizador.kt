// ModoSofá - Convierte el PC de trabajo en una consola de salón.
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

package com.capicua.modosofa

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.core.content.FileProvider
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest

/**
 * Autoactualización de la app: pregunta a GitHub por el último release (como mucho una vez al
 * día), descarga su APK, lo comprueba contra el SHA256SUMS.txt del release y se lo pasa al
 * instalador de Android, que es quien pide la confirmación. Un APK que no cuadra se borra.
 * Todo se llama desde un hilo secundario salvo [instalar].
 */
object Actualizador {
    private const val API = "https://api.github.com/repos/jbautistamartin/ModoSofa/releases/latest"
    private const val INTERVALO_MS = 24 * 60 * 60 * 1000L

    data class Novedad(val version: String, val pagina: String, val apkUrl: String, val apkNombre: String, val sumasUrl: String?)

    /** Toca preguntar: ha pasado un día desde la última vez. */
    fun toca(contexto: Context): Boolean =
        System.currentTimeMillis() - prefs(contexto).getLong("ultima", 0) > INTERVALO_MS

    /** Null si no hay nada más nuevo. Lanza si no se pudo preguntar. */
    fun buscar(contexto: Context): Novedad? {
        prefs(contexto).edit().putLong("ultima", System.currentTimeMillis()).apply()
        val conexion = (URL(API).openConnection() as HttpURLConnection).apply {
            connectTimeout = 15000
            readTimeout = 15000
            setRequestProperty("Accept", "application/vnd.github+json")
            // api.github.com responde 403 sin User-Agent.
            setRequestProperty("User-Agent", "ModoSofa-Android/${BuildConfig.VERSION_NAME}")
        }
        try {
            if (conexion.responseCode == 404) return null
            if (conexion.responseCode != 200) throw IllegalStateException("GitHub respondió HTTP ${conexion.responseCode}")
            val release = JSONObject(conexion.inputStream.bufferedReader().use { it.readText() })
            val etiqueta = release.optString("tag_name")
            if (!esMasNueva(etiqueta, BuildConfig.VERSION_NAME)) return null

            var apk: Pair<String, String>? = null
            var sumas: String? = null
            val assets = release.optJSONArray("assets")
            for (i in 0 until (assets?.length() ?: 0)) {
                val a = assets!!.getJSONObject(i)
                val nombre = a.optString("name")
                val url = a.optString("browser_download_url")
                if (nombre.endsWith(".apk", ignoreCase = true)) apk = nombre to url
                if (nombre.equals("SHA256SUMS.txt", ignoreCase = true)) sumas = url
            }
            val (nombre, url) = apk ?: return null
            return Novedad(etiqueta.trimStart('v', 'V'), release.optString("html_url"), url, nombre, sumas)
        } finally {
            conexion.disconnect()
        }
    }

    /** Descarga y verifica. Devuelve el fichero listo para instalar. */
    fun descargar(contexto: Context, novedad: Novedad, progreso: (Int) -> Unit): File {
        val sumasUrl = novedad.sumasUrl ?: throw IllegalStateException("El release no publica sumas SHA-256.")
        val carpeta = File(contexto.cacheDir, "actualizaciones").apply { deleteRecursively(); mkdirs() }
        val destino = File(carpeta, novedad.apkNombre)

        val conexion = URL(novedad.apkUrl).openConnection() as HttpURLConnection
        try {
            val total = conexion.contentLengthLong
            val digest = MessageDigest.getInstance("SHA-256")
            conexion.inputStream.use { entrada ->
                destino.outputStream().use { salida ->
                    val buffer = ByteArray(64 * 1024)
                    var leidos = 0L
                    while (true) {
                        val n = entrada.read(buffer)
                        if (n < 0) break
                        salida.write(buffer, 0, n)
                        digest.update(buffer, 0, n)
                        leidos += n
                        if (total > 0) progreso((leidos * 100 / total).toInt())
                    }
                }
            }
            val calculado = digest.digest().joinToString("") { "%02x".format(it) }
            val esperado = buscarHash(URL(sumasUrl).readText(), novedad.apkNombre)
            if (esperado == null || !esperado.equals(calculado, ignoreCase = true)) {
                destino.delete()
                throw IllegalStateException("El APK descargado no coincide con el SHA-256 publicado; se ha borrado.")
            }
            return destino
        } finally {
            conexion.disconnect()
        }
    }

    /** True si se lanzó el instalador; false si antes hay que autorizar «instalar apps desconocidas». */
    fun instalar(contexto: Context, apk: File): Boolean {
        if (!contexto.packageManager.canRequestPackageInstalls()) {
            contexto.startActivity(
                Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${contexto.packageName}"))
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            )
            return false
        }
        val uri = FileProvider.getUriForFile(contexto, "${contexto.packageName}.archivos", apk)
        contexto.startActivity(
            Intent(Intent.ACTION_VIEW)
                .setDataAndType(uri, "application/vnd.android.package-archive")
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        )
        return true
    }

    /** Como números: «1.10.0» es mayor que «1.9.0», aunque como texto no lo sea. */
    fun esMasNueva(publicada: String, instalada: String): Boolean {
        fun partes(v: String): List<Int>? = v.trim().trimStart('v', 'V').substringBefore('-').substringBefore('+')
            .split('.').map { it.toIntOrNull() ?: return null }
        val p = partes(publicada) ?: return false
        val i = partes(instalada) ?: return false
        for (k in 0 until maxOf(p.size, i.size)) {
            val a = p.getOrElse(k) { 0 }
            val b = i.getOrElse(k) { 0 }
            if (a != b) return a > b
        }
        return false
    }

    fun buscarHash(sumas: String, fichero: String): String? = sumas.lineSequence()
        .map { it.trim().split(Regex("\\s+"), limit = 2) }
        .firstOrNull { it.size == 2 && it[0].length == 64 && it[1].trimStart('*').equals(fichero, ignoreCase = true) }
        ?.get(0)

    private fun prefs(contexto: Context) = contexto.getSharedPreferences("actualizaciones", Context.MODE_PRIVATE)
}
