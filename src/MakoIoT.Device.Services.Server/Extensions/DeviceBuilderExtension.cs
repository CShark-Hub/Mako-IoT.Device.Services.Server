using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.Services.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MakoIoT.Device.Services.Server.Extensions
{
    public delegate void WebServerConfigurator(WebServerOptions options);
    public static class DeviceBuilderExtension
    {
        public static IDeviceBuilder AddWebServer(this IDeviceBuilder builder, WebServerConfigurator configurator)
        {
            builder.Services.AddSingleton(typeof(IServer), typeof(Services.Server));
            var options = new WebServerOptions();
            configurator(options);
            builder.Services.AddSingleton(typeof(WebServerOptions), options);

            return builder;
        }
    }
}
