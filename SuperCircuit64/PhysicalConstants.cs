namespace SuperCircuit64;

public static class PhysicalConstants
{
    /// <summary>
    /// Boltzmann's constant, in Joules/Kelvin (CODATA 1998).
    /// </summary>
    public const double BoltzmannConstant = 1.3806503e-23;

    /// <summary>
    /// Elementary charge, in coulombs (CODATA 1998).
    /// </summary>
    public const double ElementaryCharge = 1.602176462e-19;

    /// <summary>
    /// Nominal junction temperature in kelvin. 300 K is 26.85 °C.
    /// </summary>
    /// <remarks>
    /// uSimmics's default `Temp`/`Tnom` for both its diode and its BJT.
    /// </remarks>
    public const double NominalTemperatureKelvin = 300.0;

    /// <summary>
    /// Thermal voltage kT/q at <see cref="NominalTemperatureKelvin"/>. Device models scale this by 
    /// their ideality/emission coefficient.
    /// </summary>
    public const double ThermalVoltage = BoltzmannConstant * NominalTemperatureKelvin / ElementaryCharge;
}
