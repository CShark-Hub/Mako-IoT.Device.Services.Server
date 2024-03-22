using System;
using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.Services.Server.WebServer;

namespace MakoIoT.Device.Services.Server.Services
{
    /// <inheritdoc />
    internal sealed class Server : IServer
    {
        private readonly ILog _logger;
        private MakoWebServer _webServer;
        private readonly WebServerOptions _options;
        private readonly IServiceProvider _serviceProvider;

        public Server(ILog logger, WebServerOptions options, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _options = options;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            Type[] controllers = null;

            if (_options.Controllers.Count > 0)
                controllers = (Type[])_options.Controllers.ToArray(typeof(Type));

            _webServer = new MakoWebServer(_options.Port, _options.Protocol, controllers, _logger, _serviceProvider);
        }

        /// <inheritdoc />
        public void Start()
        {
            if (_webServer is null)
            {
                throw new Exception("Server is not initialized.");
            }
            
            var success = _webServer.Start();
            _logger.Trace($@"WebServer started: {success}");
        }

        /// <inheritdoc />
        public void Stop()
        {
            _webServer?.Stop();
            _logger.Trace("WebServer stopped");
        }

        public void Dispose()
        {
            _webServer?.Dispose();
        }
    }
}
