/********************************************************************************
*  gruntbase.js                                                                 *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(module) {

module.exports = ({task, registerTask, initConfig, file, option}, dir) => {
    const
        pkg    = file.readJSON('./package.json'),
        target = option('target');

    initConfig({
        project: {
            module:  pkg.module,
            version: pkg.version,
            dirs: {
                app:          `${dir}/src`,
                artifacts:    `${dir}/../artifacts`,
                dist:         `${dir}/dist`,
                tests:        `${dir}/tests`,
                tmp:          `${dir}/.tmp`,
                node_modules: `${dir}/node_modules`
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
                failOnError: true,
                overrideConfig: {
                    parser: '@babel/eslint-parser',
                    parserOptions: {
                        requireConfigFile: false
                    }
                }
            },
            app: {
                options: {
                    overrideConfigFile: './build/eslint-build.json'
                },
                src: '<%= project.dirs.app %>/**/*.js'

            },
            tests: {
                options: {
                    overrideConfigFile: './build/eslint-tests.json'
                },
                src: '<%= project.dirs.tests %>/**/*.spec.js'
            }
        },
        babel: {
            __umd_transform: ['@babel/plugin-transform-modules-umd', {moduleId: '<%= project.module %>'}],
            __app_files: [{
                expand: true,
                cwd: '<%= project.dirs.app %>',
                src: ['**/*.js'],
                dest: '<%= project.dirs.tmp %>'
            }],
            options: {
                presets: ['@babel/preset-env'],
                targets: {
                    // fetch() API innentol van
                    chrome: '42',
                    firefox: '39',
                    edge: '14'
                },
                //sourceMap: true,
                //comments: false  // Ne hasznaljuk mert kiszedi a file header-t is
            },
            app: {
                options: {
                    sourceType: 'module',
                    plugins: [
                        'transform-async-to-promises',
                        '<%= babel.__umd_transform %>'
                    ]
                },
                files: '<%= babel.__app_files%>'
            },
            app_istanbul_instrumented: {
                options: {
                    sourceType: 'module',
                    plugins: [
                        'istanbul',
                        'transform-async-to-promises',
                        '<%= babel.__umd_transform %>'
                    ]
                },
                files: '<%= babel.__app_files %>'
            },
            tests: {
                options: {
                    sourceType: 'script',
                    targets: 'last 2 years' // tesztek biztos modern bongeszon futnak
                },
                files: [{
                    expand: true,
                    cwd: '<%= project.dirs.tests %>',
                    src: [target || "**/*.spec.js"],
                    dest: '<%= project.dirs.tmp %>'
                }]
            },
            dist: {
                options: {
                    sourceType: 'module',
                    plugins: [
                        'transform-async-to-promises',
                        '<%= babel.__umd_transform %>',
                        'remove-comments',
                        ['add-header-comment', {header: [`${pkg.name} v${pkg.version}`, 'Author: Denes Solti']}]
                    ]
                },
                files: {
                    '<%= project.dirs.dist %>/<%= project.module %>.js': '<%= project.dirs.app %>/**/*.js'
                }
            }
        },
        karma: {
            __frameworks: ['detectBrowsers', 'jasmine', 'sinon'],
            __files: [{
                src: ['<%= project.dirs.node_modules %>/whatwg-fetch/dist/fetch.umd.js', '<%= project.dirs.tmp %>/**/*.js'],
                included: true,
                served: true
            }],
            __postDetection: availableBrowsers => availableBrowsers.filter(browser => browser.indexOf('IE') < 0),
            tests_using_reporters: {
                basePath: '',
                frameworks: '<%= karma.__frameworks %>',
                files: '<%= karma.__files %>',
                exclude: [],
                reporters: ['junit', 'coverage-istanbul'],
                port: 1986,
                singleRun: true,
                logLevel: 'ERROR',
                plugins: [
                    'karma-detect-browsers',
                    'karma-chrome-launcher',
                    'karma-firefox-launcher',
                    'karma-jasmine',
                    'karma-sinon',
                    'karma-junit-reporter',
                    'karma-coverage-istanbul-reporter'
                ],
                junitReporter: {
                    outputDir: '<%= project.dirs.artifacts %>/junit'
                },
                coverageIstanbulReporter: {
                    reports: ['lcov'],
                    dir: '<%= project.dirs.artifacts %>',
                    skipFilesWithNoCoverage: true
                },
                detectBrowsers: {
                    enabled: true,
                    usePhantomJS: false,
                    preferHeadless: true,
                    postDetection: '<%= karma.__postDetection %>'
                }
            },
            tests: {
                basePath: '',
                frameworks: '<%= karma.__frameworks %>',
                files: '<%= karma.__files %>',
                exclude: [],
                reporters: [],
                port: 1986,
                singleRun: false,
                logLevel: 'ERROR',
                plugins: [
                    'karma-detect-browsers',
                    'karma-chrome-launcher',
                    'karma-firefox-launcher',
                    'karma-jasmine',
                    'karma-sinon'
                ],
                detectBrowsers: {
                    enabled: true,
                    usePhantomJS: false,
                    preferHeadless: false,
                    postDetection: '<%= karma.__postDetection %>'
                }
            }
        },
        replace: { // coveralls.io a repo gyokerebol keres
            lcov: {
                options: {
                    patterns: [{
                        match: /^SF:([\w\\/.]+)$/gm,
                        replacement: (m, path) => `SF:\\WEB\\${path}`
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
                cmd: file.expand(`${dir}/../BIN/**/Solti.Utils.Rpc.Server.Sample.exe`)[0],
                args: [],
                options: {
                    wait: false,
                    ready: /Service started/g
                }
            }
        }
    });

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'clean:tmp',
        'eslint:app',
        'eslint:tests',
        'babel:app_istanbul_instrumented',
        'babel:tests',
        'run:server', // a szulo process terminalasaval o is eltavozik
        'karma:tests_using_reporters',
        'replace:lcov'
    ]));

    registerTask('test_debug', () => task.run([ // grunt test_debug [--target=xXx.spec.js]
        'clean:tmp',
        'eslint:app',
        'eslint:tests',
        'babel:app',
        'babel:tests',
        'run:server',
        'karma:tests'
    ]));

    registerTask('build', () => task.run([ // grunt build
        'clean:dist',
        'eslint:app',
        'babel:dist',
        'uglify:dist'
    ]));

    registerTask('lint', () => task.run([ // grunt lint --target=[tests|app]
        `eslint:${target}`
    ]));
};
})(module, require);
