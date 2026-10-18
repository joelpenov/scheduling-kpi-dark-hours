using Xunit.Abstractions;

public class WeeklyKpiTests
{
    private readonly ITestOutputHelper _out;
    public WeeklyKpiTests(ITestOutputHelper output) => _out = output;

    private static readonly DateTime From = new(2026, 10, 12, 0, 0, 0);
    private static readonly DateTime To = new(2026, 10, 19, 0, 0, 0);
    private static DateTime D(string s) => DateTime.Parse(s);

    private static (double scheduled, double covered, double dark) Calc(
        IEnumerable<Shift> shifts, IEnumerable<Coverage> coverage, string location = "Wichita")
        => WeeklyKpi.Calculate(location, From, To, shifts, coverage);

    [Fact]
    public void Rule3_OverlappingShifts_UnionNotSum()
    {
        var shifts = new[]
        {
            new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T16:00"), false),
            new Shift(2, "Wichita", D("2026-10-12T12:00"), D("2026-10-12T20:00"), false),
        };
        var r = Calc(shifts, Array.Empty<Coverage>());
        Assert.Equal(12.00, r.scheduled, 2);
    }

    [Fact]
    public void Rule2_RemovedShiftInInput_ContributesZero()
    {
        var shifts = new[]
        {
            new Shift(3, "Wichita", D("2026-10-13T09:00"), D("2026-10-13T17:00"), true),
        };
        var r = Calc(shifts, Array.Empty<Coverage>());
        Assert.Single(shifts);
        Assert.Equal(0.00, r.scheduled, 2);
        Assert.Equal(0.00, r.dark, 2);
    }

    [Fact]
    public void Rule4_DuplicateCoverageRows_CountedOnce()
    {
        var shifts = new[] { new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T16:00"), false) };
        var coverage = new[]
        {
            new Coverage(101, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T12:00")),
            new Coverage(102, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T12:00")),
        };
        var r = Calc(shifts, coverage);
        Assert.Equal(4.00, r.covered, 2);
        Assert.Equal(4.00, r.dark, 2);
    }

    [Fact]
    public void Rule6_ShiftCrossingWindowStart_ClippedToFour()
    {
        var shifts = new[] { new Shift(5, "Memphis", D("2026-10-11T22:00"), D("2026-10-12T04:00"), false) };
        var r = Calc(shifts, Array.Empty<Coverage>(), "Memphis");
        Assert.Equal(4.00, r.scheduled, 2);
    }

    [Fact]
    public void Rule4_CoverageOutsideShift_ClippedToShift()
    {
        var shifts = new[] { new Shift(7, "Memphis", D("2026-10-16T08:00"), D("2026-10-16T12:00"), false) };
        var coverage = new[] { new Coverage(107, "Memphis", D("2026-10-16T07:00"), D("2026-10-16T09:00")) };
        var r = Calc(shifts, coverage, "Memphis");
        Assert.Equal(1.00, r.covered, 2);
        Assert.Equal(3.00, r.dark, 2);
    }

    [Fact]
    public void Rule5_CoverageLargerThanScheduled_DarkIsZeroNeverNegative()
    {
        var shifts = new[] { new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T10:00"), false) };
        var coverage = new[] { new Coverage(101, "Wichita", D("2026-10-12T06:00"), D("2026-10-12T14:00")) };
        var r = Calc(shifts, coverage);
        Assert.Equal(2.00, r.scheduled, 2);
        Assert.Equal(2.00, r.covered, 2);
        Assert.Equal(0.00, r.dark, 2);
        Assert.True(r.dark >= 0);
    }

    [Fact]
    public void Rule1_TouchingIntervals_AdjacentNotOverlapping()
    {
        var shifts = new[]
        {
            new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T12:00"), false),
            new Shift(2, "Wichita", D("2026-10-12T12:00"), D("2026-10-12T16:00"), false),
        };
        var r = Calc(shifts, Array.Empty<Coverage>());
        Assert.Equal(8.00, r.scheduled, 2);
    }

    [Fact]
    public void Rule7_MinutePrecision_FortyFiveMinutesIsPointSevenFive()
    {
        var shifts = new[] { new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T08:45"), false) };
        var r = Calc(shifts, Array.Empty<Coverage>());
        Assert.Equal(0.75, r.scheduled, 2);
    }

    [Fact]
    public void Golden_FullFixture_PerLocationAndTotal()
    {
        var shifts = new[]
        {
            new Shift(1, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T16:00"), false),
            new Shift(2, "Wichita", D("2026-10-12T12:00"), D("2026-10-12T20:00"), false),
            new Shift(3, "Wichita", D("2026-10-13T09:00"), D("2026-10-13T17:00"), true),
            new Shift(4, "Wichita", D("2026-10-14T22:00"), D("2026-10-15T06:00"), false),
            new Shift(5, "Memphis", D("2026-10-11T22:00"), D("2026-10-12T04:00"), false),
            new Shift(6, "Memphis", D("2026-10-18T20:00"), D("2026-10-19T04:00"), false),
            new Shift(7, "Memphis", D("2026-10-16T08:00"), D("2026-10-16T12:00"), false),
        };
        var coverage = new[]
        {
            new Coverage(101, "Wichita", D("2026-10-12T08:00"), D("2026-10-12T13:00")),
            new Coverage(102, "Wichita", D("2026-10-12T12:00"), D("2026-10-12T15:00")),
            new Coverage(103, "Wichita", D("2026-10-12T18:00"), D("2026-10-12T20:00")),
            new Coverage(104, "Wichita", D("2026-10-14T23:00"), D("2026-10-15T03:00")),
            new Coverage(105, "Memphis", D("2026-10-11T23:00"), D("2026-10-12T02:00")),
            new Coverage(106, "Memphis", D("2026-10-18T21:00"), D("2026-10-19T02:00")),
            new Coverage(107, "Memphis", D("2026-10-16T07:00"), D("2026-10-16T09:00")),
        };

        var wichita = Calc(shifts, coverage, "Wichita");
        var memphis = Calc(shifts, coverage, "Memphis");
        var total = (scheduled: wichita.scheduled + memphis.scheduled,
                     covered: wichita.covered + memphis.covered,
                     dark: wichita.dark + memphis.dark);

        _out.WriteLine($"{"Location",-12}{"Scheduled",10}{"Covered",10}{"Dark",10}");
        _out.WriteLine($"{"Wichita",-12}{wichita.scheduled,10:F2}{wichita.covered,10:F2}{wichita.dark,10:F2}");
        _out.WriteLine($"{"Memphis",-12}{memphis.scheduled,10:F2}{memphis.covered,10:F2}{memphis.dark,10:F2}");
        _out.WriteLine($"{"Total",-12}{total.scheduled,10:F2}{total.covered,10:F2}{total.dark,10:F2}");

        Assert.Equal(20.00, wichita.scheduled, 2);
        Assert.Equal(13.00, wichita.covered, 2);
        Assert.Equal(7.00, wichita.dark, 2);

        Assert.Equal(12.00, memphis.scheduled, 2);
        Assert.Equal(6.00, memphis.covered, 2);
        Assert.Equal(6.00, memphis.dark, 2);

        Assert.Equal(32.00, total.scheduled, 2);
        Assert.Equal(19.00, total.covered, 2);
        Assert.Equal(13.00, total.dark, 2);
    }
}
