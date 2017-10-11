namespace Periscope
{
    [System.Serializable]
    public class ProfileImageUrl
    {
        public string url;
        public ProfileImageUrl(string url)
        {
            this.url = url;
        }
    }

    [System.Serializable]
    public class User
    {
        public string id;
        public string username;
        public ProfileImageUrl[] profileImageUrls;
        bool hashed;
        int hash;
        public int Hash
        {
            get
            {
                if (!hashed)
                {
                    hashed = true;
                    hash = UserHasher.Instance.ComputeUserHash(id);
                }
                return hash;
            }
        }

        public string ProfileImageUrl
        {
            get
            {
                if (profileImageUrls.Length > 0)
                {
                    return profileImageUrls[0].url;
                }
                return "";
            }
        }

        public User(string id, string username = "", string profileImageUrl = "")
        {
            this.id = id;
            this.username = username;
            profileImageUrls = new ProfileImageUrl[1] { new ProfileImageUrl(profileImageUrl) };
        }
    }
}
