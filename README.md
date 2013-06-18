SignalR.Lite
============
A lightweight version of SignalR that serves as a learning tool for how the real framework actually works. 

It shows the following:
e
- The new [HttpTaskAsyncHandler](http://msdn.microsoft.com/en-us/library/system.web.httptaskasynchandler.aspx) and [ClientDisconnectedToken](http://msdn.microsoft.com/en-us/library/system.web.httpresponse.clientdisconnectedtoken.aspx) in .NET 4.5.
- A very naive and basic in memory message bus implementation.
- Server Sent events and LongPolling transport implementations on the server and client.
- Transport fallback on the client side.
