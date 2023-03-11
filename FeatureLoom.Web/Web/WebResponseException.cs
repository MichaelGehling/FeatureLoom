using FeatureLoom.Logging;
using System;
using System.Net;

namespace FeatureLoom.Web
{
    public class WebResponseException : Exception
    {
        string internalMessage;
        string responseMessage;
        HttpStatusCode statusCode;
        Loglevel? logLevel;

        public string InternalMessage { get => internalMessage; }
        public string ResponseMessage { get => responseMessage; }
        public HttpStatusCode StatusCode { get => statusCode; }
        public Loglevel? LogLevel { get => logLevel; }

        public WebResponseException(string internalMessage, string responseMessage, HttpStatusCode statusCode, Loglevel logLevel) : base(internalMessage)
        {
            this.internalMessage = internalMessage;
            this.responseMessage = responseMessage;
            this.statusCode = statusCode;
            this.logLevel = logLevel;
        }

        public WebResponseException(string internalMessage, string responseMessage, HttpStatusCode statusCode) : base(internalMessage)
        {
            this.internalMessage = internalMessage;
            this.responseMessage = responseMessage;
            this.statusCode = statusCode;
        }

        public WebResponseException(string message, HttpStatusCode statusCode, Loglevel logLevel) : base(message)
        {
            this.internalMessage = message;
            this.responseMessage = message;
            this.statusCode = statusCode;
            this.logLevel = logLevel;
        }

        public WebResponseException(string message, HttpStatusCode statusCode) : base(message)
        {
            this.internalMessage = message;
            this.responseMessage = message;
            this.statusCode = statusCode;
        }

        public WebResponseException() : base()
        {
        }

        public WebResponseException(string message) : base(message)
        {
        }

        public WebResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}