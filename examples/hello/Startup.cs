using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using Owin;

namespace Connect.Owin.Examples.Hello
{
    // OWIN application delegate
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Use(new Func<AppFunc, AppFunc>(next => async env =>
                {
                    env["owin.ResponseStatusCode"] = 200;
                    ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                        "Content-Length", new string[] { "53" });
                    ((IDictionary<string, string[]>)env["owin.ResponseHeaders"]).Add(
                        "Content-Type", new string[] { "text/html" });
                    StreamWriter w = new StreamWriter((Stream)env["owin.ResponseBody"]);
                    w.Write("Hello, from C#. Time on server is " + DateTime.Now.ToString());
                    await w.FlushAsync();
                }));
        }
    }
}
