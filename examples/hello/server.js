var owin = require('../..'),
    express = require('express');

var app = express();
app.all('/net', owin('Connect.Owin.Examples.Hello.dll'));
app.all('/node', function (req, res) {
    res.send(200, 'Hello from JavaScript! Time on server ' + new Date());
});
app.listen(3000);
