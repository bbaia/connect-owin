var owin = require('../..'),
    express = require('express');

var app = express();
app.use(require('connect-logger')());
app.all('/net', owin('Connect.Owin.Examples.Hello.dll'));
app.all('/node', function (req, res) {
    res.status(200).send('Hello from JavaScript! Time on server ' + new Date());
});
app.listen(3000);
