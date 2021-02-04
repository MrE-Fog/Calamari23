﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class DeployAzureWebZipCommandFixture
    {
        private string _clientId;
        private string _clientSecret;
        private string _tenantId;
        private string _subscriptionId;
        private string _webappName;
        private const string _slotName = "stage";
        private string _resourceGroupName;
        private string _greeting;
        private ResourceGroupsOperations _resourceGroupClient;
        private IList<DirectoryInfo> _tempDirs;
        private string _authToken;
        private AppSettingsRoot _testAppSettings;
        private WebSiteManagementClient _webMgmtClient;

        readonly HttpClient client = new HttpClient();

        [OneTimeSetUp]
        public async Task Setup()
        {
            _resourceGroupName = Guid.NewGuid().ToString();
            _tempDirs = new List<DirectoryInfo>();

            _clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            _clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            _tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            _subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            
            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            _greeting = "Calamari";

            _authToken= await GetAuthToken(_tenantId, _clientId, _clientSecret);

            var resourcesClient = new ResourcesManagementClient(_subscriptionId,
                new ClientSecretCredential(_tenantId, _clientId, _clientSecret));

            _resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await _resourceGroupClient.CreateOrUpdateAsync(_resourceGroupName, resourceGroup);
            
            _webMgmtClient = new WebSiteManagementClient(new TokenCredentials(_authToken))
            {
                SubscriptionId = _subscriptionId,
                HttpClient = {BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint)},
            };

            var svcPlan = await _webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name,
                resourceGroup.Name, new AppServicePlan(resourceGroup.Location)
                {
                    Kind = "linux",
                    Reserved = true,
                    Sku = new SkuDescription
                    {
                        Name = "F1",
                        Tier = "Free"
                    }
                });

            var webapp = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new Site(resourceGroup.Location)
                {
                    ServerFarmId = svcPlan.Id,
                    SiteConfig = new SiteConfig
                    {
                        LinuxFxVersion = @"DOCKER|xtreampb/ubuntu_ssh",
                        AppSettings = new List<NameValuePair>
                        {
                            new NameValuePair("DOCKER_REGISTRY_SERVER_URL", "https://index.docker.io"),
                            new NameValuePair("WEBSITES_ENABLE_APP_SERVICE_STORAGE", "false")
                        }
                    }
                });

            //var slot =
            //    await _webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroup.Name, webapp.Name, webapp,
            //        "stage");

            _webappName = webapp.Name;
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            await _resourceGroupClient.StartDeleteAsync(_resourceGroupName);

            //foreach (var tempDir in _tempDirs)
            //{
            //    if(tempDir.Exists)
            //        tempDir.Delete(true);
            //}
        }

        [Test]
        public async Task Deploy_WebAppZip_Simple()
        {
            //await Task.Delay(500);
            var tempPath = TemporaryDirectory.Create();
            _tempDirs.Add(new DirectoryInfo(tempPath.DirectoryPath));
            new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            File.WriteAllText(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                "Hello #{Greeting}");
            ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage",
                $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip");

            await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    //context.WithFilesToCopy($"{tempPath.DirectoryPath}.zip");
                    context.WithPackage($"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip",
                        "AzureZipDeployPackage", "1.0.0");
                    AddDefaults(context, _webappName);
                })
                .Execute();
            await AssertContent($"{_webappName}-{_slotName}.azurewebsites.net", $"Hello {_greeting}");
            await AssertAppSettings();
        }

        void AddDefaults(CommandTestBuilderContext context, string webAppName)
        {
            context.Variables.Add(AccountVariables.ClientId, _clientId);
            context.Variables.Add(AccountVariables.Password, _clientSecret);
            context.Variables.Add(AccountVariables.TenantId, _tenantId);
            context.Variables.Add(AccountVariables.SubscriptionId, _subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", _resourceGroupName);
            context.Variables.Add("Octopus.Action.Azure.WebAppName", webAppName);
            context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            context.Variables.Add("Octopus.Action.Azure.DeploymentSlot", _slotName);
            
            context.Variables.Add(PackageVariables.SubstituteInFilesTargets, "index.html");
            context.Variables.Add("Greeting", _greeting);
        }
        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {

            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }

        async Task AssertAppSettings()
        {
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(_webappName, _slotName);
            targetSite.ResourceGroupName = _resourceGroupName;

            var settings = await AppSettingsManagement.GetAppSettingsAsync(_webMgmtClient, _authToken, targetSite);

            var testSettingsJson = JsonConvert.SerializeObject(settings);
            var controlSettingsJson = JsonConvert.SerializeObject(_testAppSettings);

            //Assert.AreEqual(_testAppSettings,settings);
            Assert.AreEqual(controlSettingsJson, testSettingsJson);
        }

        private async Task<string> GetAuthToken(string tenantId, string applicationId, string password)
        {
            var activeDirectoryEndPoint = @"https://login.windows.net/";
            var managementEndPoint = @"https://management.azure.com/";
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            //Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(managementEndPoint,
                new ClientCredential(applicationId, password));

            return result.AccessToken;
        }

        string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }

            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}