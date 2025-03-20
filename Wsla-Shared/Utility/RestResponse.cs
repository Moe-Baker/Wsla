using System;
using System.Net;
using System.Net.Http;

namespace Wsla
{
    public struct RestResponse
    {
        public HttpStatusCode Code { get; }
        public string Message { get; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Message))
                return Code.ToString();
            else
                return $"{Code} | {Message}";
        }

        public RestResponse(HttpStatusCode Code, string Message)
        {
            this.Code = Code;
            this.Message = Message;
        }

        public static RestResponse From(HttpResponseMessage response)
        {
            return new RestResponse(response.StatusCode, response.ReasonPhrase);
        }
        public static RestResponse From(Exception exception)
        {
            return new RestResponse(HttpStatusCode.InternalServerError, exception.Message);
        }
    }
}