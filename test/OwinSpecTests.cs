using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using Owin;

namespace Connect.Owin.Tests
{
    public static class OwinSpecTests
    {
        public static Task ShouldSetRequestMethod(IDictionary<string, object> env)
        {
            if ((string)env["owin.RequestMethod"] != "GET")
                throw new Exception("'owin.RequestMethod' should be equal to 'GET'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestPathAndRequestPathBase(IDictionary<string, object> env)
        {
            if ((string)env["owin.RequestPath"] != "/the/path")
                throw new Exception("'owin.RequestPath' should be equal to '/the/path'");
            if ((string)env["owin.RequestPathBase"] != "")
                throw new Exception("'owin.RequestPathBase' should be equal to ''");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestProtocol(IDictionary<string, object> env)
        {
            if ((string)env["owin.RequestProtocol"] != "HTTP/1.1")
                throw new Exception("'owin.RequestProtocol' should be equal to 'HTTP/1.1'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestQueryString(IDictionary<string, object> env)
        {
            if ((string)env["owin.RequestQueryString"] != "a=the&b=query")
                throw new Exception("'owin.RequestQueryString' should be equal to 'a=the&b=query'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestScheme(IDictionary<string, object> env)
        {
            if ((string)env["owin.RequestScheme"] != "http")
                throw new Exception("'owin.RequestScheme' should be equal to 'http'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestHeaders(IDictionary<string, object> env)
        {
            if (!(env["owin.RequestHeaders"] is IDictionary<string, string[]>))
                throw new Exception("'owin.RequestHeaders' should be an IDictionary<string, string[]>");
            if (((IDictionary<string, string[]>)env["owin.RequestHeaders"])["Content-Type"][0] != "mocha/test")
                throw new Exception("'Content-Type' header should be equal to 'mocha-test'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestBody(IDictionary<string, object> env)
        {
            if (!(env["owin.RequestBody"] is Stream))
                throw new Exception("'owin.RequestBody' should be a Stream");
            StreamReader r = new StreamReader((Stream)env["owin.RequestBody"]);
            if (r.ReadToEnd() != "Hello OWIN!")
                throw new Exception("Request body should be equal to 'Hello OWIN!'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetRequestBodyToStreamNullIfNoRequestBody(IDictionary<string, object> env)
        {
            if (env["owin.RequestBody"] != Stream.Null)
                throw new Exception("Request body should be equal to 'Stream.Null'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetResponseHeaders(IDictionary<string, object> env)
        {
            if (!(env["owin.ResponseHeaders"] is IDictionary<string, string[]>))
                throw new Exception("'owin.ResponseHeaders' should be an IDictionary<string, string[]>");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetResponseBody(IDictionary<string, object> env)
        {
            if (!(env["owin.ResponseBody"] is Stream))
                throw new Exception("'owin.ResponseBody' should be a Stream");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetCallCancelled(IDictionary<string, object> env)
        {
            if (!(env["owin.CallCancelled"] is System.Threading.CancellationToken))
                throw new Exception("'owin.CallCancelled' should be equal a System.Threading.CancellationToken");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSetVersionWith10(IDictionary<string, object> env)
        {
            if ((string)env["owin.Version"] != "1.0")
                throw new Exception("'owin.Version' should be equal to '1.0'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldGetResponseHeaders(IDictionary<string, object> env)
        {
            env["owin.ResponseStatusCode"] = 200;
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Content-Type", new string[] { "mocha/test" });

            return Task.FromResult<object>(null);
        }

        public static async Task ShouldGetResponseBody(IDictionary<string, object> env)
        {
            env["owin.ResponseStatusCode"] = 200;
            StreamWriter w = new StreamWriter((Stream)env["owin.ResponseBody"]);
            w.Write("Hello from C#");
            await w.FlushAsync();
        }

        public static Task ShouldGetResponseStatusCode(IDictionary<string, object> env)
        {
            env["owin.ResponseStatusCode"] = 201;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSupportMultiValuedHttpHeaders(IDictionary<string, object> env)
        {
            if (((IDictionary<string, string[]>)env["owin.RequestHeaders"])["Set-Cookie"][0] != "a=b;Path=/;" ||
                        ((IDictionary<string, string[]>)env["owin.RequestHeaders"])["Set-Cookie"][1] != "c=d;Path=/;")
                throw new Exception("'Set-Cookie' header should be equal to 'text/plain, text/html'");
            if (((IDictionary<string, string[]>)env["owin.RequestHeaders"])["Accept"][0] != "text/plain, text/html")
                throw new Exception("'Accept' header should be equal to 'text/plain, text/html'");
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Cache-Control", new string[] { "must-revalidate", "private", "max-age=0" });
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }
    }
}
