namespace SuperCircuit64;

public static class PhysicalConstants
{
    public const double BoltzmannConstant = 1.3806503e-23;
    public const double ElementaryCharge = 1.602176462e-19;
    public const double NominalTemperatureKelvin = 300.0;
    public const double ThermalVoltage = BoltzmannConstant * NominalTemperatureKelvin / ElementaryCharge;
}
