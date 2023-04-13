//
// Copyright (c) 2020 Laurent Ellerbach and the project contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using MakoIoT.Device.Utilities.String.Extensions;
using nanoFramework.DependencyInjection;

namespace MakoIoT.Device.Services.Server.WebServer
{
    /// <summary>
    /// This class instantiates a web server.
    /// </summary>
    public class MakoWebServer : IDisposable
    {
        /// <summary>
        /// URL parameter separation character
        /// </summary>
        public const char ParamSeparator = '&';

        /// <summary>
        /// URL parameter start character
        /// </summary>
        public const char ParamStart = '?';

        /// <summary>
        /// URL parameter equal character
        /// </summary>
        public const char ParamEqual = '=';

        private const int MaxSizeBuffer = 1024;

        #region internal objects

        private bool _cancel = false;
        private Thread _serverThread = null;
        private readonly ArrayList _callbackRoutes;
        private readonly HttpListener _listener;

        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the port the server listens on.
        /// </summary>
        public int Port { get; protected set; }

        /// <summary>
        /// The type of Http protocol used, http or https
        /// </summary>
        public HttpProtocol Protocol { get; protected set; }

        /// <summary>
        /// The Https certificate to use
        /// </summary>
        public X509Certificate HttpsCert
        {
            get => _listener.HttpsCert;

            set => _listener.HttpsCert = value;
        }

        /// <summary>
        /// SSL protocols
        /// </summary>
        public SslProtocols SslProtocols
        {
            get => _listener.SslProtocols;

            set => _listener.SslProtocols = value;
        }

        /// <summary>
        /// Network credential used for default user:password couple during basic authentication
        /// </summary>
        public NetworkCredential Credential { get; set; }

        /// <summary>
        /// Default APiKey to be used for authentication when no key is specified in the attribute
        /// </summary>
        public string ApiKey { get; set; }

        #endregion

        #region Param

        /// <summary>
        /// Get an array of parameters from a URL
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static UrlParameter[] DecodeParam(string parameter)
        {
            UrlParameter[] retParams = null;
            int i = parameter.IndexOf(ParamStart);
            int k;

            if (i >= 0)
            {
                while (i < parameter.Length || i == -1)
                {
                    int j = parameter.IndexOf(ParamEqual, i);
                    if (j > i)
                    {
                        //first param!
                        if (retParams == null)
                        {
                            retParams = new UrlParameter[1];
                            retParams[0] = new UrlParameter();
                        }
                        else
                        {
                            UrlParameter[] rettempParams = new UrlParameter[retParams.Length + 1];
                            retParams.CopyTo(rettempParams, 0);
                            rettempParams[rettempParams.Length - 1] = new UrlParameter();
                            retParams = new UrlParameter[rettempParams.Length];
                            rettempParams.CopyTo(retParams, 0);
                        }
                        k = parameter.IndexOf(ParamSeparator, j);
                        retParams[retParams.Length - 1].Name = parameter.Substring(i + 1, j - i - 1);
                        // Nothing at the end
                        if (k == j)
                        {
                            retParams[retParams.Length - 1].Value = "";
                        }
                        // Normal case
                        else if (k > j)
                        {
                            retParams[retParams.Length - 1].Value = parameter.Substring(j + 1, k - j - 1);
                        }
                        // We're at the end
                        else
                        {
                            retParams[retParams.Length - 1].Value = parameter.Substring(j + 1, parameter.Length - j - 1);
                        }
                        if (k > 0)
                            i = parameter.IndexOf(ParamSeparator, k);
                        else
                            i = parameter.Length;
                    }
                    else
                        i = -1;
                }
            }
            return retParams;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Instantiates a new web server.
        /// </summary>
        /// <param name="port">Port number to listen on.</param>
        /// <param name="protocol"><see cref="HttpProtocol"/> version to use with web server.</param>
        public MakoWebServer(int port, HttpProtocol protocol, ILogger logger, IServiceProvider serviceProvider) : this(port, protocol, null, logger, serviceProvider)
        { }

        /// <summary>
        /// Instantiates a new web server.
        /// </summary>
        /// <param name="port">Port number to listen on.</param>
        /// <param name="protocol"><see cref="HttpProtocol"/> version to use with web server.</param>
        /// <param name="controllers">Controllers to use with this web server.</param>
        public MakoWebServer(int port, HttpProtocol protocol, Type[] controllers, ILogger logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _callbackRoutes = new ArrayList();

            if (controllers != null)
            {
                foreach (var controller in controllers)
                {
                    var controlAttribs = controller.GetCustomAttributes(true);
                    Authentication authentication = null;
                    foreach (var ctrlAttrib in controlAttribs)
                    {
                        if (typeof(AuthenticationAttribute) == ctrlAttrib.GetType())
                        {
                            var strAuth = ((AuthenticationAttribute)ctrlAttrib).AuthenticationMethod;
                            // We do support only None, Basic and ApiKey, raising an exception if this doesn't start by any
                            authentication = ExtractAuthentication(strAuth);
                        }
                    }

                    var functions = controller.GetMethods();
                    foreach (var func in functions)
                    {
                        var attributes = func.GetCustomAttributes(true);
                        RouteCallback routeCallback = null;
                        foreach (var attrib in attributes)
                        {
                            if (typeof(RouteAttribute) == attrib.GetType())
                            {
                                routeCallback = new RouteCallback();
                                routeCallback.Route = ((RouteAttribute)attrib).Route;
                                routeCallback.CaseSensitive = false;
                                routeCallback.Method = string.Empty;
                                routeCallback.Authentication = authentication;

                                routeCallback.RouteParts = routeCallback.Route.Split('/');
                                if (routeCallback.Route.Contains("{"))
                                {
                                    var routeParams = new ArrayList();
                                    for (int i = 0; i < routeCallback.RouteParts.Length; i++)
                                    {
                                        if (routeCallback.RouteParts[i][0] == '{')
                                            routeParams.Add(i);
                                    }
                                    routeCallback.RouteParamsIndexes = (int[])routeParams.ToArray(typeof(int));
                                }

                                routeCallback.Callback = func;
                                foreach (var otherattrib in attributes)
                                {
                                    if (typeof(MethodAttribute) == otherattrib.GetType())
                                    {
                                        routeCallback.Method = ((MethodAttribute)otherattrib).Method;
                                    }
                                    else if (typeof(CaseSensitiveAttribute) == otherattrib.GetType())
                                    {
                                        routeCallback.CaseSensitive = true;
                                    }
                                    else if (typeof(AuthenticationAttribute) == otherattrib.GetType())
                                    {
                                        var strAuth = ((AuthenticationAttribute)otherattrib).AuthenticationMethod;
                                        // A method can have a different authentication than the main class, so we override if any
                                        routeCallback.Authentication = ExtractAuthentication(strAuth);
                                    }
                                }

                                _callbackRoutes.Add(routeCallback);
                                _logger.LogTrace($"{routeCallback.Callback.Name}, {routeCallback.Route.EscapeForInterpolation()}, {routeCallback.Method}, {routeCallback.CaseSensitive}");
                            }
                        }
                    }

                }
            }

            Protocol = protocol;
            Port = port;
            string prefix = Protocol == HttpProtocol.Http ? "http" : "https";
            _listener = new HttpListener(prefix, port);
            _logger.LogDebug("Web server started on port " + port.ToString());
        }

        private Authentication ExtractAuthentication(string strAuth)
        {
            const string _none = "None";
            const string _basic = "Basic";
            const string _apiKey = "ApiKey";

            Authentication authentication;
            if (strAuth.IndexOf(_none) == 0)
            {
                if (strAuth.Length == _none.Length)
                {
                    authentication = new Authentication();
                }
                else
                {
                    throw new ArgumentException($"Authentication attribute None can only be used alone");
                }
            }
            else if (strAuth.IndexOf(_basic) == 0)
            {
                if (strAuth.Length == _basic.Length)
                {
                    authentication = new Authentication((NetworkCredential)null);
                }
                else
                {
                    var sep = strAuth.IndexOf(':');
                    if (sep == _basic.Length)
                    {
                        var space = strAuth.IndexOf(' ');
                        if (space < 0)
                        {
                            throw new ArgumentException($"Authentication attribute Basic should be 'Basic:user passowrd'");
                        }

                        var user = strAuth.Substring(sep + 1, space - sep - 1);
                        var password = strAuth.Substring(space + 1);
                        authentication = new Authentication(new NetworkCredential(user, password, System.Net.AuthenticationType.Basic));
                    }
                    else
                    {
                        throw new ArgumentException($"Authentication attribute Basic should be 'Basic:user passowrd'");
                    }
                }
            }
            else if (strAuth.IndexOf(_apiKey) == 0)
            {
                if (strAuth.Length == _apiKey.Length)
                {
                    authentication = new Authentication(string.Empty);
                }
                else
                {
                    var sep = strAuth.IndexOf(':');
                    if (sep == _apiKey.Length)
                    {
                        var key = strAuth.Substring(sep + 1);
                        authentication = new Authentication(key);
                    }
                    else
                    {
                        throw new ArgumentException($"Authentication attribute ApiKey should be 'ApiKey:thekey'");
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Authentication attribute can only start with Basic, None or ApiKey and case sensitive");
            }
            return authentication;
        }

        #endregion

        #region Events

        /// <summary>
        /// Delegate for the CommandReceived event.
        /// </summary>
        public delegate void GetRequestHandler(object obj, WebServerEventArgs e);

        /// <summary>
        /// CommandReceived event is triggered when a valid command (plus parameters) is received.
        /// Valid commands are defined in the AllowedCommands property.
        /// </summary>
        public event GetRequestHandler CommandReceived;

        #endregion

        #region Public and private methods

        /// <summary>
        /// Start the multi threaded server.
        /// </summary>
        public bool Start()
        {
            if (_serverThread == null)
            {
                _serverThread = new Thread(StartListener);
            }

            bool bStarted = true;
            // List Ethernet interfaces, so we can determine the server's address
            ListInterfaces();
            // start server           
            try
            {
                _cancel = false;
                _serverThread.Start();
                _logger.LogDebug("Started server in thread " + _serverThread.GetHashCode().ToString());
            }
            catch
            {   //if there is a problem, maybe due to the fact we did not wait enough
                _cancel = true;
                bStarted = false;
            }
            return bStarted;
        }

        /// <summary>
        /// Restart the server.
        /// </summary>
        private bool Restart()
        {
            Stop();
            return Start();
        }

        /// <summary>
        /// Stop the multi threaded server.
        /// </summary>
        public void Stop()
        {
            _cancel = true;
            Thread.Sleep(100);
            _serverThread.Abort();
            _serverThread = null;
            _logger.LogDebug("Stoped server in thread ");
        }

        /// <summary>
        /// Output a stream
        /// </summary>
        /// <param name="response">the socket stream</param>
        /// <param name="strResponse">the stream to output</param>
        public static void OutPutStream(HttpListenerResponse response, string strResponse)
        {
            if (response == null)
            {
                return;
            }

            byte[] messageBody = Encoding.UTF8.GetBytes(strResponse);
            response.ContentLength64 = messageBody.Length;
            response.OutputStream.Write(messageBody, 0, messageBody.Length);
        }

        /// <summary>
        /// Output an HTTP Code and close the connection
        /// </summary>
        /// <param name="response">the socket stream</param>
        /// <param name="code">the http code</param>
        public static void OutputHttpCode(HttpListenerResponse response, HttpStatusCode code)
        {
            if (response == null)
            {
                return;
            }

            // This is needed to force the 200 OK without body to go thru
            response.ContentLength64 = 0;
            response.KeepAlive = false;
            response.StatusCode = (int)code;
        }

        /// <summary>
        /// Send file content over HTTP response.
        /// </summary>
        /// <param name="response"><see cref="HttpListenerResponse"/> to send the content over.</param>
        /// <param name="fileName">Name of the file to send over <see cref="HttpListenerResponse"/>.</param>
        /// <param name="content">Content of the file to send.</param>
        /// /// <param name="contentType">The type of file, if empty string, then will use auto detection</param>
        public static void SendFileOverHTTP(HttpListenerResponse response, string fileName, byte[] content, string contentType = "")
        {
            contentType = contentType == "" ? GetContentTypeFromFileName(fileName.Substring(fileName.LastIndexOf('.'))) : contentType;
            response.ContentType = contentType;
            response.ContentLength64 = content.Length;

            // Now loop to send all the data.

            for (long bytesSent = 0; bytesSent < content.Length;)
            {
                // Determines amount of data left
                long bytesToSend = content.Length - bytesSent;
                bytesToSend = bytesToSend < MaxSizeBuffer ? bytesToSend : MaxSizeBuffer;

                // Writes data to output stream
                response.OutputStream.Write(content, (int)bytesSent, (int)bytesToSend);

                // allow some time to physically send the bits. Can be reduce to 10 or even less if not too much other code running in parallel

                // update bytes sent
                bytesSent += bytesToSend;
            }
        }

        private void StartListener()
        {
            _listener.Start();
            while (!_cancel)
            {
                HttpListenerContext context = _listener.GetContext();
                if (context == null)
                {
                    return;
                }

                new Thread(() =>
                {
                    bool isRoute = false;
                    string rawUrl = context.Request.RawUrl;

                    //This is for handling with transitory or bad requests
                    if (rawUrl == null)
                    {
                        return;
                    }


                    // Variables used only within the "for". They are here for performance reasons
                    bool mustAuthenticate;
                    bool isAuthOk;
                    //

                    foreach (var rt in _callbackRoutes)
                    {
                        RouteCallback route = (RouteCallback)rt;

                        if (!IsRouteMatch(route, context, out var callbackParams))
                            continue;

                        // Starting a new thread to be able to handle a new request in parallel
                        isRoute = true;

                        // Check auth first
                        mustAuthenticate = route.Authentication != null && route.Authentication.AuthenticationType != AuthenticationType.None;
                        isAuthOk = !mustAuthenticate;

                        if (mustAuthenticate)
                        {
                            if (route.Authentication.AuthenticationType == AuthenticationType.Basic)
                            {
                                var credSite = route.Authentication.Credentials ?? Credential;
                                var credReq = context.Request.Credentials;

                                isAuthOk = credReq != null
                                    && credSite.UserName == credReq.UserName
                                    && credSite.Password == credReq.Password;
                            }
                            else if (route.Authentication.AuthenticationType == AuthenticationType.ApiKey)
                            {
                                var apikeySite = route.Authentication.ApiKey ?? ApiKey;
                                var apikeyReq = GetApiKeyFromHeaders(context.Request.Headers);

                                isAuthOk = apikeyReq != null
                                    && apikeyReq == apikeySite;
                            }
                        }

                        if (!isAuthOk)
                        {
                            if (route.Authentication != null && route.Authentication.AuthenticationType == AuthenticationType.Basic)
                            {
                                context.Response.Headers.Add("WWW-Authenticate", $"Basic realm=\"Access to {route.Route}\"");
                            }

                            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            context.Response.ContentLength64 = 0;
                            _logger.LogTrace($"Response code: {context.Response.StatusCode}");
                            return;
                        }

                        InvokeRoute(route, callbackParams);

                        if (context.Response != null)
                        {
                            _logger.LogTrace($"Response code: {context.Response.StatusCode}");
                            context.Response.Close();
                            context.Close();
                            break;
                        }
                    }

                    if (!isRoute)
                    {
                        if (CommandReceived != null)
                        {
                            // Starting a new thread to be able to handle a new request in parallel
                            CommandReceived.Invoke(this, new WebServerEventArgs(context));
                        }
                        else
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            context.Response.ContentLength64 = 0;
                        }

                        // When context has been handed over to WebsocketServer, it will be null at this point
                        if (context.Response == null)
                        {
                            //do nothing this is a websocket that is managed by a websocketserver that is responsible for the context now. 
                        }
                        else
                        {
                            _logger.LogTrace($"Response code: {context.Response.StatusCode}");
                            context.Response.Close();
                            context.Close();
                        }
                    }
                }).Start();

            }
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
        }

        /// <summary>
        /// Method which invokes route. Can be overriden to inject custom logic.
        /// </summary>
        /// <param name="route">Current rounte to invoke. Resolved based on parameters.</param>
        /// <param name="callbackParams"></param>
        protected virtual void InvokeRoute(RouteCallback route, object[] callbackParams)
        {
            _logger.LogTrace($"Invoking {route.Callback.DeclaringType}.{route.Callback.Name}");
            var dp = ActivatorUtilities.CreateInstance(_serviceProvider, route.Callback.DeclaringType);
            route.Callback.Invoke(dp, callbackParams);
        }

        private string GetApiKeyFromHeaders(WebHeaderCollection headers)
        {
            var sec = headers.GetValues("ApiKey");
            if (sec != null
                && sec.Length > 0)
            {
                return sec[0];
            }

            return null;
        }

        /// <summary>
        /// List all IP address, useful for debug only
        /// </summary>
        private void ListInterfaces()
        {
            NetworkInterface[] ifaces = NetworkInterface.GetAllNetworkInterfaces();
            _logger.LogDebug("Number of Interfaces: " + ifaces.Length.ToString());
            foreach (NetworkInterface iface in ifaces)
            {
                _logger.LogDebug("IP:  " + iface.IPv4Address + "/" + iface.IPv4SubnetMask);
            }
        }

        /// <summary>
        /// Get the MIME-type for a file name.
        /// </summary>
        /// <param name="fileName">File name to get content type for.</param>
        /// <returns>The MIME-type for the file name.</returns>
        private static string GetContentTypeFromFileName(string fileName)
        {
            // normalize to lower case to speed comparison
            fileName = fileName.ToLower();

            string contentType = "text/html";

            //determine the type of file for the http header
            if (fileName == ".cs" ||
                fileName == ".txt" ||
                fileName == ".csproj"
            )
            {
                contentType = "text/plain";
            }
            else if (fileName == ".jpg" ||
                fileName == ".bmp" ||
                fileName == ".jpeg" ||
                fileName == ".png"
              )
            {
                contentType = "image";
            }
            else if (fileName == ".htm" ||
                fileName == ".html"
              )
            {
                contentType = "text/html";
            }
            else if (fileName == ".mp3")
            {
                contentType = "audio/mpeg";
            }
            else if (fileName == ".css")
            {
                contentType = "text/css";
            }
            else if (fileName == ".ico")
            {
                contentType = "image/x-icon";
            }

            return contentType;
        }

        /// <summary>
        /// Dispose of any resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release resources.
        /// </summary>
        /// <param name="disposing">Dispose of resources?</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serverThread = null;
            }
        }

        protected virtual bool IsRouteMatch(RouteCallback route, HttpListenerContext context, out object[] callbackParams)
        {
            callbackParams = null;

            if (context.Request.HttpMethod != route.Method)
                return false;

            var urlAndParams = context.Request.RawUrl.Split('?');
            string url = urlAndParams[0];
            url = route.CaseSensitive ? url : url.ToLower();

            var urlParts = url.Trim('/').Split('/');

            if (urlParts.Length != route.RouteParts.Length)
                return false;

            bool isMatch = true;
            callbackParams = new object[route.RouteParamsIndexes == null ? 1 : route.RouteParamsIndexes.Length + 1];
            int paramIndex = 0;
            for (int i = 0; i < route.RouteParts.Length; i++)
            {
                if (route.RouteParamsIndexes != null && i == route.RouteParamsIndexes[paramIndex])
                {
                    callbackParams[paramIndex] = urlParts[i];
                    paramIndex++;
                }
                else if (route.RouteParts[i] != urlParts[i])
                {
                    isMatch = false;
                    break;
                }
            }

            if (!isMatch)
                return false;

            callbackParams[callbackParams.Length - 1] = new WebServerEventArgs(context);

            return true;
        }

        #endregion
    }
}
