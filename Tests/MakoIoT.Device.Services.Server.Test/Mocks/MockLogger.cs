using MakoIoT.Device.Services.Interface;
using System;
using System.Reflection;

namespace MakoIoT.Device.Services.Server.Test.Mocks
{
    internal class MockLogger : ILog
    {
        public void Critical(string message, Exception exception, MethodInfo format)
        {
            throw new NotImplementedException();
        }

        public void Critical(string message)
        {
            throw new NotImplementedException();
        }

        public void Critical(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Error(string message, Exception exception, MethodInfo format)
        {
            throw new NotImplementedException();
        }

        public void Error(string message)
        {
            throw new NotImplementedException();
        }

        public void Error(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Information(string message, Exception exception, MethodInfo format)
        {
            throw new NotImplementedException();
        }

        public void Information(string message)
        {
            throw new NotImplementedException();
        }

        public void Information(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Trace(string message, Exception exception, MethodInfo format)
        {
            throw new NotImplementedException();
        }

        public void Trace(string message)
        {
            throw new NotImplementedException();
        }

        public void Trace(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Warning(string message, Exception exception, MethodInfo format)
        {
            throw new NotImplementedException();
        }

        public void Warning(string message)
        {
            throw new NotImplementedException();
        }

        public void Warning(Exception exception)
        {
            throw new NotImplementedException();
        }
    }
}
