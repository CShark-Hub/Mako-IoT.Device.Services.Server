using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.Services.Server.Extensions;
using MakoIoT.Device.Services.Server.Services;
using MakoIoT.Device.Services.Server.Test.Mocks;
using Microsoft.Extensions.DependencyInjection;
using nanoFramework.TestFramework;

namespace MakoIoT.Device.Services.Server.Test.Extensions
{
    [TestClass]
    public class DeviceBuilderExtensionTests
    {
        [TestMethod]
        public void AddWebServer_Should_RegisterServices()
        {
            var protocol = WebServer.HttpProtocol.Https;
            var port = 322;
            var mockBuild = new DeviceBuilderMock();
            mockBuild.Services.AddSingleton(typeof(ILog), new MockLogger());

            DeviceBuilderExtension.AddWebServer(mockBuild, (c) => { c.Protocol = protocol; c.Port = port; });

            var serviceProvider = mockBuild.Services.BuildServiceProvider();
            var options = (WebServerOptions)serviceProvider.GetService(typeof(WebServerOptions));
            var server = (IServer)serviceProvider.GetService(typeof(IServer));

            Assert.IsNotNull(options);
            Assert.IsNotNull(server);
            Assert.AreEqual(options.Port, port);
            Assert.AreEqual(options.Protocol, protocol);
        }
    }
}
