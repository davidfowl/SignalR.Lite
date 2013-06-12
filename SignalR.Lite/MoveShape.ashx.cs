using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SignalR.Lite
{
    public class MoveShape : PersistentConnection
    {
        protected override Task OnReceived(string connectionId, string data)
        {
            return Connection.Broadcast(data);
        }
    }
}