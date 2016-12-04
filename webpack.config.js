module.exports = {
    entry: {
        // ...
    },
    output: {
        // ...
    },
    devServer: {
        setup: function (app) {
            // configure 'connect-owin' middleware to webpack dev server
            // don't forget to replace require('./') by require('connect-owin') in your project!
            app.get('/net', require('./')(__dirname + '/examples/hello/Connect.Owin.Examples.Hello.dll'));
        }
    }
};
