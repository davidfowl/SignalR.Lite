#region HolyShitComplexCode

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;

namespace SignalR.Lite
{
    public delegate void Callback(IEnumerable<object> value, int cusor);

    public class MessageBus
    {
        // List of subscriptions
        private readonly ConcurrentDictionary<string, List<Callback>> _subscriptions = new ConcurrentDictionary<string, List<Callback>>();

        // List of messages
        private readonly List<object> _messages = new List<object>();

        public Task Publish(string topic, object data)
        {
            int index;
            lock (_messages)
            {
                _messages.Add(data);
                index = _messages.Count - 1;
            }

            List<Callback> callbacks;
            if (_subscriptions.TryGetValue(topic, out callbacks))
            {
                lock (callbacks)
                {
                    for (int i = callbacks.Count - 1; i >= 0; i--)
                    {
                        var cb = callbacks[i];
                        cb(new[] { data }, index);
                    }
                }
            }

            return Task.FromResult(0);
        }

        public IDisposable Subscribe(string[] topics, string cursor, Callback callback, bool terminal = false)
        {
            int from;
            if (!Int32.TryParse(cursor, out from))
            {
                from = -1;
            }

            var lockObj = new object();

            Callback cb = (result, index) =>
            {
                lock (lockObj)
                {
                    if (terminal)
                    {
                        Interlocked.Exchange(ref callback, (r, i) => { }).Invoke(result, index);
                    }
                    else
                    {
                        callback(result, index);
                    }
                }
            };

            if (from >= 0)
            {
                lock (_messages)
                {
                    var values = _messages.Skip(from);

                    if (values.Any())
                    {
                        cb(values, _messages.Count - 1);
                    }
                }
            }

            foreach (var topic in topics)
            {
                List<Callback> callbacks;
                if (!_subscriptions.TryGetValue(topic, out callbacks))
                {
                    callbacks = new List<Callback>();
                    _subscriptions[topic] = callbacks;
                }

                lock (callbacks)
                {
                    callbacks.Add(cb);
                }
            }

            return new Subscription(_subscriptions, topics, cb);
        }

        private class Subscription : IDisposable
        {
            private readonly IDictionary<string, List<Callback>> _subscriptions;
            private readonly string[] _topics;
            private readonly Callback _callback;

            public Subscription(IDictionary<string, List<Callback>> subscriptions, string[] topics, Callback callback)
            {
                _subscriptions = subscriptions;
                _topics = topics;
                _callback = callback;
            }

            public void Dispose()
            {
                foreach (var topic in _topics)
                {
                    List<Callback> callbacks;
                    if (_subscriptions.TryGetValue(topic, out callbacks))
                    {
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