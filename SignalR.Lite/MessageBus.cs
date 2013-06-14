#region HolyShitComplexCode

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SignalR.Lite
{
    public delegate void Callback(IEnumerable<object> messages, int index);

    public class MessageBus
    {
        // List of subscriptions
        private readonly ConcurrentDictionary<string, List<Callback>> _subscriptions = new ConcurrentDictionary<string, List<Callback>>();

        // List of messages
        private readonly List<object> _messages = new List<object>();

        private static readonly Callback NoopCallback = (messages, index) => { };

        public Task Publish(string signal, object data)
        {
            int index;
            lock (_messages)
            {
                _messages.Add(data);
                index = _messages.Count - 1;
            }

            List<Callback> callbacks;
            if (_subscriptions.TryGetValue(signal, out callbacks))
            {
                lock (callbacks)
                {
                    var messages = new[] { data };

                    for (int i = callbacks.Count - 1; i >= 0; i--)
                    {
                        Callback callback = callbacks[i];
                        callback(messages, index);
                    }
                }
            }

            // Not really async (we're lying)
            return Task.FromResult(0);
        }

        public IDisposable Subscribe(string[] signals, string cursor, Callback callback, bool terminal = false)
        {
            // Parse the cursor
            int from;
            if (!Int32.TryParse(cursor, out from))
            {
                from = -1;
            }

            var callbackLock = new object();

            // Wrap the user callback in another callback so we can add some behavior
            Callback wrappedCallback = (result, index) =>
            {
                // Lock so that we do have overlapping calls to the same callback.
                // We want to avoid this since ASP.NET will explode when doing 
                // overlapping async operations (like async flush).
                lock (callbackLock)
                {
                    // Terminal means we're only ever going to fire this callback once
                    if (terminal)
                    {
                        // We don't need to interlock anything but this is a nice pattern.
                        // Exchange exchange reference to the callback with Noop delegate.
                        // The value returned will be the previous value, that is
                        // we'll exchange it with noop and get back the original callback the
                        // first time, then subsequent invocations will reference the Noop callback
                        Callback cb = Interlocked.Exchange(ref callback, NoopCallback);

                        cb(result, index);
                    }
                    else
                    {
                        // Just invoke the callback
                        callback(result, index);
                    }
                }
            };

            // If we have a cursor then invoke the callback immediately
            if (from >= 0)
            {
                lock (_messages)
                {
                    // TODO: This needs to filter based on the list of signals
                    // Get all messages we missed
                    var values = _messages.Skip(from + 1);

                    if (values.Any())
                    {
                        // Invoke the callback with the values we have
                        // The new cursor is the invoke of the messages
                        wrappedCallback(values, _messages.Count - 1);
                    }
                }
            }

            foreach (var topic in signals)
            {
                List<Callback> callbacks;
                if (!_subscriptions.TryGetValue(topic, out callbacks))
                {
                    callbacks = new List<Callback>();
                    _subscriptions[topic] = callbacks;
                }

                // Add it to the list of callbacks
                lock (callbacks)
                {
                    callbacks.Add(wrappedCallback);
                }
            }

            return new Subscription(_subscriptions, signals, wrappedCallback);
        }

        private class Subscription : IDisposable
        {
            private readonly IDictionary<string, List<Callback>> _subscriptions;
            private readonly string[] _signals;
            private readonly Callback _callback;

            public Subscription(IDictionary<string, List<Callback>> subscriptions, string[] signals, Callback callback)
            {
                _subscriptions = subscriptions;
                _signals = signals;
                _callback = callback;
            }

            public void Dispose()
            {
                foreach (var signal in _signals)
                {
                    List<Callback> callbacks;
                    if (_subscriptions.TryGetValue(signal, out callbacks))
                    {
                        // Remove the callback when the subscription is disposed
                        lock (callbacks)
                        {
                            callbacks.Remove(_callback);
                        }
                    }
                }
            }
        }
    }
}

#endregion