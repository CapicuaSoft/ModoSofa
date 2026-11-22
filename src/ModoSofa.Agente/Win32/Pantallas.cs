using System.Runtime.InteropServices;

namespace ModoSofa.Agente.Win32;

/// <summary>Pantalla conectada según la API CCD (QueryDisplayConfig).</summary>
public sealed record PantallaInfo(string Nombre, bool Activa, string Conector);

/// <summary>
/// Topología de pantallas con la API CCD: qué hay conectado (por el nombre EDID), dejar solo la
/// tele y restaurar lo que había. No requiere elevación.
/// </summary>
public sealed class Pantallas(ILogger<Pantallas> log)
{
    public IReadOnlyList<PantallaInfo> Conectadas()
    {
        var (rutas, _) = Consultar(QDC_ALL_PATHS);
        var vistas = new HashSet<(uint, int, uint)>();
        var lista = new List<PantallaInfo>();
        foreach (var r in rutas)
        {
            if (r.targetInfo.targetAvailable == 0) continue;
            var clave = (r.targetInfo.adapterId.LowPart, r.targetInfo.adapterId.HighPart, r.targetInfo.id);
            var activa = (r.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;
            if (!vistas.Add(clave))
            {
                if (activa)
                {
                    var i = lista.FindIndex(p => p.Conector == Conector(r));
                    if (i >= 0) lista[i] = lista[i] with { Activa = true };
                }
                continue;
            }
            lista.Add(new PantallaInfo(NombreDestino(r.targetInfo), activa, Conector(r)));
        }
        return lista;
    }

    public bool EstaConectada(string fragmento) =>
        !string.IsNullOrWhiteSpace(fragmento) &&
        Conectadas().Any(p => p.Nombre.Contains(fragmento, StringComparison.OrdinalIgnoreCase));

    /// <summary>Serializa la topología activa para poder restaurarla.</summary>
    public string Guardar()
    {
        var (rutas, modos) = Consultar(QDC_ONLY_ACTIVE_PATHS);
        var bytesRutas = MemoryMarshal.AsBytes(rutas.AsSpan()).ToArray();
        var bytesModos = MemoryMarshal.AsBytes(modos.AsSpan()).ToArray();
        return $"{rutas.Length}:{modos.Length}:{Convert.ToBase64String(bytesRutas)}:{Convert.ToBase64String(bytesModos)}";
    }

    /// <summary>Restaura una topología guardada; si ya no es aplicable (cambió un monitor), extiende.</summary>
    public void Restaurar(string? guardada)
    {
        if (!string.IsNullOrEmpty(guardada))
        {
            try
            {
                var partes = guardada.Split(':');
                var rutas = MemoryMarshal.Cast<byte, DISPLAYCONFIG_PATH_INFO>(Convert.FromBase64String(partes[2])).ToArray();
                var modos = MemoryMarshal.Cast<byte, DISPLAYCONFIG_MODE_INFO>(Convert.FromBase64String(partes[3])).ToArray();
                var r = SetDisplayConfig((uint)rutas.Length, rutas, (uint)modos.Length, modos,
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
                if (r == 0)
                {
                    log.LogInformation("Pantallas restauradas como estaban.");
                    return;
                }
                log.LogInformation("La topología guardada ya no es aplicable (código {Codigo}); se extiende el escritorio.", r);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "No se pudo leer la topología guardada.");
            }
        }

        var e = SetDisplayConfig(0, null, 0, null, SDC_APPLY | SDC_TOPOLOGY_EXTEND);
        if (e != 0) log.LogWarning("No se pudo extender el escritorio (código {Codigo}).", e);
        else log.LogInformation("Escritorio extendido a todas las pantallas conectadas.");
    }

    /// <summary>Deja activa solo la pantalla cuyo nombre contiene el fragmento (será la principal).</summary>
    public bool SoloPantalla(string fragmento)
    {
        var (activas, modosActivos) = Consultar(QDC_ONLY_ACTIVE_PATHS);
        var indice = Array.FindIndex(activas, r => Coincide(r, fragmento));

        if (indice >= 0)
        {
            // Conservar su modo (resolución y frecuencia) y moverla al origen para que sea la principal.
            var ruta = activas[indice];
            var modos = new List<DISPLAYCONFIG_MODE_INFO>();
            if (ruta.sourceInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
            {
                var origen = modosActivos[ruta.sourceInfo.modeInfoIdx];
                origen.sourceMode.position = new Nativo.POINT { X = 0, Y = 0 };
                modos.Add(origen);
                ruta.sourceInfo.modeInfoIdx = 0;
            }
            if (ruta.targetInfo.modeInfoIdx != DISPLAYCONFIG_PATH_MODE_IDX_INVALID)
            {
                modos.Add(modosActivos[ruta.targetInfo.modeInfoIdx]);
                ruta.targetInfo.modeInfoIdx = (uint)(modos.Count - 1);
            }
            var r = SetDisplayConfig(1, [ruta], (uint)modos.Count, modos.ToArray(),
                SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
            if (r == 0)
            {
                log.LogInformation("Pantalla única: {Nombre}.", NombreDestino(ruta.targetInfo));
                return true;
            }
            log.LogInformation("No se pudo conservar el modo de la tele (código {Codigo}); se deja elegir a Windows.", r);
        }

        // La tele no estaba activa (o falló lo anterior): activar su ruta y dejar que Windows elija el modo.
        var (todas, _) = Consultar(QDC_ALL_PATHS);
        var candidata = Array.FindIndex(todas, r => r.targetInfo.targetAvailable != 0 && Coincide(r, fragmento));
        if (candidata < 0)
        {
            log.LogWarning("No hay ninguna pantalla conectada que contenga «{Fragmento}».", fragmento);
            return false;
        }

        var nueva = todas[candidata];
        nueva.flags = DISPLAYCONFIG_PATH_ACTIVE;
        nueva.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        nueva.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
        var e = SetDisplayConfig(1, [nueva], 0, null,
            SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE);
        if (e != 0)
        {
            log.LogWarning("No se pudo dejar la tele como pantalla única (código {Codigo}).", e);
            return false;
        }
        log.LogInformation("Pantalla única: {Nombre}.", NombreDestino(nueva.targetInfo));
        return true;
    }

    private bool Coincide(DISPLAYCONFIG_PATH_INFO r, string fragmento) =>
        NombreDestino(r.targetInfo).Contains(fragmento, StringComparison.OrdinalIgnoreCase);

    private static string Conector(DISPLAYCONFIG_PATH_INFO r) =>
        $"{r.targetInfo.adapterId.LowPart:X}-{r.targetInfo.id}";

    private static (DISPLAYCONFIG_PATH_INFO[], DISPLAYCONFIG_MODE_INFO[]) Consultar(uint flags)
    {
        for (var intento = 0; intento < 3; intento++)
        {
            var e = GetDisplayConfigBufferSizes(flags, out var nRutas, out var nModos);
            if (e != 0) throw new InvalidOperationException($"GetDisplayConfigBufferSizes: {e}");
            var rutas = new DISPLAYCONFIG_PATH_INFO[nRutas];
            var modos = new DISPLAYCONFIG_MODE_INFO[nModos];
            e = QueryDisplayConfig(flags, ref nRutas, rutas, ref nModos, modos, IntPtr.Zero);
            if (e == ERROR_INSUFFICIENT_BUFFER) continue; // la topología cambió entre las dos llamadas
            if (e != 0) throw new InvalidOperationException($"QueryDisplayConfig: {e}");
            return (rutas[..(int)nRutas], modos[..(int)nModos]);
        }
        throw new InvalidOperationException("QueryDisplayConfig: la topología no deja de cambiar.");
    }

    private static string NombreDestino(DISPLAYCONFIG_PATH_TARGET_INFO destino)
    {
        var nombre = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                adapterId = destino.adapterId,
                id = destino.id,
            },
        };
        if (DisplayConfigGetDeviceInfo(ref nombre) != 0) return $"(desconocida {destino.id})";

        // El código de fabricante EDID va siempre: algunos monitores no anuncian nombre, y las
        // teles LG se identifican además por «GSM».
        var fabricante = ModoSofa.Nucleo.Edid.Fabricante(nombre.edidManufactureId);
        return string.IsNullOrEmpty(nombre.monitorFriendlyDeviceName)
            ? $"(sin nombre) [{fabricante}]"
            : $"{nombre.monitorFriendlyDeviceName} [{fabricante}]";
    }

    // --- API CCD ---------------------------------------------------------------------------

    private const uint QDC_ALL_PATHS = 0x1, QDC_ONLY_ACTIVE_PATHS = 0x2;
    private const uint SDC_TOPOLOGY_EXTEND = 0x4, SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x20, SDC_APPLY = 0x80,
        SDC_SAVE_TO_DATABASE = 0x200, SDC_ALLOW_CHANGES = 0x400;
    private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x1;
    private const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(uint numPathArrayElements, DISPLAYCONFIG_PATH_INFO[]? pathArray,
        uint numModeInfoArrayElements, DISPLAYCONFIG_MODE_INFO[]? modeInfoArray, uint flags);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL { public uint Numerator, Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_2DREGION { public uint cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width, height, pixelFormat;
        public Nativo.POINT position;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public uint infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        [FieldOffset(16)] public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetMode;
        [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
    }
}
