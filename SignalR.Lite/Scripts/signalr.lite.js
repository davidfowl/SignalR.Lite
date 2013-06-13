/// <reference path="jquery-2.0.2.js" />

(function (window, $) {

    var transports = {},
        signalr;

    signalr = function (url) {
        return new signalr.prototype.init(url);
    };

    signalr.prototype = {
        init: function (url) {
            this.url = url;
        },

        start: function () {
            var connection = this;

            // Negotiate
            return $.ajax(connection.url + "/negotiate").then(function (response) {
                connection.id = response.connectionId;

                // Connect
                var supportedTransports = [];
                for (var key in transports) {
                    if (key === "_") {
                        continue;
                    }
                    supportedTransports.push(key);
                }

                function tryConnect(index) {
                    var transportName = supportedTransports[index],
                        transport = transports[transportName];

                    return transport.start(connection)
                        .then(function() {
                            connection.transport = transport;
                        },
                        function () {
                            return tryConnect(index + 1);
                        });
                }

                return tryConnect(0);
            });
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

        onReceive: function (connection, data) {
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

        start: function (connection) {
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

            connection.eventSource.addEventListener("open", function () {
                // Connected!
                d.resolve();
            }, false);

            connection.eventSource.addEventListener("message", function (e) {
                // Messages received
                if (e.data === "init") {
                    return;
                }
                transports._.onReceive(connection, e.data);
            }, false);

            return d.promise();
        },

        send: transports._.send
    };

    transports.longPolling = {
        name: "longPolling",

        start: function (connection) {
            var d = $.Deferred(),
                that = this;

            (function poll() {
                $.ajax(connection.url + "?connectionId=" + connection.id + "&transport=" + that.name + "&messageId=" + (connection.messageId || ""), {
                    type: "POST",
                    dataType: "text"
                }).then(function (data) {
                    d.resolve();
                    transports._.onReceive(connection, data);
                    poll();
                });
            }());

            setTimeout(function () { d.resolve(); }, 250);

            return d.promise();
        },

        send: transports._.send
    };

    $.connection = signalr;

})(window, window.jQuery);
