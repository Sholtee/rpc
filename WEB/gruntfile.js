/********************************************************************************
*  gruntfile.js                                                                 *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(module, require) {

module.exports = function({log, loadNpmTasks}) {
    require('matchdep').filterDev('grunt-*').forEach(task => {
        log.writeln(`Loading GRUNT task: "${task}"`);
        loadNpmTasks(task);
    });

    require('./build/gruntbase')(arguments[0], __dirname);
};

})(module, require);