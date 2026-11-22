package com.capicua.modosofa

import android.annotation.SuppressLint
import android.app.AlertDialog
import android.content.Context
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.InputType
import android.view.View
import android.webkit.JavascriptInterface
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import kotlin.concurrent.thread

/**
 * Envoltorio del panel web de ModoSofá. Lo único que añade sobre la PWA es lo que un navegador no
 * puede hacer: encender el PC por Wake-on-LAN cuando está apagado (y la web, por tanto, no carga).
 */
class MainActivity : AppCompatActivity() {

    private lateinit var web: WebView
    private lateinit var refrescar: SwipeRefreshLayout
    private lateinit var sinConexion: View
    private lateinit var detalle: TextView
    private val prefs by lazy { getSharedPreferences("modosofa", Context.MODE_PRIVATE) }
    private val principal = Handler(Looper.getMainLooper())
    private var reintentos = 0

    private var enlace: String
        get() = prefs.getString("enlace", "") ?: ""
        set(v) = prefs.edit().putString("enlace", v.trim()).apply()

    private var mac: String
        get() = prefs.getString("mac", "") ?: ""
        set(v) = prefs.edit().putString("mac", v.trim().uppercase()).apply()

    private var difusion: String
        get() = prefs.getString("difusion", "255.255.255.255") ?: "255.255.255.255"
        set(v) = prefs.edit().putString("difusion", v.trim()).apply()

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        web = findViewById(R.id.web)
        refrescar = findViewById(R.id.refrescar)
        sinConexion = findViewById(R.id.sin_conexion)
        detalle = findViewById(R.id.detalle)

        web.settings.javaScriptEnabled = true
        web.settings.domStorageEnabled = true
        web.setBackgroundColor(0xFF14161F.toInt())
        web.addJavascriptInterface(Puente(), "ModoSofaApp")
        web.webViewClient = object : WebViewClient() {
            override fun onReceivedError(view: WebView, request: WebResourceRequest, error: WebResourceError) {
                if (request.isForMainFrame) mostrarSinConexion("No se pudo conectar (${error.description}).")
            }

            override fun onPageFinished(view: WebView, url: String) {
                refrescar.isRefreshing = false
            }
        }

        refrescar.setOnRefreshListener { cargar() }
        // Solo se refresca tirando desde arriba si la página no está desplazada.
        refrescar.setOnChildScrollUpCallback { _, _ -> web.scrollY > 0 }

        findViewById<Button>(R.id.encender).setOnClickListener { encenderPc() }
        findViewById<Button>(R.id.reintentar).setOnClickListener { cargar() }
        findViewById<Button>(R.id.ajustes).setOnClickListener { abrirAjustes() }

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (web.canGoBack()) web.goBack() else finish()
            }
        })

        if (enlace.isEmpty()) abrirAjustes() else cargar()
    }

    private fun cargar() {
        if (enlace.isEmpty()) {
            mostrarSinConexion("Falta el enlace del PC. Ábrelo desde Ajustes.")
            return
        }
        sinConexion.visibility = View.GONE
        web.loadUrl(enlace)
    }

    private fun mostrarSinConexion(texto: String) {
        refrescar.isRefreshing = false
        detalle.text = texto + if (mac.isEmpty()) "" else "\nSi está apagado, puedes encenderlo."
        sinConexion.visibility = View.VISIBLE
    }

    private fun encenderPc() {
        if (mac.isEmpty()) {
            Toast.makeText(this, "Falta la MAC del PC: se guarda sola la primera vez que conectas, o ponla en Ajustes.", Toast.LENGTH_LONG).show()
            return
        }
        thread {
            val resultado = runCatching { WakeOnLan.enviar(mac, difusion) }
            principal.post {
                if (resultado.isSuccess) {
                    Toast.makeText(this, "Encendiendo el PC…", Toast.LENGTH_SHORT).show()
                    detalle.text = "Encendiendo el PC… Se conectará solo en cuanto responda."
                    reintentos = 0
                    reintentarHastaConectar()
                } else {
                    Toast.makeText(this, "No se pudo enviar: ${resultado.exceptionOrNull()?.message}", Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    /** Tras encender, vuelve a probar cada 5 s durante 2 minutos. */
    private fun reintentarHastaConectar() {
        if (reintentos++ >= 24 || sinConexion.visibility != View.VISIBLE) return
        principal.postDelayed({
            if (sinConexion.visibility == View.VISIBLE) {
                cargar()
                reintentarHastaConectar()
            }
        }, 5000)
    }

    private fun abrirAjustes() {
        val margen = (16 * resources.displayMetrics.density).toInt()
        fun campo(pista: String, valor: String) = EditText(this).apply {
            hint = pista
            setText(valor)
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI
            isSingleLine = true
        }

        val campoEnlace = campo("Enlace del PC (Vincular móvil → Copiar enlace)", enlace)
        val campoMac = campo("MAC del PC (se rellena sola)", mac)
        val campoDifusion = campo("Dirección de difusión", difusion)

        val contenido = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(margen, margen / 2, margen, 0)
            addView(TextView(context).apply { text = "Enlace" })
            addView(campoEnlace)
            addView(TextView(context).apply { text = "Wake-on-LAN"; setPadding(0, margen, 0, 0) })
            addView(campoMac)
            addView(campoDifusion)
        }

        AlertDialog.Builder(this)
            .setTitle("Ajustes")
            .setView(contenido)
            .setPositiveButton("Guardar") { _, _ ->
                enlace = campoEnlace.text.toString()
                mac = campoMac.text.toString()
                difusion = campoDifusion.text.toString().ifBlank { "255.255.255.255" }
                cargar()
            }
            .setNegativeButton("Cancelar", null)
            .show()
    }

    /** Lo que la web puede pedirle a la app (window.ModoSofaApp). */
    inner class Puente {
        @JavascriptInterface
        fun recordarMac(valor: String) {
            if (Regex("^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$").matches(valor)) mac = valor
        }

        @JavascriptInterface
        fun abrirAjustes() {
            principal.post { this@MainActivity.abrirAjustes() }
        }
    }
}

object WakeOnLan {
    /** Paquete mágico: 6 × 0xFF y 16 × la MAC, por difusión al puerto 9. */
    fun enviar(mac: String, difusion: String) {
        val bytes = mac.split(':', '-').map { it.toInt(16).toByte() }.toByteArray()
        require(bytes.size == 6) { "MAC no válida" }
        val paquete = ByteArray(102)
        for (i in 0 until 6) paquete[i] = 0xFF.toByte()
        for (i in 1..16) System.arraycopy(bytes, 0, paquete, i * 6, 6)
        DatagramSocket().use { s ->
            s.broadcast = true
            val destino = InetAddress.getByName(difusion)
            repeat(3) { s.send(DatagramPacket(paquete, paquete.size, destino, 9)) }
        }
    }
}
