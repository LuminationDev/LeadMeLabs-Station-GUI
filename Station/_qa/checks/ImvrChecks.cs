using System.Collections.Generic;

namespace Station._qa.checks;

public class ImvrChecks
{
    public List<QaCheck> RunQa(string labType)
    {
        return SessionController.VrHeadset.GetStatusManager().VrQaChecks();
    }
}