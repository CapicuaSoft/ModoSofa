import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

// La firma vive en local.properties (no versionado), igual que en GameController: el almacén de
// claves es C:\Proyectos\Claves\capicua.jks y la ruta es relativa a este módulo.
val propiedadesLocales = Properties().apply {
    val f = rootProject.file("local.properties")
    if (f.exists()) f.inputStream().use { load(it) }
}
val hayFirma = propiedadesLocales.getProperty("KEYSTORE_PATH") != null

android {
    namespace = "com.capicua.modosofa"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.capicua.modosofa"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "0.1.0"
    }

    signingConfigs {
        if (hayFirma) {
            create("release") {
                storeFile = file(propiedadesLocales.getProperty("KEYSTORE_PATH"))
                storePassword = propiedadesLocales.getProperty("KEYSTORE_PASSWORD")
                keyAlias = propiedadesLocales.getProperty("KEY_ALIAS")
                keyPassword = propiedadesLocales.getProperty("KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            if (hayFirma) signingConfig = signingConfigs.getByName("release")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    lint {
        // El lint de AGP 8.5 se cae con el JDK 25 del JBR de Android Studio («> 25.0.4») antes de
        // analizar nada. No es un problema del código; se puede reactivar al subir de AGP.
        checkReleaseBuilds = false
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("androidx.swiperefreshlayout:swiperefreshlayout:1.1.0")
}
