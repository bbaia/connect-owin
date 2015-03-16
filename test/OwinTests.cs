using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;

using Owin;

namespace Connect.Owin.Tests
{
    // OWIN application delegate
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Use(new Func<AppFunc, AppFunc>(next => env =>
            {
                env["owin.ResponseStatusCode"] = 200;

                return Task.FromResult<object>(null);
            }));
        }
    }

    public static class OwinTests
    {
        public static Task ShouldSupportAppFuncCallingConvention(IDictionary<string, object> env)
        {
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static void ShouldNextAfterDefaultApp(IAppBuilder app)
        {
            app.Use(new Func<AppFunc, AppFunc>(next => env =>
            {
                ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                    "Owin-Data", new string[] { "Hello!" });
                return next(env);
            }));
        }

        public static Task ShouldNextWhenContinueIsSetToTrue(IDictionary<string, object> env)
        {
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Owin-Data", new string[] { "Hello!" });
            env["connect-owin.continue"] = true;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSupportMultipleOwinMiddlewares_App1(IDictionary<string, object> env)
        {
            if (!env.ContainsKey("connect-owin.appId"))
                throw new Exception("'connect-owin.appId' should exists");
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Owin-App1", new string[] { "App1" });
            env["connect-owin.continue"] = true;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldSupportMultipleOwinMiddlewares_App2(IDictionary<string, object> env)
        {
            if (!env.ContainsKey("connect-owin.appId"))
                throw new Exception("'connect-owin.appId' should exists");
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "Owin-App2", new string[] { "App2" });
            env["connect-owin.continue"] = true;

            return Task.FromResult<object>(null);
        }

        public static Task HaveToSupportHttpStatusCodeEnum(IDictionary<string, object> env)
        {
            env["owin.ResponseStatusCode"] = HttpStatusCode.Created;

            return Task.FromResult<object>(null);
        }

        public static Task ShouldUseCaseInsensitiveHttpHeaders(IDictionary<string, object> env)
        {
            if (!((IDictionary<string, string[]>)env["owin.RequestHeaders"]).ContainsKey("Request-HEADER"))
                throw new Exception("'Request-HEADER' header should exists");
            ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                "RESPONSE-Header", new string[] { "responseValue" });
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        public static async Task ShouldExportGlobalNodeDataAndFunctions(IAppBuilder app)
        {
            // value
            if (!app.Properties.ContainsKey("node.key"))
                throw new Exception("'node.key' property should exists");
            if ((string)app.Properties["node.key"] != "value")
                throw new Exception("'node.key' property should be equal to 'value'");
            // function
            if (!app.Properties.ContainsKey("node.func"))
                throw new Exception("'node.func' function should exists");
            Func<object, Task<object>> appBuilderJsFunc = (Func<object, Task<object>>)app.Properties["node.func"];
            var appBuilderJsFuncResult = (string)await appBuilderJsFunc("Bruno");
            if (appBuilderJsFuncResult != "Hello Bruno")
                throw new Exception("'node.func' function call should return 'Hello Bruno'");

            app.Use(new Func<AppFunc, AppFunc>(next => async env =>
            {
                // value
                if (!env.ContainsKey("node.key"))
                    throw new Exception("'node.key' property should exists");
                if ((string)env["node.key"] != "value")
                    throw new Exception("'node.key' property should be equal to 'value'");
                // function
                if (!env.ContainsKey("node.func"))
                    throw new Exception("'node.func' function should exists");
                Func<object, Task<object>> appJsFunc = (Func<object, Task<object>>)env["node.func"];
                var appResult = (string)await appJsFunc("Bruno");
                if (appResult != "Hello Bruno")
                    throw new Exception("'node.func' function call should return 'Hello Bruno'");

                env["owin.ResponseStatusCode"] = 200;
            }));
        }

        public static async Task ShouldExportRequestSpecificNodeDataAndFunctions(IDictionary<string, object> env)
        {
            // value
            if (!env.ContainsKey("node.key"))
                throw new Exception("'node.key' property should exists");
            if ((string)env["node.key"] != "value")
                throw new Exception("'node.key' property should be equal to 'value'");
            // function
            if (!env.ContainsKey("node.func"))
                throw new Exception("'node.func' function should exists");
            Func<object, Task<object>> jsFunc = (Func<object, Task<object>>)env["node.func"];
            var result = (string)await jsFunc("Bruno");
            if (result != "Hello Bruno")
                throw new Exception("'node.func' function call should return 'Hello Bruno'");
            env["owin.ResponseStatusCode"] = 200;
        }

        public static Task ShouldWorkWithConnectBodyParser(IDictionary<string, object> env)
        {
            if (!(env["owin.RequestBody"] is Stream))
                throw new Exception("'owin.RequestBody' should be a Stream");
            StreamReader r = new StreamReader((Stream)env["owin.RequestBody"]);
            if (r.ReadToEnd() != "{\"msg\":\"Hello OWIN!\"}")
                throw new Exception("Request body should be equal to 'Hello OWIN!'");
            env["owin.ResponseStatusCode"] = 200;

            return Task.FromResult<object>(null);
        }

        // #4
        public static async Task ShouldGetBigResponseBody(IDictionary<string, object> env)
        {
            var fileInfo = new FileInfo("test/dummy.txt");
            if (fileInfo.Exists)
            {
                env["owin.ResponseStatusCode"] = 200;
                ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                    "Content-Length", new string[] { fileInfo.Length.ToString() });
                using (var file = fileInfo.OpenRead())
                {
                    await file.CopyToAsync((Stream)env["owin.ResponseBody"]);
                }
            }
            else
            {
                env["owin.ResponseStatusCode"] = 404;
            }
        }
    }
}
