using System;
using MakoIoT.Device.Services.Server.WebServer;
using Microsoft.Extensions.Logging;

namespace MakoIoT.Device.Services.Server.Services
{
    /// <inheritdoc />
    public class Server : IServer
    {
        private readonly ILogger _logger;
        private MakoWebServer _webServer;
        private readonly WebServerOptions _options;

        public Server(ILogger logger, WebServerOptions options)
        {
            _logger = logger;
            _options = options;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            Type[] controllers = null;

            if (_options.Controllers.Count > 0)
                controllers = (Type[])_options.Controllers.ToArray(typeof(Type));

            _webServer = new MakoWebServer(_options.Port, _options.Protocol, controllers, _logger);
        }

        /// <inheritdoc />
        public void Start()
        {
            bool success = _webServer.Start();
            _logger.LogDebug($@"WebServer started: {success}");
        }

        /// <inheritdoc />
        public void Stop()
        {
            _webServer.Stop();
            _logger.LogDebug("WebServer stopped");
        }

        public void Dispose()
        {
            if (_webServer != null)
                _webServer.Dispose();
        }
    }
}
