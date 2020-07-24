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
            version: pkg.version,
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
            app: {
                options: {
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
                    '<%= project.dirs.dist %>/<%= project.name %>-<%= project.version %>.js': '<%= project.dirs.app %>/**/*.js'
                }
            }
        },
        jasmine: {
            src: ['<%= project.dirs.tmp %>/**/*.js', '!<%= project.dirs.tmp %>/**/*.spec.js'],
            options: {
                specs: '<%= project.dirs.tmp %>/**/*.spec.js',
                vendor: './node_modules/sinon/pkg/sinon.js',
                outfile: '.tmp/_SpecRunner.html', // nem lehet abszolut utvonal -> "<%= project.dirs.tmp %>/_SpecRunner.html" kilove
                keepRunner: true,
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
        }
    }));

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'init',
        'clean:tmp',
        'clean:artifacts',
        'eslint:app',
        'eslint:tests',
        'babel:app',
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