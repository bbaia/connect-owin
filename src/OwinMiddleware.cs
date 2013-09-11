using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Owin;
using Owin.Builder;

namespace Connect.Owin
{
    // OWIN application delegate
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class OwinMiddleware
    {
        static List<AppFunc> owinMiddlewares = new List<AppFunc>();

        public async Task<object> Initialize(IDictionary<string, object> input)
        {
            /*
             * This method performs initialization of the OWIN middleware given the configuration parameters in `input`. 
             * The return value is an integer representing a unique identifier of the middleware instance. 
             * That unique identifier will be later provided to the Handle method to allow dispatching the actual HTTP request
             * processing to the appropriate OWIN middleware instance. 
             * 
             * The `input` parameter is an IDictionary<string,object> representing the application configuration parameters 
             * specified when calling the proxy to this function from node.js. 
             * At minimum the dictionary will contain the `assemblyFile` property that contains 
             * the file name of the assembly with the OWIN middleware. 
             * The dictionary may also contain any other properties the node.js developer has chosen to pass to Initialize. 
             * It may be useful to pass-though these options to initialization code of the OWIN application. 
             * 
             * This method will normalize to OWIN application delegate 'Func<IDictionary<string,object>, Task>'
             * 
             * This method does not need to be thread safe as it is always called from the singleton V8 thread 
             * of the node.js application.
             */

            // Load assembly
            var assemblyFile = GetValueOrDefault<string>(input, "assemblyFile", null);
            Assembly assembly = Assembly.LoadFrom(assemblyFile);

            // Determine type
            string typeName = GetValueOrDefault<string>(input, "typeName", assembly.GetName().Name + ".Startup");
            Type type = assembly.GetType(typeName, true, true);

            // Determine method
            string methodName = GetValueOrDefault<string>(input, "methodName", "Configuration");
            MethodInfo method = type.GetMethod(methodName);
            if (method == null)
            {
                throw new InvalidOperationException(String.Format("The method '{0}' could not be found on type '{1}'", methodName, typeName));
            }

            // Create standard IAppBuilder implementation
            IAppBuilder appBuilder = new AppBuilder();

            // The builder requires a default middleware: override the default one that sets status code to 404-NotFound
            appBuilder.Properties.Add("builder.DefaultApp", new AppFunc((env) =>
            {
                // indicates to connect-owin to continue on connect pipeline
                env["connect-owin.continue"] = true;

                return Task.FromResult<object>(null);
            }));

            // Add `input` parameters from node.js to builder properties
            foreach (KeyValuePair<string, object> item in input)
            {
                appBuilder.Properties.Add("node." + item.Key, item.Value);
            }

            // Create Owin middleware
            object instance = method.IsStatic ? null : Activator.CreateInstance(type);
            // Normalize to Func<IDictionary<string, object>, Task> using IAppBuilder pipeline
            if (Matches(method, typeof(void), typeof(IAppBuilder)))
            {
                method.Invoke(instance, new[] { appBuilder });
            }
            else if (Matches(method, typeof(Task), typeof(IAppBuilder)))
            {
                await (Task)method.Invoke(instance, new[] { appBuilder });
            }
            // Normalize to Func<IDictionary<string, object>, Task> using OWIN calling convention
            else if (Matches(method, typeof(Task), typeof(IDictionary<string, object>)))
            {
                appBuilder.Use(new Func<AppFunc, AppFunc>(next => env => (Task)method.Invoke(instance, new object[] { env })));
            }
            else
            {
                throw new InvalidOperationException(String.Format(
                    "The method '{0}.{1}' could not be matched to 'void (IAppBuilder)' or 'Task (IDictionary<string,object>)'",
                    typeName, methodName));
            }
            owinMiddlewares.Add((AppFunc)appBuilder.Build(typeof(AppFunc)));

            // Return middleware identifier to node.js. 
            // The identifier is the index into the owinMiddlewares list.
            // Note: no sychronization required since we are running on singleton node.js thread. 
            return (owinMiddlewares.Count - 1);
        }

        public async Task<object> Handle(IDictionary<string, object> env)
        {
            /*
             * This method invokes the actual OWIN middleware to process the HTTP request. 
             * 
             * The `env` parameter contains the following keys:
             * - owin.RequestMethod
             * - owin.RequestPath
             * - owin.RequestPathBase
             * - owin.RequestProtocol
             * - owin.RequestQueryString
             * - owin.RequestScheme
             * - owin.RequestHeaders
             * - owin.RequestBody
             * - connect-owin.appId
             * - node.*
             * 
             * Most of the owin.* properties are already in the format required by the OWIN spec. 
             * These are exceptions that require preprocessing before passing to the actual OWIN middleware:
             * 
             * The `owin.RequestBody` is a byte[]. It must be wrapped in an instance of MemoryStream.
             * The `owin.RequestHeaders` is a IDictionary<string, object>. It must be converted to IDictionary<string, string[]>.
             * The `owin.ResponseBody` is missing. A new MemoryStream must be created to represent it. 
             * The `owin.ResponseHeaders` is missing. A new entry must be created to represent it. 
             * The `owin.CallCancelled` is missing. A new entry must be created to represent it. 
             * The `owin.Version` is missing. A new entry must be created to represent it. 
             * 
             * According to RFC 2616, headers field names are case-insensitive. 
             * IDictionary<string, string[]> instances for request/response headers must be case-insensitive.
             * 
             * The connect-owin.appId is the identifier returned from the Configure method that should be used to dispatch
             * the request to appropriate middleware.
             * 
             * The node.* is an arbitrary set of properties specified by node.js developer either at the time of initialization, or
             * per-request via connect middleware running before the owin middleware. Typically they will contain proxies to 
             * node.js functions exported to .NET in the form of Func<object,Task<object>>. 
             * 
             * This method must pre-process the OWIN environment, invoke the OWIN middleware, post-process the resulting OWIN environment,
             * and return it as the object result of this function. 
             * 
             * Postprocessing of the OWIN environment after invoking the OWIN application must:
             * - remove all node.* entries from the dictionary. This is required because we cannot marshal Func<object,Task<object>> back to node.js at this time.
             * - remove owin.Request* properties
             * - convert owin.ResponseBody to a byte[] and store it back in owin.ResponseBody
             * 
             * Future: non-byte[] content types and full streaming support (basically we will be able to marshal node.js Stream as a .NET Stream from node.js)
             */

            // Extract appId
            int owinAppId = GetValueOrDefault<int>(env, "connect-owin.appId", -1);

            // Convert request headers to IDictionary<string, string[]>
            IDictionary<string, string[]> requestHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, object> nodeRequestHeaders = (IDictionary<string, object>)env["owin.RequestHeaders"];
            foreach (KeyValuePair<string, object> header in nodeRequestHeaders)
            {
                if (header.Value is object[])
                {
                    requestHeaders.Add(header.Key, Array.ConvertAll<object, string>((object[])header.Value, o => o.ToString()));
                }
                else
                {
                    requestHeaders.Add(header.Key, new string[] { header.Value.ToString() });
                }
            }
            env["owin.RequestHeaders"] = requestHeaders;

            // Create memory stream around request body
            byte[] body = GetValueOrDefault<byte[]>(env, "owin.RequestBody", new byte[0]);
            env["owin.RequestBody"] = body.Length > 0 ? new MemoryStream(body) : Stream.Null;

            // Create response OWIN properties for the application to write to
            MemoryStream responseBody = new MemoryStream();
            env["owin.ResponseBody"] = responseBody;
            Dictionary<string, string[]> responseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            env["owin.ResponseHeaders"] = responseHeaders;

            // Other data
            env["owin.Version"] = "1.0";
            // TODO: request cancel/abort from the server
            CancellationTokenSource cts = new CancellationTokenSource();
            env["owin.CallCancelled"] = cts.Token;

            // Run the OWIN app
            return await owinMiddlewares[owinAppId](env).ContinueWith<object>((task) =>
            {
                if (task.IsFaulted)
                {
                    throw task.Exception;
                }

                if (task.IsCanceled)
                {
                    throw new InvalidOperationException("The OWIN application has cancelled processing of the request.");
                }

                // Remove all non-response entries from env
                List<string> keys = new List<string>(env.Keys);
                foreach (string key in keys)
                {
                    if (!key.StartsWith("owin.Response") &&
                        !key.StartsWith("connect-owin."))
                    {
                        env.Remove(key);
                    }
                }

                // Serialize response body to a byte[]
                byte[] content = responseBody.ToArray();
                if (content.Length > 0)
                {
                    env["owin.ResponseBody"] = content;
                }
                else
                {
                    env.Remove("owin.ResponseBody");
                }

                if (env.ContainsKey("owin.ResponseStatusCode"))
                {
                    // Fix use of HttpStatusCode enum for 'owin.ResponseStatusCode'
                    env["owin.ResponseStatusCode"] = (int)env["owin.ResponseStatusCode"];
                }

                // Return the post-processed env back to node.js
                return env;
            });
        }

        static T GetValueOrDefault<T>(IDictionary<string, object> parameters, string parameter, T defaultValue)
        {
            object value;
            if (parameters.TryGetValue(parameter, out value))
            {
                return (T)value;
            }
            else
            {
                return defaultValue;
            }
        }

        static bool Matches(MethodInfo methodInfo, Type returnType, params Type[] parameterTypes)
        {
            if (returnType != null && methodInfo.ReturnType != returnType)
            {
                return false;
            }

            var parameters = methodInfo.GetParameters();
            if (parameters.Length != parameterTypes.Length)
            {
                return false;
            }

            return parameters
                .Zip(parameterTypes, (pi, t) => pi.ParameterType == t)
                .All(b => b);
        }
    }
}
