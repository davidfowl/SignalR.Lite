using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace SignalR.Lite
{
    public class SignalR : HttpTaskAsyncHandler
    {
        private static readonly MessageBus messageBus = new MessageBus();

        public override Task ProcessRequestAsync(HttpContext context)
        {
            if (context.Request.Url.LocalPath.EndsWith("/negotiate"))
            {
                var negotiation = new
                {
                    connectionId = Guid.NewGuid()
                };

                context.Response.ContentType = "application/json";
                context.Response.WriteJson(negotiation);
            }
            else
            {
                string transport = context.Request.QueryString["transport"];
                string cursor = context.Request.QueryString["messageId"];
                string connectionId = context.Request.QueryString["connectionId"];

                if (context.Request.Url.LocalPath.EndsWith("/send"))
                {
                    return HandleSend(context, connectionId);
                }
                else
                {
                    switch (transport)
                    {
                        case "longPolling":
                            return HandleLongPolling(context, cursor, connectionId);
                        case "serverSentEvents":
                            return HandleServerSentEvents(context, cursor, connectionId);
                        default:
                            throw new NotSupportedException();
                    }
                }
            }

            return Task.FromResult(0);
        }

        private Task HandleSend(HttpContext context, string connectionId)
        {
            string data = context.Request.Form["data"];

            var persistentConnection = new PersistentConnection();
            persistentConnection.Connection = new Connection(messageBus, typeof(PersistentConnection).FullName);

            return persistentConnection.OnReceived(connectionId, data);
        }

        private async Task HandleServerSentEvents(HttpContext context, string cursor, string connectionId)
        {
            var tcs = new TaskCompletionSource<object>();
            var topics = new[] { typeof(PersistentConnection).FullName, connectionId };

            // Disable IIS caching
            context.Request.Headers.Remove("Accept-Encoding");

            context.Response.ContentType = "text/event-stream";

            context.Response.Write("data: init\n\n");
            await context.Response.FlushAsync();

            var subscription = messageBus.Subscribe(topics, cursor, (value, index) =>
            {
                var response = new PersistentResponse();
                response.Messages = new List<object>();
                response.Messages.Add(value);
                response.Cursor = index.ToString();

                return context.Response.WriteSSE(response);
            });

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
                subscription.Dispose();
            });

            await tcs.Task;
        }

        private async Task HandleLongPolling(HttpContext context, string cursor, string connectionId)
        {
            throw new NotImplementedException();
        }
    }

    public class PersistentResponse
    {
        /// <summary>
        /// The cursor representing what the client saw last
        /// </summary>
        [JsonProperty("C")]
        public string Cursor { get; set; }

        /// <summary>
        /// The messages received by this client. A list of json objects.
        /// </summary>
        [JsonProperty("M")]
        public List<object> Messages { get; set; }
    }

    public class PersistentConnection
    {
        public Connection Connection { get; set; }

        public Task OnReceived(string connectionId, string data)
        {
            return Connection.Broadcast(data);
        }
    }

    public class Connection
    {
        private readonly MessageBus _bus;
        private readonly string _defaultSignal;

        public Connection(MessageBus bus, string defaultSignal)
        {
            _bus = bus;
            _defaultSignal = defaultSignal;
        }

        public Task Send(string connectionId, object value)
        {
            return _bus.Publish(connectionId, value);
        }

        public Task Broadcast(object value)
        {
            return _bus.Publish(_defaultSignal, value);
        }
    }
}