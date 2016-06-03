﻿using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return new[] {ScriptType.Bash.FileExtension()};
        }

        public CommandResult Execute(string scriptFile, CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner, string scriptParameters = null)
        {

            var workingDirectory = Path.GetDirectoryName(scriptFile);
            var configurationFile = BashScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = BashScriptBootstrapper.PrepareBootstrapFile(scriptFile, configurationFile, workingDirectory);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(
                    BashScriptBootstrapper.FindBashExecutable(),
                    BashScriptBootstrapper.FormatCommandArguments(boostrapFile), workingDirectory));
            }
        }
    }
}