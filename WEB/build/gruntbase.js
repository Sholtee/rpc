/********************************************************************************
*  gruntbase.js                                                                 *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(module, require) {

module.exports = ({task, registerTask, initConfig, file, template, option}, dir) => {
    const
        pkg    = file.readJSON('./package.json'),
        target = option('target'),
        path   = require('path');

    registerTask('init', () => initConfig({
        project: {
            name:  pkg.name.toLowerCase(),
            dirs: {
                app:       `${dir}/src`,
                artifacts: `${dir}/artifacts`,
                dist:      `${dir}/dist/${pkg.version}`,
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
                files: {
                    '<%= project.dirs.dist %>/<%= project.name %>.min.js': '<%= project.dirs.dist %>/<%= project.name %>.js'
                }
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
                src: [
                    '<%= project.dirs.tests %>/**/*.spec.js'
                ]
            }
        },
        babel: {
            options: {
                //sourceMap: true,
                sourceType: 'script',
                presets: ['@babel/preset-env']
            },
            tests: {
                files: {
                    '<%= project.dirs.tmp %>/specs.js': `<%= project.dirs.tests %>/${target || "**/*.spec.js"}`,
                    '<%= project.dirs.tmp %>/app.js': '<%= project.dirs.app %>/**/*.js'
                }
            },
            dist: {
                files: {
                    '<%= project.dirs.dist %>/<%= project.name %>.js': '<%= project.dirs.app %>/**/*.js'
                }
            }
        },
        jasmine: {
            src: '<%= project.dirs.tmp %>/app.js',
            options: {
                specs: '<%= project.dirs.tmp %>/specs.js',
                outfile: '.tmp/_SpecRunner.html', // nem lehet abszolut utvonal -> "<%= project.dirs.tmp %>/_SpecRunner.html" kilove
                junit: {
                   path: '<%= project.dirs.artifacts %>',
                    consolidate: true
                }
            }
        },
        http_upload: {
            testresults: {
                options: {
                    url: `https://ci.appveyor.com/api/testresults/junit/${process.env.APPVEYOR_JOB_ID}`,
                    method: 'PUT'
                },
                filter: '<%= project.dirs.artifacts %>/*.xml', // hack h hasznalhassunk sablont a getTestResults() hivasakor
                get files() {
                    return getTestResults(this.filter);
                }
            },
        }
    }));

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'init',
        'clean:tmp',
        'clean:artifacts',
        'eslint:app',
        'eslint:tests',
        'babel:tests',
        'jasmine'
    ]));

    registerTask('pushresults', () => task.run([ // grunt pushresults
        'init',
        'http_upload:testresults'
    ]));

    registerTask('build', () => task.run([ // grunt build
        'init',
        'clean:dist',
        'eslint:app',
        'babel:dist',
        'uglify:dist'
    ]));

    registerTask('lint', () => task.run([ // grunt lint --target=[tests|app]
        'init',
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