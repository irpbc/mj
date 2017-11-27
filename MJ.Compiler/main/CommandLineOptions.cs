﻿using System;
using System.Collections.Generic;

using mj.compiler.utils;

using Mono.Options;

namespace mj.compiler.main
{
    public class CommandLineOptions
    {
        private static readonly Context.Key<CommandLineOptions> CONTEX_KEY = new Context.Key<CommandLineOptions>();

        public static CommandLineOptions instance(Context context)
        {
            if (context.tryGet(CONTEX_KEY, out var instance)) {
                return instance;
            }
            return new CommandLineOptions(context);
        }

        public bool Verbose { get; private set; }
        public bool DumpTree { get; private set; }
        public bool PrettyPrintTree { get; private set; }
        public bool ShowHelp { get; private set; }
        public IList<String> InputFiles { get; } = new List<String>();

        private readonly OptionSet optionSet;

        private CommandLineOptions(Context context)
        {
            context.put(CONTEX_KEY, this);

            optionSet = new OptionSet {
                {"v|verbose", s => Verbose = true},
                {"dump-tree", s => DumpTree = true},
                {"pretty-print-tree", s => PrettyPrintTree = true},
                {"h|help", s => ShowHelp = true},
                {"<>", "Input files", s => InputFiles.Add(s), true}
            };
        }

        public void readOptions(String[] options)
        {
            optionSet.Parse(options);
        }
    }
}
