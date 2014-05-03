var owin = require('..'),
    connect = require('connect'),
    assert = require('assert'),
    request = require('supertest');

describe('owin()', function () {
    it('should require an assembly file', function (done) {
        var app = connect();

        assert.throws(
            function () {
                app.use(owin())
            },
            'Specify the file name of the OWIN assembly DLL or provide an options object.'
        );

        assert.throws(
            function () {
                app.use(owin({}))
            },
            'OWIN assembly file name must be provided as a string parameter or assemblyFile options property.'
        );

        done();
    });

    it('should use Startup/Configuration for type/method name by default', function (done) {
        var app = connect();

        app.use(owin('test/Connect.Owin.Tests.dll'));

        request(app)
            .get('/')
            .expect(200, done);
    });

    it('should support AppFunc calling convention', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldSupportAppFuncCallingConvention'
        }));

        request(app)
            .get('/')
            .expect(200, done);
    });

    it('should next() after default app', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldNextAfterDefaultApp'
        }));

        app.use(function (req, res) {
            res.statusCode = 500;
            res.end();
        });

        request(app)
            .get('/')
            .expect('Owin-Data', 'Hello!')
            .expect(500, done);
    });

    it('should next() when `connect-owin.continue` is set to true', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldNextWhenContinueIsSetToTrue'
        }));

        app.use(function (req, res) {
            res.statusCode = 500;
            res.end();
        });

        request(app)
            .get('/')
            .expect('Owin-Data', 'Hello!')
            .expect(500, done);
    });

    it('should support multiple OWIN middlewares', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldSupportMultipleOwinMiddlewares_App1'
        }));

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldSupportMultipleOwinMiddlewares_App2'
        }));

        app.use(function (req, res) {
            res.statusCode = 200;
            res.end();
        });

        request(app)
            .get('/')
            .expect('Owin-App1', 'App1')
            .expect('Owin-App2', 'App2')
            .expect(200, done);
    });

    it('have to support `HttpStatusCode` enum', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'HaveToSupportHttpStatusCodeEnum'
        }));

        request(app)
            .get('/')
            .expect(201, done);
    });

    it('should use case-insensitive HTTP headers', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldUseCaseInsensitiveHttpHeaders'
        }));

        request(app)
            .get('/')
            .set('request-header', 'requestValue')
            .expect('response-header', 'responseValue')
            .expect(200, done);
    });

    it('should export global node.js data and functions', function (done) {
        var app = connect();

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldExportGlobalNodeDataAndFunctions',
            key: 'value',
            func: function (s, callback) {
                callback(null, 'Hello ' + s);
            }
        }));

        request(app)
            .get('/')
            .expect(200, done);
    });

    it('should export request-specific node.js data and functions', function (done) {
        var app = connect();

        app.use(function (req, res, next) {
            req.owin = {
                key: 'value',
                func: function (s, callback) {
                    callback(null, 'Hello ' + s);
                }
            };
            next();
        });

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldExportRequestSpecificNodeDataAndFunctions'
        }));

        request(app)
            .get('/')
            .expect(200, done);
    });

    it('should work with `connect.bodyParser()`', function (done) {
        var app = connect();

        app.use(connect.urlencoded())
        app.use(connect.json())

        app.use(owin({
            assemblyFile: 'test/Connect.Owin.Tests.dll',
            typeName: 'Connect.Owin.Tests.OwinTests',
            methodName: 'ShouldWorkWithConnectBodyParser'
        }));

        request(app)
            .post('/')
            .send({ msg: "Hello OWIN!" })
            .expect(200, done);
    });
});