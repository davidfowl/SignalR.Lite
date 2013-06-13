using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using SignalR.Lite;

namespace SignalR.Lite
{
    public abstract class PersistentConnection : HttpTaskAsyncHandler
    {
        private readonly static MessageBus _messageBus = new MessageBus();

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
                var transport = context.Request.QueryString["transport"];
                var connectionId = context.Request.QueryString["connectionId"];
                var cursor = context.Request.QueryString["messageId"];

                var signals = new[] { GetType().FullName, connectionId };

                if (context.Request.Url.LocalPath.EndsWith("/send"))
                {
                    return HandleSend(context, connectionId);
                }

                switch (transport)
                {
                    case "serverSentEvents":
                        return HandleSSE(context, transport, connectionId, cursor, signals);
                    case "longPolling":
                        return HandlingLongPolling(context, transport, connectionId, cursor, signals);
                    default:
                        throw new NotSupportedException();
                }

            }

            return Task.FromResult(0);
        }

        private Task HandleSend(HttpContext context, string connectionId)
        {
            var data = context.Request.Form["data"];

            Connection = new Connection(_messageBus, GetType().FullName);

            return OnReceived(connectionId, data);
        }

        protected abstract Task OnReceived(string connectionId, string data);

        private async Task HandleSSE(HttpContext context, string transport, string connectionId, string cursor, string[] signals)
        {
            var tcs = new TaskCompletionSource<object>();

            context.Response.ContentType = "text/event-stream";

            // GTFO IIS
            context.Request.Headers.Remove("Accept-Encoding");

            context.Response.WriteSSE("init");

            IDisposable sub = _messageBus.Subscribe(signals, cursor, (value, index) =>
            {
                context.Response.WriteJsonSSE(new Response(value, index));
            });

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            sub.Dispose();
        }

        private async Task HandlingLongPolling(HttpContext context, string transport, string connectionId, string cursor, string[] signals)
        {
            var tcs = new TaskCompletionSource<object>();

            context.Response.ContentType = "application/json";

            IDisposable sub = _messageBus.Subscribe(signals, cursor, (value, index) =>
            {
                context.Response.WriteJson(new Response(value, index));
                tcs.TrySetResult(null);
            },
            terminal: true);

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            sub.Dispose();
        }
    }

    class Response
    {
        public Response(IEnumerable<object> messages, int index)
        {
            C = index.ToString();
            M = messages;
        }

        public string C { get; set; }
        public IEnumerable<object> M { get; set; }
    }

    public class Connection
    {
        private MessageBus _messageBus;
        private string _defaultSignal;

        public Connection(MessageBus messageBus, string defaultSignal)
        {
            _messageBus = messageBus;
            _defaultSignal = defaultSignal;
        }

        public Task Broadcast(object data)
        {
            return _messageBus.Publish(_defaultSignal, data);
        }
    }
}