using FeatureLoom.Synchronization;

namespace FeatureLoom.Web
{
    public static class SharedWebServer
    {
        private static IWebServer webServer;
        private static FeatureLock creationLock = new FeatureLock();

        public static IWebServer WebServer
        {
            get
            {
                while (webServer == null)
                {
                    if (creationLock.TryLock(out var acquiredLock))
                    {
                        using (acquiredLock)
                        {
                            webServer = new DefaultWebServer();
                        }
                    }
                }

                return webServer;
            }
            set
            {
                webServer = value;
            }
        }
    }
}