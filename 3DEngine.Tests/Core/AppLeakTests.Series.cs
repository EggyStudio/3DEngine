using FluentAssertions;

namespace Engine.Tests.Core;

// The growth the leak tests judge, read from series of readings other runs took.
public sealed partial class AppLeakTests
{
    [Fact]
    public void The_Growth_Is_Read_From_The_Most_Of_Three_Readings_So_A_Trough_In_A_Flat_Series_Holds_And_A_Climb_Fails()
    {
        // A series of the offscreen test's private bytes on Windows, flat but for a trough at the
        // twentieth app, and a process that keeps 0.8 MB of each app from the same start.
        (int Apps, double Resident)[] trough =
            [(10, 156.5), (20, 80.0), (30, 159.2), (40, 164.0), (50, 163.9), (60, 164.1), (70, 166.6), (80, 168.7), (90, 165.9), (100, 169.6)];
        var climbing = Enumerable.Range(1, 10).Select(k => (Apps: 10 * k, Resident: 150 + 8.0 * k)).ToArray();

        (trough[^1].Resident - Cycled.BaseOf(trough, trough[1].Resident)).Should().BeApproximately(10.4, 1e-9, "a trough in a flat series holds nothing")
            .And.BeLessThan(50);
        (climbing[^1].Resident - Cycled.BaseOf(climbing, climbing[1].Resident)).Should().BeApproximately(56, 1e-9, "a climb of 0.8 MB an app keeps it")
            .And.BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void One_Step_Past_The_Thirtieth_App_That_Alone_Passes_The_Bound_Is_Forgiven_And_A_Leak_Of_Each_App_Or_A_Second_Step_Is_Not()
    {
        // Series of the offscreen tests' private bytes on Windows, without the held instance and
        // beside it, each with lavapipe's one allocation of an app's drawing kept from some fifty
        // apps on, the trough of the test above, a process that keeps 0.8 MB of each app, and one
        // that takes two such steps.
        (int Apps, double Resident)[] alone = [(10, 91.9), (20, 87.3), (30, 91.4), (40, 94.3), (50, 209.6), (60, 219.3)];
        (int Apps, double Resident)[] held = [(10, 98.4), (20, 89.4), (30, 94.3), (40, 92.4), (50, 99.1), (60, 212.2)];
        (int Apps, double Resident)[] trough =
            [(10, 156.5), (20, 80.0), (30, 159.2), (40, 164.0), (50, 163.9), (60, 164.1), (70, 166.6), (80, 168.7), (90, 165.9), (100, 169.6)];
        var climbing = Enumerable.Range(1, 10).Select(k => (Apps: 10 * k, Resident: 150 + 8.0 * k)).ToArray();
        (int Apps, double Resident)[] twice = [(10, 90), (20, 90), (30, 90), (40, 205), (50, 205), (60, 320)];
        double Growth((int Apps, double Resident)[] series) => Cycled.GrowthOf(series, series[1].Resident, series[^1].Resident);

        Cycled.StepOf(alone).Should().Be((50, 209.6 - 94.3));
        Growth(alone).Should().BeApproximately(219.3 - 91.9 - (209.6 - 94.3), 1e-9, "the step between the fortieth and the fiftieth app is the driver's")
            .And.BeLessThan(GrowthBound);
        Growth(held).Should().BeApproximately(212.2 - 98.4 - (212.2 - 99.1), 1e-9, "and so is the one between the fiftieth and the sixtieth")
            .And.BeLessThan(GrowthBound);
        Growth(trough).Should().BeApproximately(10.4, 1e-9, "a trough in a flat series holds no step").And.BeLessThan(GrowthBound);
        Growth(climbing).Should().BeApproximately(56, 1e-9, "a climb of 0.8 MB an app has no rise that alone passes the bound")
            .And.BeGreaterThanOrEqualTo(GrowthBound);
        Cycled.StepOf(twice).Should().BeNull("a second step is not the driver's one allocation");
        Growth(twice).Should().BeGreaterThanOrEqualTo(GrowthBound);
    }
}
