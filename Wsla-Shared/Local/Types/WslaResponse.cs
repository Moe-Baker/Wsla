using System;
using System.Threading.Tasks;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public struct WslaResponse
    {
        public WslaResponseResponseType Type;

        public bool IsSuccess => Type is WslaResponseResponseType.Success;
        public bool IsError => Type is WslaResponseResponseType.Error;

        public WslaResponse(WslaResponseResponseType type)
        {
            this.Type = type;
        }

        public static implicit operator WslaResponse(bool value)
        {
            if (value)
                return Success;
            else
                return Error;
        }

        public static WslaResponse Success => new(WslaResponseResponseType.Success);
        public static WslaResponse Error => new(WslaResponseResponseType.Error);
    }

    public struct WslaResponse<TError> : IAutoNetworkSerialization
    {
        public WslaResponseResponseType Type;

        public TError Error;

        public bool IsSuccess => Type is WslaResponseResponseType.Success;
        public bool IsError => Type is WslaResponseResponseType.Error;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Type);

            if (Type is WslaResponseResponseType.Error)
                context.Select(ref Error);
        }

        public WslaResponse(WslaResponseResponseType type, TError error)
        {
            this.Type = type;
            this.Error = error;
        }

        public static implicit operator WslaResponse<TError>(bool value)
        {
            if (value is false)
                throw new InvalidOperationException($"Can Only Implicitly Convert True to Success Response, False not Supported");

            return Success;
        }
        public static implicit operator WslaResponse<TError>(TError error) => FromError(error);

        public static WslaResponse<TError> Success => new(WslaResponseResponseType.Success, default);

        public static WslaResponse<TError> FromError(TError error) => new(WslaResponseResponseType.Error, error);
    }

    public struct WslaResponse<TValue, TError> : IAutoNetworkSerialization
    {
        public WslaResponseResponseType Type;

        public TValue Value;
        public TError Error;

        public bool IsSuccess => Type is WslaResponseResponseType.Success;
        public bool IsError => Type is WslaResponseResponseType.Error;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Type);

            switch (Type)
            {
                case WslaResponseResponseType.Error:
                    context.Select(ref Error);
                    break;

                case WslaResponseResponseType.Success:
                    context.Select(ref Value);
                    break;
            }
        }

        public WslaResponse(WslaResponseResponseType type, TValue value, TError error)
        {
            this.Type = type;
            this.Value = value;
            this.Error = error;
        }
        public WslaResponse(TValue value) : this(WslaResponseResponseType.Error, value, default) { }
        public WslaResponse(TError error) : this(WslaResponseResponseType.Error, default, error) { }

        public static implicit operator WslaResponse<TValue, TError>(TValue value) => FromResult(value);
        public static implicit operator WslaResponse<TValue, TError>(TError error) => FromError(error);

        public static WslaResponse<TValue, TError> FromResult(TValue value) => new(WslaResponseResponseType.Success, value, default);
        public static WslaResponse<TValue, TError> FromError(TError error) => new(WslaResponseResponseType.Error, default, error);
    }

    public enum WslaResponseResponseType
    {
        Error, Success
    }

    public static class WslaResponseResponseExtensions
    {
        public static async Task<WslaResponse<TValue, Exception>> ToResponse<TValue>(this Task<TValue> task)
        {
            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static async ValueTask<WslaResponse<TValue, Exception>> ToResponse<TValue>(this ValueTask<TValue> task)
        {
            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}