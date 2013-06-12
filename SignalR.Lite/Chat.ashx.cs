using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace SignalR.Lite
{
    public class Chat : PersistentConnection
    {
        protected override Task OnReceived(string connectionId, string data)
        {
            return Connection.Broadcast(data);
        }
    }
}