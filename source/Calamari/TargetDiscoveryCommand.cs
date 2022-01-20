﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.AzureAppService
{
    [Command("target-discovery", Description = "Discover Azure web applications")]
    public class TargetDiscoveryCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<TargetDiscoveryBehaviour>();
        }
    }

    class TargetDiscoveryBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public TargetDiscoveryBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            await Task.CompletedTask;
            var targetDiscoveryContext = GetTargetDiscoveryContext(runningDeployment.Variables);
            var account = ServicePrincipalAccount.CreateFromTargetDiscoveryScope(targetDiscoveryContext.Account);
            var azureClient = account.CreateAzureClient();

            var webApps = azureClient.WebApps.ListWebAppBasic();

            foreach (var webApp in webApps.Where(app => WebAppHasMatchesScope(app, targetDiscoveryContext.Scope)))
            {
                WriteTargetCreationServiceMessage(webApp, targetDiscoveryContext.Scope);

            }
        }

        // TODO: Proper tag matching (tag names, case-sensitivity etc.)
        private bool WebAppHasMatchesScope(IWebAppBasic webApp, TargetDiscoveryContext.TargetDiscoveryScope scope) =>
            webApp.Tags.Any(tag => tag.Key == "project" && tag.Value == scope.ProjectId) &&
            webApp.Tags.Any(tag => tag.Key == "environment" && tag.Value == scope.EnvironmentId) &&
            webApp.Tags.Any(tag => tag.Key == "role" && scope.Roles.Any(role => role == tag.Value));

        private void WriteTargetCreationServiceMessage(IWebAppBasic webApp, TargetDiscoveryContext.TargetDiscoveryScope scope)
        {
            // TODO:
            // - octopusAccountIdOrName
            // - updateIfExisting
            // - 
            // TODO: Which role to use (matching role)?


            var role = webApp.Tags.First(tag => tag.Key == "role" && scope.Roles.Any(role => role == tag.Value)).Value;
            Log.Info($"##octopus[create-azurewebapptarget "
                + $"name=\"{AbstractLog.ConvertServiceMessageValue(webApp.Name)}\" "
                + $"octopusRoles=\"{role}\"]");

        }

        private TargetDiscoveryContext GetTargetDiscoveryContext(IVariables variables)
        {
            var json = variables.Get("Octopus.TargetDiscovery.Context");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<TargetDiscoveryContext>(json, options);
        }
    }
}