using System.Collections.Generic;

namespace Station._interfaces;

public enum Variant
{
    Vr,
    Content
}

public interface IProfile
{
    public Variant GetVariant();

    void StartSession();

    List<string> GetProcessesToQuery();

    void MinimizeSoftware(int attemptLimit);
}
