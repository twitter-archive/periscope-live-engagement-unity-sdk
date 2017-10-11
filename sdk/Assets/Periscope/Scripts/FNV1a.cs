namespace Periscope
{
    public static class FNV1a32b
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        public static uint ComputeHash(byte[] array)
        {
            var hash = offset;
            for (var i = 0; i < array.Length; i++)
            {
                unchecked
                {
                    hash ^= array[i];
                    hash *= prime;
                }
            }
            return hash;
        }
    }
}
