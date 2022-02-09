namespace FeatureLoom.Security
{
    public readonly struct UsernamePassword
    {
        public readonly string username;
        public readonly string password;

        public UsernamePassword(string username, string password)
        {
            this.username = username;
            this.password = password;
        }
    }
}
