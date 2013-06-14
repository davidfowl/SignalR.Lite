using System;
using System.Threading.Tasks;
using System.Web;

namespace SignalR.Lite
{
    public abstract class PersistentConnection : HttpTaskAsyncHandler
    {
        private readonly static MessageBus _messageBus = new MessageBus();

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
                        return HandleSSE(context, cursor, signals);
                    case "longPolling":
                        return HandlingLongPolling(context, cursor, signals);
                    default:
                        throw new NotSupportedException();
                }

            }

            return Task.FromResult(0);
        }

        private Task HandleSend(HttpContext context, string connectionId)
        {
            var data = context.Request.Form["data"];

            return OnReceived(connectionId, data);
        }

        protected abstract Task OnReceived(string connectionId, string data);

        protected Task Broadcast(object data)
        {
            return _messageBus.Publish(GetType().FullName, data);
        }

        /// <summary>
        /// An implementation of Server sent events (http://en.wikipedia.org/wiki/Server-sent_events)
        /// </summary>
        private async Task HandleSSE(HttpContext context, string cursor, string[] signals)
        {
            var tcs = new TaskCompletionSource<object>();

            context.Response.ContentType = "text/event-stream";

            // Tell IIS's dynamic compression module to back off.
            // The module tries to buffer enough content before flushing it over the wire,
            // we'd prefer if it would get out of the way.
            context.Request.Headers.Remove("Accept-Encoding");

            context.Response.WriteSSE("init");

            IDisposable subscription = _messageBus.Subscribe(signals, cursor, (messages, index) =>
            {
                var response = new
                {
                    C = index,
                    M = messages
                };

                context.Response.WriteJsonSSE(response);
            });

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            subscription.Dispose();
        }

        private async Task HandlingLongPolling(HttpContext context, string cursor, string[] signals)
        {
            var tcs = new TaskCompletionSource<object>();

            context.Response.ContentType = "application/json";

            IDisposable subscription = _messageBus.Subscribe(signals, cursor, (messages, index) =>
            {
                var response = new
                {
                    C = index,
                    M = messages
                };

                context.Response.WriteJson(response);
                tcs.TrySetResult(null);
            },
            terminal: true);

            context.Response.ClientDisconnectedToken.Register(() =>
            {
                tcs.TrySetResult(null);
            });

            await tcs.Task;
            subscription.Dispose();
        }
    }
}