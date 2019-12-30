namespace FeatureFlowFramework.Web
{
    public static class SharedWebServer
    {
        private static IWebServer webServer;

        public static IWebServer WebServer
        {
            get
            {
                if(webServer == null) webServer = new DefaultWebServer();
                return webServer;
            }
            set
            {
                webServer = value;
            }
        }
    }
}
