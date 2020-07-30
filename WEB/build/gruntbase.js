/********************************************************************************
*  gruntbase.js                                                                 *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(module, require) {

module.exports = ({task, registerTask, initConfig, file, template, option}, dir) => {
    const
        pkg     = file.readJSON('./package.json'),
        target  = option('target'),
        path    = require('path'),
        process = require('process');

    initConfig({
        project: {
            name:  pkg.name.toLowerCase(),
            version: pkg.version,
            dirs: {
                app:       `${dir}/src`,
                artifacts: `${dir}/artifacts`,
                dist:      `${dir}/dist`,
                tests:     `${dir}/tests`,
                tmp:       `${dir}/.tmp`
            }
        },
        clean: {
            options: {
                force: true
            },
            dist: ['<%= project.dirs.dist %>'],
            tmp: ['<%= project.dirs.tmp %>'],
            artifacts: ['<%= project.dirs.artifacts %>']
        },
        uglify: {
            dist: {
                files: [{
                    expand: true,
                    cwd: '<%= project.dirs.dist %>',
                    src: ['*.js', '!*.min.js'],
                    dest: '<%= project.dirs.dist %>',
                    rename: (dst, src) => `${dst}/${src.replace('.js', '.min.js')}`
                }]
            }
        },
        eslint: {
            options: {
                outputFile: false,
                quiet: false,
                maxWarnings: -1,
                failOnError: true
            },
            app: {
                options: {
                    configFile: './build/eslint-build.json'
                },
                src: '<%= project.dirs.app %>/**/*.js'

            },
            tests: {
                options: {
                    configFile: './build/eslint-tests.json'
                },
                src: '<%= project.dirs.tests %>/**/*.spec.js'
            }
        },
        babel: {
            options: {
                //sourceMap: true,
                sourceType: 'script',
                presets: ['@babel/preset-env']
            },
            app: {
                options: {
                    plugins: ['istanbul']
                },
                files: [{
                    expand: true,
                    cwd: '<%= project.dirs.app %>',
                    src: ['**/*.js'],
                    dest: '<%= project.dirs.tmp %>'
                }]
            },
            tests: {
                files: [{
                    expand: true,
                    cwd: '<%= project.dirs.tests %>',
                    src: [target || "**/*.spec.js"],
                    dest: '<%= project.dirs.tmp %>'
                }]
            },
            dist: {
                files: {
                    '<%= project.dirs.dist %>/<%= project.name %>.js': '<%= project.dirs.app %>/**/*.js'
                }
            }
        },
        karma: {
            test: {
                basePath: '',
                frameworks: ['jasmine', 'sinon'],
                files: [
                    {src: ['<%= project.dirs.tmp %>/**/*.js'], served: true}
                ],
                exclude: [],
                reporters: ['junit', 'coverage-istanbul'],
                port: 1986,
                singleRun: true,
                browsers: ['Chrome_without_security'],
                customLaunchers: {
                    Chrome_without_security: {
                        base: 'Chrome',
                        flags: ['--disable-web-security']
                    }
                },
                logLevel: 'ERROR',
                plugins: [
                    'karma-chrome-launcher',
                    'karma-jasmine',
                    'karma-sinon',
                    'karma-junit-reporter',
                    'karma-coverage-istanbul-reporter'
                ],
                junitReporter: {
                    outputDir: '<%= project.dirs.artifacts %>'
                },
                coverageIstanbulReporter: {
                    reports: ['lcov'],
                    dir: '<%= project.dirs.artifacts %>',
                    skipFilesWithNoCoverage: true
                }
            }
        },
        http_upload: {
            testresults: {
                options: {
                    url: `https://ci.appveyor.com/api/testresults/junit/${process.env.APPVEYOR_JOB_ID}`,
                    method: 'POST',
                    headers: {
                        'Content-Type': 'text/xml'
                    }
                },
                filter: '<%= project.dirs.artifacts %>/*.xml',
                get files() {
                    // a "files" property nem tartalmazhat kifejteseket (pl.: *.xml) -> "filter" hack
                    return getTestResults(this.filter);
                }
            },
        },
        env: {
            coveralls: {
                COVERALLS_SERVICE_NAME: 'appveyor',
                COVERALLS_GIT_BRANCH: () => process.env.APPVEYOR_REPO_BRANCH,
                COVERALLS_SERVICE_JOB_ID: () => process.env.APPVEYOR_JOB_ID
            }
        },
        coveralls: {
            src: '<%= project.dirs.artifacts %>/lcov.info'
        },
        replace: { // coveralls.io a repo gyokerebol keres
            lcov: {
                options: {
                    patterns: [{
                        match: /^SF:([\w\\/.]+)$/gm,
                        replacement: (m, path) => `SF:WEB\\${path}`
                    }]
                },
                files: [{
                    expand: true,
                    src: '<%= project.dirs.artifacts %>/lcov.info',
                    dest: '.'
                }]
            }
        },
        run: {
            server: {
                cmd: 'dotnet',
                args: [
                    file.expand(`${dir}/../BIN/**/Solti.Utils.Rpc.Server.Sample.dll`)[0]
                ],
                options: {
                    wait: false
                }
            }
        }
    });

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'clean:tmp',
        'clean:artifacts',
        'eslint:app',
        'eslint:tests',
        'babel:app',
        'babel:tests',
        'run:server', // a szulo process terminalasaval o is eltavozik
        'karma:test'
    ]));

    registerTask('pushresults', () => task.run([ // grunt pushresults
        'http_upload:testresults'
    ]));

    registerTask('pushcoverage', () => {  // grunt pushcoverage
        process.chdir('../'); // kell h a "coveralls" task megfeleloen mukodjon

        task.run([
            'env:coveralls',
            'replace:lcov',
            //'coveralls'
        ]);
    });

    registerTask('build', () => task.run([ // grunt build
        'clean:dist',
        'eslint:app',
        'babel:dist',
        'uglify:dist'
    ]));

    registerTask('lint', () => task.run([ // grunt lint --target=[tests|app]
        `eslint:${target}`
    ]));

    function getTestResults(filter) {
        filter = template.process(filter);

        return file.expand(filter).map(file => ({
            src: file,
            dest: path.basename(file)
        }));
    }
};
})(module, require);