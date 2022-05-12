using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FeatureLoom.Web
{
    public struct HandlerResult
    {
        public bool requestHandled;
        public object data;
        public HttpStatusCode? statusCode;

        public HandlerResult(bool requestHandled, object result, HttpStatusCode? statusCode)
        {
            this.requestHandled = requestHandled;
            this.data = result;
            this.statusCode = statusCode;
        }

        public bool IsInformational => statusCode.HasValue && (int)statusCode.Value >= 100 && (int)statusCode.Value < 200;
        public bool IsSuccessful => statusCode.HasValue && (int)statusCode.Value >= 200 && (int)statusCode.Value < 300;
        public bool IsRedirection => statusCode.HasValue && (int)statusCode.Value >= 300 && (int)statusCode.Value < 400;
        public bool IsClientError => statusCode.HasValue && (int)statusCode.Value >= 400 && (int)statusCode.Value < 500;
        public bool IsServerError => statusCode.HasValue && (int)statusCode.Value >= 500 && (int)statusCode.Value < 600;

        public static HandlerResult NotHandled() => new HandlerResult(false, null, null);
        public static HandlerResult NotHandled_Forbidden() => new HandlerResult(false, null, HttpStatusCode.Forbidden);
        public static HandlerResult NotHandled_BadRequest() => new HandlerResult(false, null, HttpStatusCode.BadRequest);
        public static HandlerResult NotHandled_NotFound() => new HandlerResult(false, null, HttpStatusCode.NotFound);
        public static HandlerResult NotHandled_MethodNotAllowed() => new HandlerResult(false, null, HttpStatusCode.MethodNotAllowed);
        public static HandlerResult NotHandled_InternalServerError() => new HandlerResult(false, null, HttpStatusCode.InternalServerError);
        public static HandlerResult NotHandled_ServiceUnavailable() => new HandlerResult(false, null, HttpStatusCode.ServiceUnavailable);
        public static HandlerResult NotHandled_Unauthorized() => new HandlerResult(false, null, HttpStatusCode.Unauthorized);
        public static HandlerResult NotHandled_RequestTimeout() => new HandlerResult(false, null, HttpStatusCode.RequestTimeout);


        public static HandlerResult Handled_OK(object result = null) => new HandlerResult(true, result, HttpStatusCode.OK);
        public static HandlerResult Handled_Forbidden(object result = null) => new HandlerResult(true, result, HttpStatusCode.Forbidden);
        public static HandlerResult Handled_BadRequest(object result = null) => new HandlerResult(true, result, HttpStatusCode.BadRequest);
        public static HandlerResult Handled_NotFound(object result = null) => new HandlerResult(true, result, HttpStatusCode.NotFound);
        public static HandlerResult Handled_MethodNotAllowed(object result = null) => new HandlerResult(true, result, HttpStatusCode.MethodNotAllowed);
        public static HandlerResult Handled_Accepted(object result = null) => new HandlerResult(true, result, HttpStatusCode.Accepted);
        public static HandlerResult Handled_Conflict(object result = null) => new HandlerResult(true, result, HttpStatusCode.Conflict);
        public static HandlerResult Handled_Created(object result = null) => new HandlerResult(true, result, HttpStatusCode.Created);
        public static HandlerResult Handled_InternalServerError(object result = null) => new HandlerResult(true, result, HttpStatusCode.InternalServerError);
        public static HandlerResult Handled_NotModified(object result = null) => new HandlerResult(true, result, HttpStatusCode.NotModified);
        public static HandlerResult Handled_ServiceUnavailable(object result = null) => new HandlerResult(true, result, HttpStatusCode.ServiceUnavailable);
        public static HandlerResult Handled_Unauthorized(object result = null) => new HandlerResult(true, result, HttpStatusCode.Unauthorized);
        public static HandlerResult Handled_RequestTimeout(object result = null) => new HandlerResult(true, result, HttpStatusCode.RequestTimeout);

    }
}
