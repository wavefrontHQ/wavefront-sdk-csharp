using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// A common logger.
    /// </summary>
    public static class Logging
    {
        private static ILoggerFactory loggerFactory = null;

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (loggerFactory == null)
                {
#if NET452 || NET46
                    loggerFactory = new LoggerFactory();
                    loggerFactory.AddDebug();
#else
                    loggerFactory = new ServiceCollection()
                        .AddLogging(builder => builder.AddDebug())
                        .BuildServiceProvider()
                        .GetService<ILoggerFactory>();
#endif
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
