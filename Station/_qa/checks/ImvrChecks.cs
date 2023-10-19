using System.Collections.Generic;

namespace Station._qa.checks;

public class ImvrChecks
{
    public List<QaCheck> RunQa()
    {
        return SessionController.vrHeadset.GetStatusManager().VrQaChecks();
    }
}