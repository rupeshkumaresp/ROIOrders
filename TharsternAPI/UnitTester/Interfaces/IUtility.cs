using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;

namespace nsTharsternAPI.Interfaces
{
    public interface IUtility
    {
        HttpRequestMessage CloneHTTPRequestMessages(HttpRequestMessage requestMessage, StreamContent clonedStreamContent);
        Task<HttpResponseMessage> CloneHTTPResponseMessages(HttpRequestMessage clonedRequestMessage, HttpResponseMessage responseMessage);
        string ToQueryString(NameValueCollection nvc);

        string GetAPIMethodUrl(string apiMethod,
            NameValueCollection nvc);

        Task<object> GetObjectAsync(string apiMethod, NameValueCollection nvc);
        Task<object> GetObjectFromPostAsync(string apiMethod, string toPost);
    }
}