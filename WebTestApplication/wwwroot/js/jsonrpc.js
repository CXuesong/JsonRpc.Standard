/// <reference path="../lib/jquery-3.2.1.min.js"/>

function JsonRpcClient(/**@type string*/ endpointUrl) {
    this._endpointUrl = endpointUrl.trim();
    this._id = 0;
    if (endpointUrl.match(/^wss?:\/\//i)) {
        // JSON-RPC over WebSocket
        this._ws = new WebSocket(endpointUrl);
        /**@type {Object.<string, JQueryDeferred>} */
        this._impendingResponses = {};
        /**@type JsonRpcClient */
        var _this = this;
        this._ws.addEventListener("message",
            function(e) {
                if (typeof (e.data) === "string") {
                    var data = JSON.parse(e.data);
                    if (data.jsonrpc !== "2.0" || data.id == null) {
                        console.warn("Ignored invalid JSON-RPC message from server.", data);
                    }
                    var d = _this._impendingResponses[data.id];
                    delete _this._impendingResponses[data.id];
                    if (data.error) {
                        d.reject(data.error);
                    } else {
                        d.resolve(data.result);
                    }
                } else {
                    console.warn("Received non-JSON-RPC message from server.", e.data);
                }
            });
    }
}

JsonRpcClient.prototype.send = function (methodName, parameters, id) {
    var body = {
        jsonrpc: "2.0",
        method: methodName,
        params: parameters,
    };
    if (id === undefined)
        body.id = this._id++;
    else if (id !== null)
        body.id = id;
    var d = $.Deferred();
    if (this._ws) {
        if (this._ws.readyState !== WebSocket.OPEN) {
            throw new Error("WebSocket is not open. readyState = " + this._ws.readyState + ".");
        }
        if (id !== null)
	        this._impendingResponses[body.id] = d;
        else
	        d.resolve();
        this._ws.send(JSON.stringify(body));
    } else {
        $.post({
            url: this._endpointUrl,
            contentType: "application/json",
            data: JSON.stringify(body),
        }).done(function(response, status, xhr) {
            if (response.error) {
                d.reject(response.error, xhr.status);
            } else {
                d.resolve(response.result, xhr.status);
            }
        }).fail(function(xhr, status, error) {
            var response = xhr.responseJSON;
            d.reject(response.error, xhr.status);
        });
    }
    return d;
};

JsonRpcClient.prototype.request = function (methodName, parameters) {
    this.send(methodName, parameters);
};

JsonRpcClient.prototype.notify = function (methodName, parameters) {
    this.send(methodName, parameters, null);
};

JsonRpcClient.prototype.close = function () {
    if (this._ws) {
        this._ws.close(1000, "Client closing.");
    }
};
