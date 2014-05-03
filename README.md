# [connect-owin](https://github.com/bbaia/connect-owin/) 

Implement node.js [connect middleware](http://www.senchalabs.org/connect/) in .NET using [OWIN](http://owin.org/).

Versions are incremented according to [semver](http://semver.org/).

This is a fork of Tomasz Janczuk's [original code](https://github.com/bbaia/edge-connect/);
thanks go to him for getting this thing started!

## Introduction

OWIN itself is not a technology, just a specification to decouple Web applications from the Web server. 
The goal of `connect-owin` is to implement this specification to use `node.js`, through `connect` framework, as the Web Server.

The `connect-owin` exports a function that returns a connect middleware. 
The function takes the path of the OWIN .NET assembly file as a parameter.
The following code shows how to use `connect-owin` with [express.js](http://expressjs.com/), 
a Web framework built on connect:

```js
var owin = require('connect-owin'),
    express = require('express');

var app = express();
app.all('/net', owin('MyAssembly.dll'));
// ...
app.listen(3000);
```

.NET OWIN middlewares can be implemented in two ways with `connect-owin`:

* By implementing the OWIN primary interface `Func<IDictionary<string, object>, Task>`:

```csharp
public class Startup
{
  public Task Invoke(IDictionary<string, object> env) 
  {
    // ...
  }
}
```

* By using the `IAppBuilder` interface that acts as the glue for any .NET OWIN middleware, exactly how connect in node.js works:

```csharp
public class Startup
{
  public void Configuration(IAppBuilder builder)
  {
    // ...
  }
}
```

The `connect-owin` function uses `<assembly name>.Startup` as default type name, and `Configuration` as default method name.
Custom type and method name can be provided via an options object:

```js
owin({
    assemblyFile: 'MyAssembly.dll',
    typeName: 'MyNamespace.MyType',
    methodName: 'MyMethodName'
});
```

## Requirements

* Windows, Linux or MacOS
* [node.js](http://nodejs.org/) 0.8.x or later
* [.NET Framework 4.5](http://www.microsoft.com/en-us/download/details.aspx?id=30653) or [Mono 3.4](http://www.mono-project.com/)

## Building

[Grunt](http://gruntjs.com/) is used to build, test and preview the sample on all platforms.

First, install `connect-owin` dependencies:

	$ npm install

Then, you'll need to install Grunt's command line interface (CLI) globally:

	$ npm install -g grunt-cli

You can build sources, run tests and preview the sample by using the default Grunt task:

	$ grunt

### Building sources

	$ grunt build

The build creates the `lib\clr\Connect.Owin.dll` file required by the `lib\connect-owin.js` library.

### Running the sample

_Using Grunt_

The following command uses the `grunt-contrib-connect` task to start a `connect` web server 
with the .NET OWIN application plugged in as a middleware and open the page in your default browser:

	$ grunt hello

_Using express.js_

An [express.js](http://expressjs.com/) sample is also provided to run the .NET OWIN application:

	$ cd examples\hello
	$ npm install express
	$ node server.js

Then go to http://localhost:3000/node. This should display a message from an express middleware in node.js. 

If you go to http://localhost:3000/net, you should see a similar message from the .NET OWIN application 
in `Owin.Connect.Examples.Hello.dll` plugged in as a middleware to the express pipeline.

_More samples available @ [connect-owin-samples](https://github.com/bbaia/connect-owin-samples/)_

### Running tests

	$ grunt test

`mocha` is used to run tests.