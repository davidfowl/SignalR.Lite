using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignalR.Lite
{
    public delegate Task Callback(object value, int cusor);

    public class MessageBus
    {
        // List of subscriptions
        private readonly Dictionary<string, HashSet<Callback>> _subscriptions = new Dictionary<string, HashSet<Callback>>();

        // List of messages
        private readonly List<object> _messages = new List<object>();

        public async Task Publish(string topic, object data)
        {
            _messages.Add(data);
            int index = _messages.Count - 1;

            HashSet<Callback> callbacks;
            if (_subscriptions.TryGetValue(topic, out callbacks))
            {
                foreach (var cb in callbacks)
                {
                    await cb(data, index);
                }
            }
        }

        public IDisposable Subscribe(string[] topics, string cursor, Callback callback)
        {
            int from = String.IsNullOrEmpty(cursor) ? -1 : Int32.Parse(cursor);

            Callback cb = async (result, index) =>
            {
                // Stay within range of the cursor
                if (from >= _messages.Count || from < index)
                {
                    await callback(result, index);
                }
            };

            foreach (var topic in topics)
            {
                HashSet<Callback> callbacks;
                if (!_subscriptions.TryGetValue(topic, out callbacks))
                {
                    callbacks = new HashSet<Callback>();
                    _subscriptions[topic] = callbacks;
                }

                callbacks.Add(cb);
            }

            return new Subscription(_subscriptions, topics, cb);
        }

        private class Subscription : IDisposable
        {
            private readonly IDictionary<string, HashSet<Callback>> _subscriptions;
            private readonly string[] _topics;
            private readonly Callback _callback;

            public Subscription(IDictionary<string, HashSet<Callback>> subscriptions, string[] topics, Callback callback)
            {
                _subscriptions = subscriptions;
                _topics = topics;
                _callback = callback;
            }

            public void Dispose()
            {
                foreach (var topic in _topics)
                {
                    HashSet<Callback> callbacks;
                    if (_subscriptions.TryGetValue(topic, out callbacks))
                    {
                        callbacks.Remove(_callback);
                    }
                }
            }
        }
    }
}