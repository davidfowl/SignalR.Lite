using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace SignalR.Lite
{
    public static class ResponseExtensions
    {
        public static void WriteJson(this HttpResponse response, object value)
        {
            response.Write(JsonConvert.SerializeObject(value));
        }

        public static void WriteSSE(this HttpResponse response, string value)
        {
            // See the server sent events data framing format (http://www.w3.org/TR/2011/WD-eventsource-20110310/#server-sent-events-intro)
            response.Write("data:" + value + "\n\n");
            response.Flush();
        }

        public static void WriteJsonSSE(this HttpResponse response, object value)
        {
            response.WriteSSE(JsonConvert.SerializeObject(value)); 
        }
    }
}