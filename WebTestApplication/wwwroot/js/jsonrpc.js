/// <reference path="../lib/jquery-3.2.1.min.js"/>

function JsonRpcClient(endpointUrl) {
    this._endpointUrl = endpointUrl;
    this._id = 0;
}

JsonRpcClient.prototype.send = function(methodName, parameters, id) {
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
    $.post(this._endpointUrl, JSON.stringify(body)).done(function(response, status, xhr) {
        if (response.error) {
            d.reject(response.error, xhr.status);
        } else {
            d.resolve(response.result, xhr.status);
        }
    }).fail(function(xhr, status, error) {
        var response = xhr.responseJSON;
        d.reject(response.error, xhr.status);
    });
    return d;
};

JsonRpcClient.prototype.request = function(methodName, parameters) {
    this.send(methodName, parameters);
};

JsonRpcClient.prototype.notify = function(methodName, parameters) {
    this.send(methodName, parameters, null);
};
