namespace Devil.Client.Config
{
    public class DevilServerSettings
    {
        public double SpawnChanceDevil { get; set; }

        public double DevilHarbingerBonusChance { get; set; }

        public void Normalize()
        {
            SpawnChanceDevil = NormalizeChance(SpawnChanceDevil);
            DevilHarbingerBonusChance =
                NormalizeChance(DevilHarbingerBonusChance);
        }

        private static double NormalizeChance(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            return System.Math.Clamp(value, 0, 100);
        }
    }
}
