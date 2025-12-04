namespace Barometer_UWP.Helpers
{
    public static class UnitConverter
    {
        private const double HpaToMmHgFactor = 0.75006375541921;

        public static double HpaToMmHg(double hpa)
        {
            return hpa * HpaToMmHgFactor;
        }

        public static double MmHgToHpa(double mmHg)
        {
            return mmHg / HpaToMmHgFactor;
        }
    }
}