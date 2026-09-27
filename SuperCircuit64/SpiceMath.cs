using System;

namespace SuperCircuit64;

public static class SpiceMath
{
    /// <summary>
    /// Limits the per-iteration change of a PN junction voltage during
    /// Newton-Raphson iteration.
    /// </summary>
    /// <param name="vnew">Proposed new junction voltage.</param>
    /// <param name="vold">Junction voltage from the previous iteration.</param>
    /// <param name="vt">Thermal voltage.</param>
    /// <param name="vcrit">Critical voltage.</param>
    /// <param name="limited">
    /// True if the voltage was altered. The caller should treat the iteration
    /// as non-converged.
    /// </param>
    /// <returns>
    /// The limited junction voltage.
    /// </returns>
    public static double Pnjlim(double vnew, double vold, double vt, double vcrit, out bool limited)
    {
        // https://sourceforge.net/p/ngspice/ngspice/ci/master/tree/src/spicelib/devices/devsup.c#l50
        // double DEVpnjlim(...)

        if ((vnew > vcrit) && (Math.Abs(vnew - vold) > (vt + vt)))
        {
            if (vold > 0.0)
            {
                double arg = (vnew - vold) / vt;
                if (arg > 0.0)
                {
                    vnew = vold + vt * (2.0 + Math.Log(arg - 2.0));
                }
                else
                {
                    vnew = vold - vt * (2.0 + Math.Log(2.0 - arg));
                }
            }
            else
            {
                vnew = vt * Math.Log(vnew / vt);
            }
            limited = true;
            return vnew;
        }

        if (vnew < 0.0)
        {
            double floor;
            if (vold > 0.0)
            {
                floor = -1.0 * vold - 1.0;
            }
            else
            {
                floor = 2.0 * vold - 1.0;
            }

            if (vnew < floor)
            {
                limited = true;
                return floor;
            }
        }

        limited = false;
        return vnew;
    }
}
