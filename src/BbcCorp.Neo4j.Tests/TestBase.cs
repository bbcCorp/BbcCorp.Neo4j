using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BbcCorp.Neo4j.Tests
{
    public class TestBase : IDisposable
    {
        protected IConfigurationRoot Configuration  { get; private set; }
        protected ILoggerFactory loggerFactory;

        public TestBase()
        {
            loggerFactory = new NullLoggerFactory();

            Configuration = GetIConfigurationRoot();
        }

        private IConfigurationRoot GetIConfigurationRoot()
        {            
            return new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory()))
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();
        }

        public virtual void Dispose()
        {
            loggerFactory = null;
            Configuration = null;
        }
    }

}