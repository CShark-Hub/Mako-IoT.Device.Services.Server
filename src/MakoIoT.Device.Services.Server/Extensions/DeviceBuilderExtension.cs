using MakoIoT.Device.Services.DependencyInjection;
using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.Services.Server.Services;

namespace MakoIoT.Device.Services.Server.Extensions
{
    public delegate void WebServerConfigurator(WebServerOptions options);
    public static class DeviceBuilderExtension
    {
        public static IDeviceBuilder AddWebServer(this IDeviceBuilder builder, WebServerConfigurator configurator)
        {
            DI.RegisterSingleton(typeof(IServer), typeof(Services.Server));
            var options = new WebServerOptions();
            configurator(options);
            DI.RegisterInstance(typeof(WebServerOptions), options);

            return builder;
        }
    }
}
