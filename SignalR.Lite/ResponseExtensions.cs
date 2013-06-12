using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace SignalR.Lite
{
    public static class ResponseExtensions
    {
        public static Task FlushAsync(this HttpResponse response)
        {
            return Task.Factory.FromAsync((cb, state) => response.BeginFlush(cb, state), ar => response.EndFlush(ar), null);
        }

        public static void WriteJson(this HttpResponse response, object value)
        {
            response.Write(JsonConvert.SerializeObject(value));
        }

        public static Task WriteSSE(this HttpResponse response, object value)
        {
            response.Write("data:" + JsonConvert.SerializeObject(value) + "\n\n");
            return response.FlushAsync();
        }
    }
}