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

        public async Task<object> Handle(IDictionary<string, object> input)
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
             * - connect-owin.*Func
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
             * The connect-owin.appId is the identifier returned from the Initialize method that should be used to dispatch
             * the request to appropriate middleware.
             * 
             * The connect-owin.*Func properties are node.js functions that configure the response.
             * 
             * The node.* is an arbitrary set of properties specified by node.js developer either at the time of initialization, or
             * per-request via connect middleware running before the owin middleware. Typically they will contain proxies to 
             * node.js functions exported to .NET in the form of Func<object,Task<object>>. 
             * 
             * This method must pre-process the OWIN environment, invoke the OWIN middleware, post-process the resulting OWIN environment,
             * and return a boolean indicating whether the connect pipeline continue running.
             * 
             * Future: non-byte[] content types and full streaming support (basically we will be able to marshal node.js Stream as a .NET Stream from node.js)
             */

            // Async tasks to complete before returning back to node.js
            IList<Task> asyncTasks = new List<Task>();

            // Convert request headers to IDictionary<string, string[]>
            IDictionary<string, string[]> requestHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, object> nodeRequestHeaders = (IDictionary<string, object>)input["owin.RequestHeaders"];
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
            input["owin.RequestHeaders"] = requestHeaders;

            // Create memory stream around request body
            byte[] body = GetValueOrDefault<byte[]>(input, "owin.RequestBody", new byte[0]);
            input["owin.RequestBody"] = body.Length > 0 ? new MemoryStream(body) : Stream.Null;

            // Create response body stream for the application to write to
            Func<object, Task<object>> writeFunc = GetValueOrDefault<Func<object, Task<object>>>(input, "connect-owin.writeFunc", null);
            input["owin.ResponseBody"] = new OwinMiddlewareResponseStream(asyncTasks, 
                async (buffer, offset, count) =>
                {
                    // On write
                    if (count > 0)
                    {
                        if (buffer.Length != count)
                        {
                            // Trim buffer
                            byte[] data = new byte[count];
                            Array.Copy(buffer, data, count);
                            buffer = data;
                        }
                        await Task.WhenAll(asyncTasks.ToArray());
                        asyncTasks.Clear();
                        Task writeTask = writeFunc(buffer);
                        asyncTasks.Add(writeTask);
                        await writeTask;
                    }
                });
            
            // Create response headers for the application to write to
            Func<object, Task<object>> setHeaderFunc = GetValueOrDefault<Func<object, Task<object>>>(input, "connect-owin.setHeaderFunc", null);
            Func<object, Task<object>> removeHeaderFunc = GetValueOrDefault<Func<object, Task<object>>>(input, "connect-owin.removeHeaderFunc", null);
            Func<object, Task<object>> removeAllHeadersFunc = GetValueOrDefault<Func<object, Task<object>>>(input, "connect-owin.removeAllHeadersFunc", null);
            input["owin.ResponseHeaders"] = new OwinMiddlewareDictionary<string, string[]>(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                (key, value) =>
                {
                    // On set header
                    IDictionary<string, string[]> header = new Dictionary<string, string[]>(1);
                    header.Add(key, value);
                    asyncTasks.Add(setHeaderFunc(header));
                },
                (key) =>
                {
                    // On remove header
                    asyncTasks.Add(removeHeaderFunc(key));
                },
                () =>
                {
                    // On clear headers
                    asyncTasks.Add(removeAllHeadersFunc(null));
                });

            // Other data
            input["owin.Version"] = "1.0";
            // TODO: request cancel/abort from the server
            CancellationTokenSource cts = new CancellationTokenSource();
            input["owin.CallCancelled"] = cts.Token;

            // Create OWIN environment dictionary
            Func<object, Task<object>> setStatusCodeFunc = GetValueOrDefault<Func<object, Task<object>>>(input, "connect-owin.setStatusCodeFunc", null);
            IDictionary<string, object> env = new OwinMiddlewareDictionary<string, object>(input,
                (key, value) =>
                {
                    // On set OWIN variable
                    switch (key)
                    {
                        case "owin.ResponseStatusCode":
                            // Set response status code
                            asyncTasks.Add(setStatusCodeFunc((int)value));
                            break;
                        case "owin.ResponseBody":
                            throw new InvalidOperationException("Cannot set 'owin.ResponseBody'. Use provided stream instead.");
                        case "owin.ResponseHeaders":
                            throw new InvalidOperationException("Cannot set 'owin.ResponseHeaders'. Use provided dictionary instead.");
                    }
                },
                (key) =>
                {
                    // On remove OWIN variable
                    if (key.StartsWith("owin."))
                    {
                        throw new InvalidOperationException("Cannot remove OWIN environment data.");
                    }
                },
                () =>
                {
                    // On clear OWIN variables
                    throw new InvalidOperationException("Cannot clear OWIN environment dictionary.");
                });

            // Run the OWIN app
            int owinAppId = GetValueOrDefault<int>(input, "connect-owin.appId", -1);
            await owinMiddlewares[owinAppId](env);
            await Task.WhenAll(asyncTasks.ToArray());
            return GetValueOrDefault<bool>(env, "connect-owin.continue", false);
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

        class OwinMiddlewareDictionary<K, V> : IDictionary<K, V>
        {
            private IDictionary<K, V> innerDictionary;
            private Action<K, V> onSetAction;
            private Action<K> onRemoveAction;
            private Action onClearAction;

            public OwinMiddlewareDictionary(
                IDictionary<K, V> innerDictionary,
                Action<K, V> onSetAction,
                Action<K> onRemoveAction,
                Action onClearAction)
            {
                this.innerDictionary = innerDictionary;

                this.onSetAction = onSetAction;
                this.onRemoveAction = onRemoveAction;
                this.onClearAction = onClearAction;
            }

            public void Add(K key, V value)
            {
                this.innerDictionary.Add(key, value);

                // Set
                if (this.onSetAction != null) this.onSetAction(key, value);
            }

            public bool ContainsKey(K key)
            {
                return this.innerDictionary.ContainsKey(key);
            }

            public ICollection<K> Keys
            {
                get { return this.innerDictionary.Keys; }
            }

            public bool Remove(K key)
            {
                bool result = this.innerDictionary.Remove(key);

                // Remove
                if (this.onRemoveAction != null) this.onRemoveAction(key);

                return result;
            }

            public bool TryGetValue(K key, out V value)
            {
                return this.innerDictionary.TryGetValue(key, out value);
            }

            public ICollection<V> Values
            {
                get { return this.innerDictionary.Values; }
            }

            public V this[K key]
            {
                get { return this.innerDictionary[key]; }
                set
                {
                    this.innerDictionary[key] = value;

                    // Set
                    if (this.onSetAction != null) this.onSetAction(key, value);
                }
            }

            public void Add(KeyValuePair<K, V> item)
            {
                this.innerDictionary.Add(item);

                // Set
                if (this.onSetAction != null) this.onSetAction(item.Key, item.Value);
            }

            public void Clear()
            {
                this.innerDictionary.Clear();

                // Clear
                if (this.onClearAction != null) this.onClearAction();
            }

            public bool Contains(KeyValuePair<K, V> item)
            {
                return this.innerDictionary.Contains(item);
            }

            public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
            {
                this.innerDictionary.CopyTo(array, arrayIndex);
            }

            public int Count
            {
                get { return this.innerDictionary.Count; }
            }

            public bool IsReadOnly
            {
                get { return this.innerDictionary.IsReadOnly; }
            }

            public bool Remove(KeyValuePair<K, V> item)
            {
                bool result = this.innerDictionary.Remove(item);

                // Remove
                if (this.onRemoveAction != null) this.onRemoveAction(item.Key);

                return result;
            }

            public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
            {
                return this.innerDictionary.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return ((System.Collections.IEnumerable)this.innerDictionary).GetEnumerator();
            }
        }

        class OwinMiddlewareResponseStream : Stream
        {
            private IList<Task> asyncTasks;
            private Func<byte[], int, int, Task> onWriteAsync;

            public OwinMiddlewareResponseStream(IList<Task> asyncTasks, Func<byte[], int, int, Task> onWriteAsync)
            {
                if (asyncTasks == null)
                    throw new ArgumentNullException("asyncTasks");
                if (onWriteAsync == null)
                    throw new ArgumentNullException("onWriteActionAsync");

                this.asyncTasks = asyncTasks;
                this.onWriteAsync = onWriteAsync;
            }

            public override bool CanRead
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override long Position
            {
                get { throw new NotSupportedException("Seeking is not supported on this stream."); }
                set { throw new NotSupportedException("Seeking is not supported on this stream."); }
            }

            public override long Length
            {
                get { throw new NotSupportedException("Seeking is not supported on this stream."); }
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException("Seeking is not supported on this stream.");
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("Reading is not supported on this stream.");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.asyncTasks.Add(this.WriteAsync(buffer, offset, count, CancellationToken.None));
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return this.onWriteAsync(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("Seeking is not supported on this stream.");
            }

            public override void Flush()
            {
            }
        }
    }
}
