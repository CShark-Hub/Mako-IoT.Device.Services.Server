using System.Net;

namespace MakoIoT.Device.Services.Server.Extensions
{
    public static class HttpListenerResponseExtension
    {
        public static void AddCors(this HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        }
    }
}
