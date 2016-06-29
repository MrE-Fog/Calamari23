﻿using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.FSharp
{
    [TestFixture]
    public class FSharpFixture : CalamariFixture
    {
        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldPrintEncodedVariable()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "PrintEncodedVariable.fsx")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldCreateArtifact()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CreateArtifact.fsx")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact");
            output.AssertOutput("name='bXlGaWxlLnR4dA==' length='MTAw']");
        }

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldCallHello()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "Paul");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Paul");
            }
        }

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var sensitiveVariables = new VariableDictionary();
            sensitiveVariables.Set("Name", "NameToEncrypt");
            var encryptedContent = new AesEncryption("5XETGOgqYR2bRhlfhDruEg==").Encrypt(sensitiveVariables.SaveAsString());
            File.WriteAllBytes(variablesFile, encryptedContent);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("sensitiveVariables", variablesFile)
                    .Argument("sensitiveVariablesPassword", "5XETGOgqYR2bRhlfhDruEg=="));

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
            }
        }

        // Add test with variable substitution

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldCallHelloDirectValue()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "direct value");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.fsx"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello direct value");
            }
        }

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldCallHelloDefaultValue()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "HelloDefaultValue.fsx")));

            output.AssertSuccess();
            output.AssertOutput("Hello default value");
        }

        [Test, RequiresDotNet45, RequiresMono4]
        public void ShouldConsumeParametersWithQuotes()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Parameters.fsx"))
                .Argument("scriptParameters", "\"Para meter0\" Parameter1")); ;

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0-Parameter1");
        }
    }
}