using Microsoft.Extensions.Logging;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// A common logger.
    /// </summary>
    public static class Logging
    {
        private static ILoggerFactory loggerFactory = null;

        public static void ConfigureLogger(ILoggerFactory factory)
        {
            factory.AddDebug();
        }

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (loggerFactory == null)
                {
                    loggerFactory = new LoggerFactory();
                    ConfigureLogger(loggerFactory);
                }
                return loggerFactory;
            }
            set
            {
                loggerFactory = value;
            }
        }
    }
}
