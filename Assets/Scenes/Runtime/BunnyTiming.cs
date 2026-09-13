using System;

namespace LandOfFire.BunnyStep
{
    [Serializable]
    public struct BunnyTiming
    {
        public int preparation, flight, recovery;
        public BunnyTiming(int preparation, int flight, int recovery)
        { this.preparation = preparation; this.flight = flight; this.recovery = recovery; }
        public int Preparation => Math.Max(1, preparation);
        public int Flight => Math.Max(2, flight);
        public int Recovery => Math.Max(1, recovery);
        public int Total => Preparation + Flight + Recovery;
        public static BunnyTiming FromTotal(int ticks)
        {
            int total = Math.Max(4, ticks);
            int ground = Math.Max(1, total / 6);
            return new BunnyTiming(ground, total - ground * 2, ground);
        }
    }
}
