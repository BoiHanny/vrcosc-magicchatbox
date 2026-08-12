using Valve.VR;

namespace vrcosc_magicchatbox.Services.Vr;

public interface IOpenVrRuntime
{
    bool TryInit(out EVRInitError error);

    void Shutdown();

    CVRSystem? System { get; }

    CVRCompositor? Compositor { get; }
}

public sealed class OpenVrRuntime : IOpenVrRuntime
{
    private CVRSystem? _system;

    public bool TryInit(out EVRInitError error)
    {
        error = EVRInitError.None;
        _system = Valve.VR.OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

        if (error != EVRInitError.None)
            _system = null;

        return _system != null;
    }

    public void Shutdown()
    {
        _system = null;
        Valve.VR.OpenVR.Shutdown();
    }

    public CVRSystem? System => _system;

    public CVRCompositor? Compositor => _system == null ? null : Valve.VR.OpenVR.Compositor;
}
