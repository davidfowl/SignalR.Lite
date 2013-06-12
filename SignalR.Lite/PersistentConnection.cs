using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace SignalR.Lite
{
    public abstract class PersistentConnection : HttpTaskAsyncHandler
    {
        private static readonly MessageBus messageBus = new MessageBus();

        protected Connection Connection
        {
            get;
            private set;
        }

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
                var topics = new[] { typeof(PersistentConnection).FullName, connectionId };

                if (context.Request.Url.LocalPath.EndsWith("/send"))
                {
                    return HandleSend(context, connectionId);
                }
                else
                {
                    switch (transport)
                    {
                        case "longPolling":
                            return HandleLongPolling(context, cursor, connectionId, topics);
                        case "serverSentEvents":
                            return HandleServerSentEvents(context, cursor, connectionId, topics);
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

            Connection = new Connection(messageBus, typeof(PersistentConnection).FullName);

            return OnReceived(connectionId, data);
        }

        private async Task HandleServerSentEvents(HttpContext context, string cursor, string connectionId, string[] topics)
        {
            // Disable IIS compression
            context.Request.Headers.Remove("Accept-Encoding");

            context.Response.ContentType = "text/event-stream";
            await context.Response.WriteSSEAsync("init");

            var subscription = messageBus.Subscribe(topics, cursor, (value, index) =>
            {
                var response = new PersistentResponse();
                response.Messages = new List<object>();
                response.Messages.Add(value);
                response.Cursor = index.ToString();

                return context.Response.WriteJsonSSEAsync(response);
            });

            var tcs = new TaskCompletionSource<object>();

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            subscription.Dispose();
        }

        private async Task HandleLongPolling(HttpContext context, string cursor, string connectionId, string[] topics)
        {
            context.Response.ContentType = "application/json";
            var tcs = new TaskCompletionSource<object>();

            var subscription = messageBus.Subscribe(topics, cursor, (value, index) =>
            {
                var response = new PersistentResponse();
                response.Messages = new List<object>();
                response.Messages.Add(value);
                response.Cursor = index.ToString();

                context.Response.WriteJson(response);
                tcs.TrySetResult(null);

                return Task.FromResult(0);
            });
            
            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            subscription.Dispose();
        }

        protected abstract Task OnReceived(string connectionId, string data);
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