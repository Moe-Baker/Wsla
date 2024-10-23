using System;
using System.Threading.Tasks;

using Cysharp.Threading.Tasks;

namespace Toolbox
{
    public struct Response
    {
        public ResponseType Type { get; }

        public bool IsSuccess => Type is ResponseType.Success;
        public bool IsError => Type is ResponseType.Error;

        public Response(ResponseType type)
        {
            this.Type = type;
        }

        public static implicit operator Response(bool value)
        {
            if (value)
                return Success;
            else
                return Error;
        }

        public static Response Success => new(ResponseType.Success);
        public static Response Error => new(ResponseType.Error);
    }

    public struct Response<TError>
    {
        public ResponseType Type { get; }

        public TError Error { get; }

        public bool IsSuccess => Type is ResponseType.Success;
        public bool IsError => Type is ResponseType.Error;

        public Response(ResponseType type, TError error)
        {
            this.Type = type;
            this.Error = error;
        }

        public static implicit operator Response<TError>(bool value)
        {
            if (value)
                return Success;
            else
                return FromError(default);
        }
        public static implicit operator Response<TError>(TError error) => FromError(error);

        public static Response<TError> Success => new(ResponseType.Success, default);

        public static Response<TError> FromError(TError error) => new(ResponseType.Error, error);
    }

    public struct Response<TValue, TError>
    {
        public ResponseType Type { get; }

        public TValue Value { get; }
        public TError Error { get; }

        public bool IsSuccess => Type is ResponseType.Success;
        public bool IsError => Type is ResponseType.Error;

        public Response(ResponseType type, TValue value, TError error)
        {
            this.Type = type;
            this.Value = value;
            this.Error = error;
        }

        public static implicit operator Response<TValue, TError>(TValue value) => FromResult(value);
        public static implicit operator Response<TValue, TError>(TError error) => FromError(error);

        public static Response<TValue, TError> FromResult(TValue error) => new(ResponseType.Success, error, default);
        public static Response<TValue, TError> FromError(TError error) => new(ResponseType.Error, default, error);
    }

    public enum ResponseType
    {
        Error, Success
    }

    public static class ResponseExtensions
    {
        public static async Task<Response<TValue, Exception>> AsResponse<TValue>(this Task<TValue> task)
        {
            try
            {
                var value = await task;
                return value;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static async UniTask<Response<TValue, Exception>> ToResponse<TValue>(this UniTask<TValue> task)
        {
            try
            {
                var value = await task;
                return value;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}