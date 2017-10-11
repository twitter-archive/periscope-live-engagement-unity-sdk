using System.Text;

namespace Periscope
{
    public class UserHasher
    {
        uint seed;
        static UserHasher instance;
        public static UserHasher Instance
        {
            get
            {
                return instance ?? new UserHasher();
            }
        }

        public void SetSeed(string seed)
        {
            this.seed = FNV1a32b.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        public int ComputeUserHash(string userId)
        {
            var tmp = FNV1a32b.ComputeHash(Encoding.UTF8.GetBytes(userId));
            return (int)((tmp ^ seed) % int.MaxValue);
        }
    }
}
