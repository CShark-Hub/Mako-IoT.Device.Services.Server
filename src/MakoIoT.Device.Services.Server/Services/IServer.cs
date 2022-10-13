using System;

namespace MakoIoT.Device.Services.Server.Services
{
    /// <summary>
    /// Web server with Controllers support
    /// </summary>
    public interface IServer : IDisposable
    {
        /// <summary>
        /// Starts web server.
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stops web server.
        /// </summary>
        void Stop();

        /// <summary>
        /// Initializes web server, register Controllers etc.
        /// </summary>
        void Initialize();
    }
}