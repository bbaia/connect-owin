var owin = require('..'),
    connect = require('connect'),
    assert = require('assert'),
    request = require('supertest');

describe('owin()', function () {
    describe('to implement OWIN 1.0 specifications', function () {
        it('should set `owin.RequestMethod`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestMethod'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.RequestPath` and `owin.RequestPathBase`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestPathAndRequestPathBase'
            }));

            request(app)
                .get('/the/path')
                .expect(200, done);
        });

        it('should set `owin.RequestProtocol`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestProtocol'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.RequestQueryString`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestQueryString'
            }));

            request(app)
                .get('/the/path?a=the&b=query')
                .expect(200, done);
        });

        it('should set `owin.RequestScheme`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestScheme'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.RequestHeaders`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestHeaders'
            }));

            request(app)
                .get('/')
                .set('Content-Type', 'mocha/test')
                .expect(200, done);
        });

        it('should set `owin.RequestBody`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestBody'
            }));

            request(app)
                .post('/')
                .send('Hello OWIN!')
                .expect(200, done);
        });

        it('should set `owin.RequestBody` to `Stream.Null` if no request body', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetRequestBodyToStreamNullIfNoRequestBody'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.ResponseHeaders`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetResponseHeaders'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.ResponseBody`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetResponseBody'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.CallCancelled`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetCallCancelled'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should set `owin.Version` with `1.0`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSetVersionWith10'
            }));

            request(app)
                .get('/')
                .expect(200, done);
        });

        it('should get `owin.ResponseHeaders`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldGetResponseHeaders'
            }));

            request(app)
                .get('/')
                .expect('Content-Type', 'mocha/test')
                .expect(200, done);
        });

        it('should get `owin.ResponseBody`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldGetResponseBody'
            }));

            request(app)
                .get('/')
                .expect(200, 'Hello from C#', done);
        });

        it('should get `owin.ResponseHeaders` and `owin.ResponseBody`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldGetResponseHeadersAndBody'
            }));

            request(app)
                .get('/')
                .expect('Content-Type', 'mocha/test')
                .expect(200, 'Hello from C#', done);
        });

        it('should get `owin.ResponseStatusCode`', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldGetResponseStatusCode'
            }));

            request(app)
                .get('/')
                .expect(201, done);
        });

        it('should support multi-valued HTTP headers', function (done) {
            var app = connect();

            app.use(owin({
                assemblyFile: 'test/Connect.Owin.Tests.dll',
                typeName: 'Connect.Owin.Tests.OwinSpecTests',
                methodName: 'ShouldSupportMultiValuedHttpHeaders'
            }));

            request(app)
                .get('/')
                .set('Set-Cookie', ['a=b;Path=/;', 'c=d;Path=/;']) // Support multiple header with the same name
                .set('Accept', ['text/plain', 'text/html'])
                .expect('Cache-Control', 'must-revalidate,private,max-age=0')
                .expect(200, done);
        });
    });
});