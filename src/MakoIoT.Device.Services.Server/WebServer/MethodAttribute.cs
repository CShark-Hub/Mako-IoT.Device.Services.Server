//
// Copyright (c) 2020 Laurent Ellerbach and the project contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace MakoIoT.Device.Services.Server.WebServer
{
    /// <summary>
    /// The HTTP Method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodAttribute : Attribute
    {
        public string Method { get; set; }

        public MethodAttribute(string method)
        {
            Method = method;
        }
    }
}
