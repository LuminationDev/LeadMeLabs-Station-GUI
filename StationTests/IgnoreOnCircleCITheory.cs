using System;
using Xunit;

namespace StationTests;

public sealed class IgnoreOnCircleCITheory : TheoryAttribute
{
    public IgnoreOnCircleCITheory() {
        if(IsCircleCI()) {
            Skip = "Ignore when running on CircleCI";
        }
    }
    
    private static bool IsCircleCI()
        => Environment.GetEnvironmentVariable("CIRCLECI") != null;
}