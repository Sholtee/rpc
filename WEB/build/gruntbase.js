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
                app:   `${dir}/src`,
                tests: `${dir}/tests`,
                dist:  `${dir}/dist/${pkg.version}`
            }
        },
        clean: {
            options: {
                force: true
            },
            dist: ['<%= project.dirs.dist %>']
        },
        uglify: {
            dist: {
                files: {
                    '<%= project.dirs.dist %>/<%= project.name %>.min.js': '<%= project.dirs.app %>/**/*.js'
                }
            }
        },
        concat: {
            options: {
                separator: ';',
            },
            dist: {
                src: '<%= project.dirs.app %>/**/*.js',
                dest: '<%= project.dirs.dist %>/<%= project.name %>.js',
            },
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
        jasmine: {
            run: {
                src: '<%= project.dirs.app %>/**/*.js',
                options: {
                    specs: target || '<%= project.dirs.tests %>/**/*.spec.js'
                }
            }
        }
    }));

    registerTask('test', () => task.run([ // grunt test [--target=xXx.spec.js]
        'init',
        'eslint:tests',
        'jasmine:run'
    ]));

    registerTask('build', () => task.run([ // grunt build
        'init',
        'clean:dist',
        'eslint:app',
        'concat:dist',
        'uglify:dist'
    ]));

    registerTask('lint', () => task.run([ // grunt lint --target=[tests|app]
        'init',
        `eslint:${target}`
    ]));
};
})(module, require);