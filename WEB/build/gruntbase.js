/********************************************************************************
*  gruntbase.js                                                                 *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(module, require) {

module.exports = ({task, registerTask, initConfig, file, option}, dir) => {
    const
        pkg    = file.readJSON('./package.json'),
        target = option('target');

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
            tmp: ['<%= project.dirs.tmp %>']
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
                sourceMap: true,
                sourceType: 'module',
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
                outfile: '<%= project.dirs.tmp %>/_SpecRunner.html',
                junit: {
                    path: '<%= project.dirs.artifacts %>'
                }
            }
        }
    }));

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'init',
        'clean:tmp',
        'eslint:app',
        'eslint:tests',
        'babel:tests',
        'jasmine'
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
};
})(module, require);