using System.Collections.Generic;

namespace Station.Components._interfaces;

public enum Variant
{
    Vr,
    Content
}

public interface IProfile
{
    public Variant GetVariant();

    void StartSession();
    void StartDevToolsSession();

    List<string> GetProcessesToQuery();

    void MinimizeSoftware(int attemptLimit);
}
