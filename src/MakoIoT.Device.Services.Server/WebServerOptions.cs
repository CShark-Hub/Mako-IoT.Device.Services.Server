using System;
using System.Collections;
using MakoIoT.Device.Services.Server.WebServer;

namespace MakoIoT.Device.Services.Server
{
    public class WebServerOptions
    {
        internal readonly ArrayList Controllers = new ArrayList();

        public int Port { get; set; } = 80;
        public HttpProtocol Protocol { get; set; } = HttpProtocol.Http;

        public void AddController(Type controllerType)
        {
            Controllers.Add(controllerType);
        }
    }
}
