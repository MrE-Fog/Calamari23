﻿using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.Packages.Download;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PercentFreeDiskSpacePackageCleaner : IRetentionAlgorithm
    {
        const string PackageRetentionPercentFreeDiskSpace = "OctopusPackageRetentionPercentFreeDiskSpace";
        const int DefaultPercentFreeDiskSpace = 10;
        const int FreeSpacePercentBuffer = 20;
        readonly ISortJournalEntries sortJournalEntries;
        readonly IVariables variables;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageDownloaderUtils packageUtils = new PackageDownloaderUtils();

        public PercentFreeDiskSpacePackageCleaner(ICalamariFileSystem fileSystem, ISortJournalEntries sortJournalEntries, IVariables variables, ILog log)
        {
            this.fileSystem = fileSystem;
            this.sortJournalEntries = sortJournalEntries;
            this.variables = variables;
            this.log = log;
        }

        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries)
        {
            if (!fileSystem.GetDiskFreeSpace(packageUtils.RootDirectory, out var totalNumberOfFreeBytes) || !fileSystem.GetDiskTotalSpace(packageUtils.RootDirectory, out var totalNumberOfBytes))
            {
                log.Info("Unable to determine disk space. Skipping free space package retention.");
                return new PackageIdentity[0];
            }

            var percentFreeDiskSpaceDesired = variables.GetInt32(PackageRetentionPercentFreeDiskSpace) ?? DefaultPercentFreeDiskSpace;
            var desiredSpaceInBytes = totalNumberOfBytes * (ulong) percentFreeDiskSpaceDesired / 100;
            if (totalNumberOfFreeBytes > desiredSpaceInBytes)
            {
                log.VerboseFormat("Detected enough space for new packages. ({0}/{1})", totalNumberOfBytes.ToFileSizeString(), totalNumberOfFreeBytes.ToFileSizeString());
                return new PackageIdentity[0];
            }

            var spaceToFree = (desiredSpaceInBytes - totalNumberOfFreeBytes) * (100 + FreeSpacePercentBuffer) / 100;
            ulong spaceFreed = 0L;
            var orderedJournalEntries = sortJournalEntries.Sort(journalEntries);
            return orderedJournalEntries.TakeWhile(entry =>
                                                   {
                                                       spaceFreed += entry.FileSizeBytes;
                                                       return spaceFreed < spaceToFree;
                                                   })
                                        .Select(entry => entry.Package);
        }
    }
}