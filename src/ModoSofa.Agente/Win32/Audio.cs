using System.Runtime.InteropServices;

namespace ModoSofa.Agente.Win32;

public sealed record DispositivoAudio(string Id, string Nombre, bool PorDefecto);

/// <summary>
/// Salida de audio por defecto y volumen con Core Audio. Cambiar el dispositivo por defecto usa
/// IPolicyConfig, una interfaz no documentada pero estable desde Windows 7 (la usa el propio panel
/// de sonido y herramientas como SoundSwitch o EarTrumpet).
/// </summary>
public sealed class Audio(ILogger<Audio> log)
{
    public IReadOnlyList<DispositivoAudio> Salidas()
    {
        var enumerador = Enumerador();
        var porDefecto = IdPorDefecto(enumerador);
        Comprobar(enumerador.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out var coleccion));
        Comprobar(coleccion.GetCount(out var n));
        var lista = new List<DispositivoAudio>();
        for (uint i = 0; i < n; i++)
        {
            Comprobar(coleccion.Item(i, out var dispositivo));
            Comprobar(dispositivo.GetId(out var id));
            lista.Add(new DispositivoAudio(id, NombreAmigable(dispositivo), id == porDefecto));
        }
        return lista;
    }

    public string? SalidaActual() => IdPorDefecto(Enumerador());

    public string? NombreActual()
    {
        var id = SalidaActual();
        return id is null ? null : Salidas().FirstOrDefault(d => d.Id == id)?.Nombre;
    }

    /// <summary>Pone como salida por defecto el primer dispositivo cuyo nombre contiene el fragmento.</summary>
    public bool CambiarA(string fragmento)
    {
        var destino = Salidas().FirstOrDefault(d => d.Nombre.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
        if (destino is null)
        {
            log.LogWarning("No hay ninguna salida de audio que contenga «{Fragmento}».", fragmento);
            return false;
        }
        CambiarPorId(destino.Id);
        log.LogInformation("Audio por: {Nombre}.", destino.Nombre);
        return true;
    }

    public void CambiarPorId(string id)
    {
        var politica = (IPolicyConfig)new CPolicyConfigClient();
        foreach (var rol in new[] { ERole.eConsole, ERole.eMultimedia, ERole.eCommunications })
            Comprobar(politica.SetDefaultEndpoint(id, rol));
    }

    public float? Volumen()
    {
        var v = VolumenPorDefecto();
        if (v is null) return null;
        Comprobar(v.GetMasterVolumeLevelScalar(out var nivel));
        return nivel;
    }

    public void FijarVolumen(float nivel)
    {
        var v = VolumenPorDefecto() ?? throw new InvalidOperationException("No hay salida de audio.");
        var contexto = Guid.Empty;
        Comprobar(v.SetMasterVolumeLevelScalar(Math.Clamp(nivel, 0f, 1f), ref contexto));
    }

    public void Pasos(int pasos)
    {
        var v = VolumenPorDefecto() ?? throw new InvalidOperationException("No hay salida de audio.");
        var contexto = Guid.Empty;
        if (pasos > 0) Comprobar(v.SetMute(false, ref contexto));
        for (var i = 0; i < Math.Abs(pasos); i++)
            Comprobar(pasos > 0 ? v.VolumeStepUp(ref contexto) : v.VolumeStepDown(ref contexto));
    }

    public bool AlternarSilencio()
    {
        var v = VolumenPorDefecto() ?? throw new InvalidOperationException("No hay salida de audio.");
        var contexto = Guid.Empty;
        Comprobar(v.GetMute(out var silenciado));
        Comprobar(v.SetMute(!silenciado, ref contexto));
        return !silenciado;
    }

    private static IAudioEndpointVolume? VolumenPorDefecto()
    {
        var enumerador = Enumerador();
        if (enumerador.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var dispositivo) != 0) return null;
        var iid = typeof(IAudioEndpointVolume).GUID;
        Comprobar(dispositivo.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var objeto));
        return (IAudioEndpointVolume)objeto;
    }

    private static IMMDeviceEnumerator Enumerador() => (IMMDeviceEnumerator)new MMDeviceEnumerator();

    private static string? IdPorDefecto(IMMDeviceEnumerator enumerador)
    {
        if (enumerador.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var d) != 0) return null;
        return d.GetId(out var id) == 0 ? id : null;
    }

    private static string NombreAmigable(IMMDevice dispositivo)
    {
        if (dispositivo.OpenPropertyStore(STGM_READ, out var almacen) != 0) return "(sin nombre)";
        var clave = PKEY_Device_FriendlyName;
        if (almacen.GetValue(ref clave, out var valor) != 0) return "(sin nombre)";
        try
        {
            return valor.vt == VT_LPWSTR ? Marshal.PtrToStringUni(valor.puntero) ?? "" : "(sin nombre)";
        }
        finally { PropVariantClear(ref valor); }
    }

    private static void Comprobar(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    // --- Core Audio -------------------------------------------------------------------------

    private const uint DEVICE_STATE_ACTIVE = 0x1;
    private const uint CLSCTX_ALL = 0x17;
    private const uint STGM_READ = 0;
    private const ushort VT_LPWSTR = 31;
    private static readonly PROPERTYKEY PKEY_Device_FriendlyName =
        new() { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr puntero;
    }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class CPolicyConfigClient;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instancia);
        [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid context);
        [PreserveSig] int VolumeStepDown(ref Guid context);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float increment);
    }

    /// <summary>Solo importa el orden de la tabla virtual: los diez primeros no se llaman nunca.</summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, ERole role);
        [PreserveSig] int SetEndpointVisibility();
    }
}
