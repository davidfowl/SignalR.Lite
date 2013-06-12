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

        public static Task WriteSSEAsync(this HttpResponse response, string value)
        {
            response.Write("data:" + value + "\n\n");
            return response.FlushAsync();
        }

        public static Task WriteJsonSSEAsync(this HttpResponse response, object value)
        {
            return response.WriteSSEAsync(JsonConvert.SerializeObject(value)); 
        }
    }
}