/// <reference path="jquery-2.0.2.js" />

(function (window, $) {

    var transports = {},
        signalr;

    signalr = function (url) {
        return new signalr.prototype.init(url);
    };

    signalr.prototype = {
        init: function (url) {
            this.url = url || "/signalr.ashx";
        },

        start: function () {
            var connection = this;

            // Negotiate
            return $.ajax(this.url + "/negotiate").then(function (res) {
                connection.id = res.connectionId;

                // Connect
                var supportedTransports = [];
                for (var key in transports) {
                    supportedTransports.push(key);
                }

                function tryConnect(currentTransport) {
                    var transportName = supportedTransports[currentTransport],
                        transport = transports[transportName];

                    return transport.connect(connection)
                        .then(function() {
                            connection.transport = transport;
                        },
                        function () {
                            return tryConnect(currentTransport + 1);
                        });
                }

                return tryConnect(1);
            });
        },

        stop: function () {
            this.transport.stop(this);
        },

        send: function (message) {
            this.transport.send(this, message);
        },

        receive: function (callback) {
            $(this).on("receive", function (e, data) {
                callback(data);
            });
        }
    };

    signalr.prototype.init.prototype = signalr.prototype;

    transports._ = {
        send: function (connection, message) {
            return $.ajax(connection.url + "/send?connectionId=" + connection.id + "&transport=" + connection.transport.name, {
                type: "POST",
                data: { data: message } 
            });
        },

        stop: function (connection) {
            return $.ajax(connection.url + "/abort?connectionId=" + connection.id + "&transport=" + connection.transport.name, {
                type: "POST"
            });
        },

        onMessage: function (connection, data) {
            var response = window.JSON.parse(data),
                messages = response.M;

            connection.messageId = data.C;

            for (var i = 0; i < messages.length; i++) {
                $(connection).triggerHandler("receive", [messages[i]]);
            }
        }
    };

    transports.serverSentEvents = {
        name: "serverSentEvents",

        connect: function (connection) {
            var d = $.Deferred();

            if (!window.EventSource) {
                // No support for SSE, just fail now
                d.reject();
                return d.promise();
            }

            try {
                connection.eventSource = new window.EventSource(connection.url + "?connectionId=" + connection.id + "&transport=" + this.name);
            } catch (e) {
                // Failure on connect
                d.reject();
                return d.promise();
            }

            connection.eventSource.addEventListener("open", function (e) {
                // Connected!
                d.resolve();
            }, false);

            connection.eventSource.addEventListener("message", function (e) {
                // Messages received
                if (e.data === "init") {
                    return;
                }
                transports._.onMessage(connection, e.data);
            }, false);

            return d.promise();
        },
        send: transports._.send,
        stop: transports._.stop
    };

    transports.longPolling = {
        name: "longPolling",

        connect: function (connection) {
            var d = $.Deferred();

            (function poll() {
                $.ajax(connection.url + "?connectionId=" + connection.id + "&transport=longPolling&messageId=" + (connection.messageId || ""), {
                    type: "POST",
                    dataType: "text"
                }).then(function (data) {
                    d.resolve();
                    transports._.onMessage(connection, data);
                    poll();
                });
            }());

            setTimeout(function () { d.resolve(); }, 250);

            return d.promise();
        },
        send: transports._.send,
        stop: transports._.stop,
    };

    $.connection = signalr;

})(window, window.jQuery);
