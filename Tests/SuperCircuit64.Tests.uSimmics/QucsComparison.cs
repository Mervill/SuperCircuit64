using System;

namespace SuperCircuit64.Tests.uSimmics;

/// <summary>
/// Shared floating-point tolerances for asserting that SuperCircuit64's simulated results match
/// QucsStudio reference data. These model disagreement between two solvers, not measurement
/// uncertainty: two independent trapezoidal implementations stepping at the same dt still diverge
/// (QucsStudio's own ac-resistor-inductor output is off the closed-form RL solution by a comparable
/// amount). A topology, sign or model bug produces errors many orders of magnitude larger.
///
/// <see cref="AbsoluteTolerance"/>/<see cref="RelativeTolerance"/> are sized for the nonlinear
/// fixtures, measured as:
///
///   ac-resistor-diode              L1.Vt    4.42e-7   (18.7% of the band)
///   ac-half-wave-rectifier         Pr3.dVt  2.77e-7   (24.1%)
///   ac-full-wave-rectifier         Pr3.dVt  1.89e-7   (10.2%)
///   ac-full-wave-rectifier-filter  Pr1.It   2.50e-7   (20.9%)
///   bjt-common-emitter-sweep       nc.Vt    4.60e-7   (26.6%)
///   opamp-741-internals            vhigh.Vt 5.40e-6   (17.5%)
///
/// leaving ~3.8x headroom on the worst. That headroom is deliberate: the residual is dominated by
/// an exponential, so a different libm's Math.Exp can move it slightly. opamp-741-internals has
/// the largest absolute figure but not the highest utilisation, because its samples sit near a
/// 15 V rail where the relative term dominates; the AC fixtures swing through zero and are carried
/// by the absolute term. Its row covers only the samples that test asserts; it skips start-up and
/// the slew around each output switch, for reasons given there.
///
/// The bounds were 2e-6/1e-5 until PhysicalConstants.ThermalVoltage was corrected to the CODATA
/// 1998 value QucsStudio uses. Before that the four diode fixtures measured 9.19e-7 / 9.35e-7 /
/// 1.84e-6 / 4.48e-6, so most of what the old default absorbed was a 1.04 ppm constant mismatch.
///
/// One known wart: a single absolute term covers both volts and amps. In most diode fixtures the
/// currents sit at 0.2-0.4% of the band and the voltages at 10-27%, but that is one error in two
/// units (ac-resistor-diode's 4.42e-7 V across 180 ohm is its 2.46e-9 A). A per-quantity band would
/// tighten current coverage, but must be sized from ac-full-wave-rectifier-filter, whose Pr1.It
/// reaches 20.9%: the smoothing capacitor turns the input current into narrow conduction spikes,
/// and a small disagreement in switching instant lands on their edges.
///
/// A fixture that agrees more tightly (the linear R/L/C ones drift ~1e-8) passes its own
/// tolerances to <see cref="AssertMatches"/>, so a regression there cannot hide under this bound.
/// </summary>
internal static class QucsComparison
{
    public const double AbsoluteTolerance = 1e-6;
    public const double RelativeTolerance = 2e-6;

    /// <summary>
    /// Asserts that <paramref name="actual"/> matches <paramref name="expected"/> within the
    /// combined absolute/relative tolerance: |actual - expected| &lt;= absoluteTolerance +
    /// relativeTolerance * |expected|. Defaults to the shared <see cref="AbsoluteTolerance"/>/
    /// <see cref="RelativeTolerance"/>; pass tighter values when the fixture is known to agree more
    /// closely.
    /// </summary>
    public static void AssertMatches(
        double expected,
        double actual,
        string label,
        double absoluteTolerance = AbsoluteTolerance,
        double relativeTolerance = RelativeTolerance)
    {
        double allowed = absoluteTolerance + relativeTolerance * Math.Abs(expected);
        double diff = Math.Abs(actual - expected);

        Assert.True(diff <= allowed,
            $"{label}: expected {expected:G17} but got {actual:G17} (|diff| = {diff:G3} exceeds tolerance {allowed:G3}).");
    }
}
