﻿/********************************************************************************
* CommandLineApplication.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Internals
{
    using Properties;
    using Primitives;

    /// <summary>
    /// Base class for command line applications.
    /// </summary>
    public class CommandLineApplication
    {
        /// <summary>
        /// The command line arguments
        /// </summary>
        public IReadOnlyList<string> Args { get; }

        /// <summary>
        /// Creates a new <see cref="CommandLineApplication"/> instance.
        /// </summary>
        public CommandLineApplication(IReadOnlyList<string> args)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));

            Args = args.Count > 0 && File.Exists(args[0])
                ? args.Skip(1).ToArray()
                : args;
        }
        /// <summary>
        /// Parses the command line arguments to a typed object.
        /// </summary>
        protected internal T GetParsedArguments<T>() where T: new()
        {
            T result = new();

            Action<string>? lastSetter = null;

            foreach (string arg in Args)
            {
                if (arg.StartsWith("-", StringComparison.Ordinal))
                    lastSetter = BuildSetter(arg.TrimStart('-'));
                else
                    lastSetter?.Invoke(arg);
            }

            return result;

            Action<string>? BuildSetter(string arg)
            {
                PropertyInfo? prop = typeof(T).GetProperty(arg, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.SetProperty | BindingFlags.FlattenHierarchy);
                if (prop is null)
                    return null;

                Action<object, object?> setter = prop.ToSetter();
                System.ComponentModel.TypeConverter converter = TypeDescriptor.GetConverter(prop.PropertyType);

                return val => setter(result, converter.ConvertFromString(val));
            }
        }

        /// <summary>
        /// Runs the application
        /// </summary>
        public int Run()
        {
            try
            {
                IReadOnlyList<string> verbs = Args
                    .TakeWhile(arg => !arg.StartsWith("-", StringComparison.Ordinal))
                    .ToArray();

                IReadOnlyList<MethodInfo> compatibleMethods = GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                    .Where(m => m
                        .GetCustomAttribute<VerbAttribute>(true)?
                        .Verbs
                        .SequenceEqual(verbs, StringComparer.OrdinalIgnoreCase) is true)
                    .ToArray();

                switch (compatibleMethods.Count)
                {
                    case 0:
                        OnRun();
                        break;
                    case 1:
                        MethodInfo target = compatibleMethods[0];
                        if (target.GetParameters().Length > 0)
                            throw new InvalidOperationException(Errors.NOT_PARAMETERLESS);

                        target
                            .ToInstanceDelegate()
                            .Invoke(this, Array.Empty<object?>());
                        break;
                    default:
                        throw new InvalidOperationException(Errors.AMBIGOUS_TARGET);
                };
            }
            #pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                OnUnhandledException(ex);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// The default behavior.
        /// </summary>
        public virtual void OnRun() { }

        /// <summary>
        /// Called on unhandled exception
        /// </summary>
        public virtual void OnUnhandledException(Exception ex) => System.Diagnostics.Trace.TraceError(ex?.ToString() ?? "Unknown error");
    }
}
