using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;

namespace Wsla.Server
{
    public static class CoordinatorServerExtensions
    {
        public static ProviderException ToProviderException(this RestResponse response)
        {
            var code = (ResponseStatus)response.Code;
            return new ProviderException((ResponseStatus)response.Code, response.Message);
        }
    }
}