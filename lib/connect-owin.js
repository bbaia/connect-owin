var edge = require('edge'),
    urlParser = require('url');

var initialize = edge.func({
    assemblyFile: process.env.CONNECT_OWIN_NATIVE || (__dirname + '/clr/Connect.Owin.dll'),
    typeName: 'Connect.Owin.OwinMiddleware',
    methodName: 'Initialize'
});

var handle = edge.func({
    assemblyFile: process.env.CONNECT_OWIN_NATIVE || (__dirname + '/clr/Connect.Owin.dll'),
    typeName: 'Connect.Owin.OwinMiddleware',
    methodName: 'Handle'
});

// Returns OWIN middleware
module.exports = function (options) {
    'use strict';

    if (typeof options === 'string') {
        options = { assemblyFile: options };
    }
    else if (typeof options !== 'object') {
        throw new Error('Specify the file name of the OWIN assembly DLL or provide an options object.');
    }
    else if (typeof options.assemblyFile !== 'string') {
        throw new Error('OWIN assembly file name must be provided as a string parameter or assemblyFile options property.');
    }

    var owinAppId;

    var owinBodyParser = function (req, res, next) {
        if (req.body) return next();

        // Has body?
        if ('transfer-encoding' in req.headers ||
            ('content-length' in req.headers && req.headers['content-length'] !== '0')) {
            // Parse body 
            var buffers = [];
            req.on('data', function (d) { buffers.push(d); });
            req.on('end', function () {
                req.body = Buffer.concat(buffers);
                next();
            });
        }
        else {
            req.body = new Buffer(0);
            next();
        }
    };

    var owinMiddleware = function (req, res, next) {

        function onInitialized() {
            // Create the baseline OWIN env using properties of the request object
            var env = {
                'connect-owin.appId': owinAppId,
                'owin.RequestMethod': req.method,
                'owin.RequestPath': urlParser.parse(req.url).pathname,
                'owin.RequestPathBase': '',
                'owin.RequestProtocol': 'HTTP/' + req.httpVersion,
                'owin.RequestQueryString': urlParser.parse(req.url).query || '',
                'owin.RequestScheme': req.connection.encrypted ? 'https' : 'http',
                'owin.RequestHeaders': req.headers
            };
            if (Buffer.isBuffer(req.body)) {
                env['owin.RequestBody'] = req.body;
            }
            else if (typeof req.body === 'object') {
                env['owin.RequestBody'] = new Buffer(JSON.stringify(req.body));
            }
            else {
                var err = new Error('Invalid body format');
                err.status = 400;
                err.body = req.body;
                return next(err);
            }

            // Add options to the OWIN environment.
            // This is a good mechanism to export global node.js functions to the OWIN middleware in .NET.
            for (var i in options) {
                env['node.' + i] = options[i];
            }

            // Add per-request owin properties to the OWIN environment.
            // This is a good mechanism to allow previously running connect middleware 
            // to export request-specific node.js functions to the OWIN middleware in .NET.
            if (typeof req.owin === 'object') {
                for (var j in req.owin) {
                    env['node.' + j] = req.owin[j];
                }
            }

            // Add js functions to OWIN environment
            // This will allow the OWIN middleware in .NET to configure the 'res' object.
            env['connect-owin.setStatusCodeFunc'] = function (data, callback) {
                if (typeof data === 'number') {
                    res.statusCode = data;
                }
                callback(null, null);
            };
            env['connect-owin.setHeaderFunc'] = function (data, callback) {
                if (typeof data === 'object') {
                    for (var i in data) {
                        res.setHeader(i, data[i].join(','));
                    }
                }
                callback(null, null);
            };
            env['connect-owin.removeHeaderFunc'] = function (data, callback) {
                if (typeof data === 'string') {
                    res.removeHeader(data);
                }
                callback(null, null);
            };
            env['connect-owin.removeAllHeadersFunc'] = function (data, callback) {
                for (var i in res.headers) {
                    res.removeHeader(i);
                }
                callback(null, null);
            };
            env['connect-owin.writeFunc'] = function (data, callback) {
                if (Buffer.isBuffer(data)) {
                    res.write(data);
                }
                callback(null, null);
            };

            // Call into .NET OWIN application
            handle(env, function (error, result) {
                if (error) return next(error);

                // Consider this response complete or continue running connect pipeline?
                return result ? next() : res.end();
            });
        }

        function ensureInitialized() {
            initialize(options, function (error, result) {
                if (error) return next(error);
                // Result is a unique identifier of the OWIN middleware in .NET.
                // It is passed to the handle method so that .NET code can dispatch the request
                // to the appropriate OWIN middleware instance.
                owinAppId = result;
                onInitialized();
            });
        }

        if (owinAppId !== undefined)
            onInitialized();
        else
            ensureInitialized();
    };

    return function (req, res, next) {
        owinBodyParser(req, res, function (err) {
            if (err) return next(err);
            owinMiddleware(req, res, next);
        });
    };
};

