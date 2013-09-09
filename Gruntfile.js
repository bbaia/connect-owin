module.exports = function (grunt) {
    'use strict';

    // Load all grunt tasks
    require('matchdep').filterDev('grunt-*').forEach(grunt.loadNpmTasks);

    // Project configuration
    grunt.initConfig({

        // Task configuration

        msbuild: {
            options: {
                projectConfiguration: 'Release',
                targets: ['Rebuild'],
                stdout: true,
                buildParameters: {
                    WarningLevel: 2
                },
                verbosity: 'quiet',
                version: 4.0
            },
            src: {
                src: ['src/Connect.Owin.csproj']
            },
            test: {
                src: ['test/Connect.Owin.Tests.csproj']
            },
            hello: {
                src: ['examples/hello/Connect.Owin.Examples.Hello.csproj']
            }
        },

        jshint: {
            options: {
                node: true,
                bitwise: true,
                camelcase: true,
                eqeqeq: true,
                immed: true,
                latedef: true,
                newcap: true,
                noarg: true,
                quotmark: 'single',
                undef: true,
                unused: true,
                strict: true,
                trailing: true,
                indent: 4
            },
            src: [ 'lib/*.js' ]
        },

        mochaTest: {
            test: {
                options: {
                    reporter: 'spec',
                    require: 'should'
                },
                src: ['test/*.js']
            }
        },

        connect: {
            hello: {
                options: {
                    hostname: 'localhost',
                    port: 9000,
                    open: true,
                    keepalive: true,
                    middleware: function (connect, options) {
                        return [
                            require('./')(__dirname + '\\examples\\hello\\Connect.Owin.Examples.Hello.dll')
                        ];
                    }
                }
            }
        }
    });

    // Tasks definition

    grunt.registerTask('build', [
        'msbuild:src',
        'jshint:src'
    ]);

    grunt.registerTask('test', [
        'msbuild:test',
        'mochaTest'
    ]);

    grunt.registerTask('hello', [
        'msbuild:hello',
        'connect:hello'
    ]);

    grunt.registerTask('default', ['build', 'test', 'hello']);
};