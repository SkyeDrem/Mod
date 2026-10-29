using System;
using System.Security.Cryptography;

namespace FXDSP.Core
{
    internal static class MarketRandom
    {
        // OS entropy is needed only when creating a market or loading a save.
        // Quotes continue to use the existing, inexpensive Xorshift32 stream.
        internal static uint NewSeed(uint previous = 0)
        {
            using (var source = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                uint seed;
                do
                {
                    source.GetBytes(bytes);
                    seed = BitConverter.ToUInt32(bytes, 0);
                } while (seed == 0 || seed == previous);
                return seed;
            }
        }
    }
}
