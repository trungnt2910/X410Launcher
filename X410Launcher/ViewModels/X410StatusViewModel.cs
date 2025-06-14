using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using X410Launcher.Tools;

namespace X410Launcher.ViewModels
{
    public class X410StatusViewModel : ObservableObject
    {
        public const string StatusTextReady = "Ready.";

        public const string StatusTextFetching = "Fetching packages from provider: {0}...";
        public const string StatusTextFetchFailed = "Failed to fetch packages.";
        public const string StatusTextFetchCompleted = "Fetched {0} packages.";

        public const string StatusTextDownloading = "Downloading package: {0}...";
        public const string StatusTextDownloadExpired = "The selected package has expired. Please refresh your package list.";
        public const string StatusTextDownloadFailed = "Failed to download package.";
        public const string StatusTextDownloadArchNoSupport = "Your operating system architecture, {0}, is not supported.";

        public const string StatusTextExtracting = "Extracting {0} to {1}...";
        public const string StatusTextExtractingHelper = "Extracting helper library to {0}...";
        public const string StatusTextPatching = "Patching {0}...";

        public const string StatusTextInstallFailed = "Failed to install package.";
        public const string StatusTextInstallCompleted = "Successfully installed package.";

        public const string StatusTextUninstallCompleted = "Successfully uninstalled package.";

        public const string StatusTextKilled = "Sucessfully killed X410 process.";

        public const string StatusTextLaunching = "Starting X410 process...";
        public const string StatusTextLaunchWaiting = "Waiting for response from X410 process...";
        public const string StatusTextLaunched = "Sucessfully started X410 process.";
        public const string StatusTextLaunchFailed = "Failed to start X410 process.";

        public const double ProgressIndeterminate = -1;
        public const double ProgressMin = 0;
        public const double ProgressMax = 100;

        public const int DownloadMaxRetries = 128;
        public const int DownloadBufferSize = 32768;

        public const int LaunchMaxRetries = 4;
        public const int LaunchDelayMilliseconds = 1000;

        private string? _installedVersion;
        public string? InstalledVersion
        {
            get => _installedVersion;
            private set => SetProperty(ref _installedVersion, value);
        }

        private string? _installedArchitecture;
        public string? InstalledArchitecture
        {
            get => _installedArchitecture;
            private set => SetProperty(ref _installedArchitecture, value);
        }

        private string? _latestVersion;
        public string? LatestVersion
        {
            get => _latestVersion;
            private set => SetProperty(ref _latestVersion, value);
        }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        private uint? _displayNumber;
        public uint? DisplayNumber
        {
            get => _displayNumber;
            private set => SetProperty(ref _displayNumber, value);
        }

        public string? _subscriptionStatus;
        public string? SubscriptionStatus
        {
            get => _subscriptionStatus;
            private set => SetProperty(ref _subscriptionStatus, value);
        }

        private ObservableCollection<PackageInfo> _packages = new();
        public ObservableCollection<PackageInfo> Packages
        {
            get => _packages;
            private set => SetProperty(ref _packages, value);
        }

        private string _statusText = StatusTextReady;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private double _progress = 0;
        public double Progress
        {
            get => _progress;
            private set
            {
                SetProperty(ref _isIndeterminate, value < 0, nameof(ProgressIsIndeterminate));
                SetProperty(ref _progress, value);
            }
        }

        private bool _isIndeterminate = false;
        public bool ProgressIsIndeterminate
        {
            get => _isIndeterminate;
        }

        private string _appId = "9PM8LP83G3L3";
        public string AppId
        {
            get => _appId;
            private set
            {
                SetProperty(ref _appId, value);
                OnPropertyChanged(nameof(StoreLink));
            }
        }

        public string StoreLink
        {
            get => $"https://www.microsoft.com/store/apps/{_appId}";
        }

        private string _api = "https://store.rg-adguard.net/api/";
        public string Api
        {
            get => _api;
            private set => SetProperty(ref _api, value);
        }

        public void RefreshInstalledVersion()
        {
            var appxManifestPath = Path.Combine(Paths.GetAppInstallPath(), "AppxManifest.xml");
            if (File.Exists(appxManifestPath))
            {
                using var stream = File.OpenRead(appxManifestPath);
                var deserializer = new XmlSerializer(typeof(Models.Appx.Package));
                var package = deserializer.Deserialize(stream) as Models.Appx.Package;
                InstalledVersion = package?.Identity?.VersionString;
                InstalledArchitecture = package?.Identity?.ProcessorArchitecture.ToString();
            }
            else
            {
                InstalledVersion = null;
            }
        }

        public void RefreshAppStatus()
        {
            IsRunning = X410.AreYouThere();
            DisplayNumber = X410.GetDisplayNumber();
            SubscriptionStatus = X410.GetSubscriptionStatus() switch
            {
                X410.SubscriptionStatus.SubscriptionActive
                    => "Active",
                X410.SubscriptionStatus.TrialValid
                    => "Trial",
                X410.SubscriptionStatus.SubscriptionExpired or
                X410.SubscriptionStatus.TrialExpired
                    => "Expired",
                X410.SubscriptionStatus.NoAppUseEntitlement or
                X410.SubscriptionStatus.StoreError
                    => "Error",
                X410.SubscriptionStatus.Unknown or _
                    => "Unknown"
            };
        }

        public async Task RefreshAsync()
        {
            RefreshInstalledVersion();
            RefreshAppStatus();

            Packages.Clear();
            Progress = ProgressIndeterminate;
            StatusText = string.Format(StatusTextFetching, _api);

            try
            {
                var msPackage = new MicrosoftStorePackage(_appId, _api);
                await msPackage.LoadAsync();
                var desiredPackageArchitectures = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => new[] { Tools.PackageArchitecture.x64, Tools.PackageArchitecture.x86, Tools.PackageArchitecture.neutral },
                    Architecture.X86 => new[] { Tools.PackageArchitecture.x86, Tools.PackageArchitecture.neutral },
                    Architecture.Arm64 => new[] { Tools.PackageArchitecture.arm64, Tools.PackageArchitecture.neutral },
                    Architecture.Arm => new[] { Tools.PackageArchitecture.arm, Tools.PackageArchitecture.neutral },
                    _ => new[] { Tools.PackageArchitecture.neutral },
                };
                Packages = new ObservableCollection<PackageInfo>(
                    msPackage.Locations.Where(p =>
                        !p.Name.StartsWith("Microsoft.VCLibs") &&
                        desiredPackageArchitectures.Contains(p.Architecture)
                    ).OrderByDescending(p => p.Version));

                LatestVersion = Packages.FirstOrDefault()?.Version.ToString();
            }
            catch
            {
                Progress = ProgressMin;
                StatusText = StatusTextFetchFailed;
                throw;
            }

            Progress = ProgressMax;
            StatusText = string.Format(StatusTextFetchCompleted, Packages.Count);
        }

        public async Task InstallPackageAsync(int index)
        {
            InstalledVersion = null;

            var selectedPackage = _packages[index];

            if (selectedPackage.ExpireTime <= DateTime.Now)
            {
                StatusText = StatusTextDownloadExpired;
                RefreshInstalledVersion();
                throw new InvalidOperationException(StatusTextDownloadExpired);
            }

            StatusText = string.Format(StatusTextDownloading, selectedPackage.Name);
            Progress = ProgressIndeterminate;
            using var packageStream = new MemoryStream();

            try
            {
                await selectedPackage.DownloadAsync((buffer, currentBytesRead, bytesRead, totalBytes) =>
                {
                    if (totalBytes > 0)
                    {
                        Progress = ProgressMin + ((ProgressMax - ProgressMin) * (bytesRead / (double)totalBytes));
                    }
                    else
                    {
                        Progress = -1;
                    }
                    packageStream.Write(buffer, 0, currentBytesRead);
                });
            }
            catch
            {
                StatusText = StatusTextDownloadFailed;
                Progress = ProgressMin;
                RefreshInstalledVersion();
                throw;
            }

            packageStream.Seek(0, SeekOrigin.Begin);

            var wasRunning = X410.AreYouThere();

            try
            {
                switch (selectedPackage.Format)
                {
                    case PackageFormat.appxbundle:
                    case PackageFormat.msixbundle:
                        {
                            using var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
                            using var appxBundleManifestStream =
                                zipArchive.Entries.First(e => e.FullName == "AppxMetadata/AppxBundleManifest.xml").Open();
                            var serializer = new XmlSerializer(typeof(Models.AppxBundle.Bundle));
                            var bundle = (serializer.Deserialize(appxBundleManifestStream) as Models.AppxBundle.Bundle)!;
                            var architecture = RuntimeInformation.OSArchitecture switch
                            {
                                Architecture.X64 => Models.AppxBundle.PackageArchitecture.X64,
                                Architecture.X86 => Models.AppxBundle.PackageArchitecture.X86,
                                Architecture.Arm64 => Models.AppxBundle.PackageArchitecture.Arm64,
                                Architecture.Arm => Models.AppxBundle.PackageArchitecture.Arm,
                                _ => Models.AppxBundle.PackageArchitecture.Neutral
                            };
                            var package = bundle.Packages
                                    .FirstOrDefault(p =>
                                        p.Architecture == architecture &&
                                        p.Type == Models.AppxBundle.PackageType.Application);
                            if (package == null)
                            {
                                var error = string.Format(StatusTextDownloadArchNoSupport, RuntimeInformation.OSArchitecture);
                                StatusText = error;
                                Progress = ProgressMin;
                                throw new InvalidOperationException(error);
                            }
                            using var newPackageZipStream = zipArchive.Entries.First(e => e.FullName == package.FileName).Open();
                            // We don't know if disposing the old stream corrupts the zip archive.
                            // Therefore we use this temporary stream instead.
                            using var newPackageMemoryStream = new MemoryStream();
                            await newPackageZipStream.CopyToAsync(newPackageMemoryStream);
                            newPackageMemoryStream.Seek(0, SeekOrigin.Begin);

                            packageStream.SetLength(newPackageMemoryStream.Length);
                            packageStream.Seek(0, SeekOrigin.Begin);
                            await newPackageMemoryStream.CopyToAsync(packageStream);

                            // Now, we have the desired appx file.
                            packageStream.Seek(0, SeekOrigin.Begin);
                        }
                        break;
                    case PackageFormat.msix:
                    case PackageFormat.appx:
                        // Do nothing, we already have the appx stream.
                        break;
                }

                using var appxArchive = new ZipArchive(packageStream);
                var appPath = Paths.GetAppInstallPath();

                Models.Appx.Package manifest;
                using (var appxManifestStream = appxArchive.Entries
                        .First(e => e.FullName == "AppxManifest.xml").Open())
                {
                    var serializer = new XmlSerializer(typeof(Models.Appx.Package));
                    manifest = (serializer.Deserialize(appxManifestStream) as Models.Appx.Package)!;
                }

                await UninstallPackageAsync();

                var directoryInfo = Directory.CreateDirectory(appPath);
                var text = directoryInfo.FullName;
                var totalLength = packageStream.Length;
                var extractedLength = 0L;

                Progress = ProgressMin;

                foreach (var entry in appxArchive.Entries)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(text, entry.FullName));

                    StatusText = string.Format(StatusTextExtracting, entry.FullName, fullPath);
                    Progress = ProgressMin + ((ProgressMax - ProgressMin) * (extractedLength / (double)totalLength));

                    if (Path.GetFileName(fullPath).Length == 0)
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        await Task.Run(() =>
                        {
                            entry.ExtractToFile(fullPath, overwrite: true);
                        });
                        extractedLength += entry.CompressedLength;
                    }
                }

                using (var helperStream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(
                        $"X410Launcher.Native.X410.{RuntimeInformation.OSArchitecture}.dll"
                ))
                {
                    if (helperStream is not null)
                    {
                        var helperPath = Paths.GetHelperDllFile();
                        StatusText = string.Format(StatusTextExtractingHelper, helperPath);
                        await Task.Run(() =>
                        {
                            using var memory = new MemoryStream();
                            helperStream.CopyTo(memory);
                            File.WriteAllBytes(helperPath, memory.ToArray());
                        });
                    }
                }

                Progress = ProgressMax;
                StatusText = StatusTextInstallCompleted;
                RefreshInstalledVersion();
            }
            catch
            {
                Progress = ProgressMin;
                StatusText = StatusTextInstallFailed;
                RefreshInstalledVersion();
                throw;
            }
            finally
            {
                if (wasRunning)
                {
                    await LaunchAsync();
                }
            }
        }

        public async Task UninstallPackageAsync()
        {
            await KillAsync();

            var appPath = Paths.GetAppInstallPath();

            await Task.Run(() =>
            {
                if (Directory.Exists(appPath))
                {
                    Directory.Delete(appPath, true);
                }
            });

            RefreshInstalledVersion();

            StatusText = StatusTextUninstallCompleted;
        }

        public async Task KillAsync()
        {
            await Task.Run(() =>
            {
                // Give the app a chance to exit cleanly.
                while (X410.AppExit())
                {
                    continue;
                }

                foreach (var proc in Process.GetProcessesByName("X410"))
                {
                    proc.Kill();
                }

                // Kill any hanging settings menu as well.
                foreach (var proc in Process.GetProcessesByName("X410.Settings"))
                {
                    proc.Kill();
                }
            });

            RefreshAppStatus();

            StatusText = StatusTextKilled;
        }

        public async Task LaunchAsync()
        {
            StatusText = StatusTextLaunching;

            await Task.Run(async () =>
            {
                if (!X410.AreYouThere())
                {
                    try
                    {
                        Launcher.Launch(Paths.GetAppFile());
                    }
                    catch
                    {
                        StatusText = StatusTextLaunchFailed;
                        return;
                    }
                }

                for (int i = 0; i < LaunchMaxRetries; ++i)
                {
                    if (X410.AreYouThere())
                    {
                        X410.SetFocus();

                        // Launch settings app for more initial visibility.
                        Launcher.LaunchSettings(Paths.GetSettingsAppFile());

                        Progress = ProgressMax;
                        StatusText = StatusTextLaunched;

                        return;
                    }

                    Progress = -1;
                    StatusText = StatusTextLaunchWaiting;

                    await Task.Delay(LaunchDelayMilliseconds);
                }

                Progress = ProgressMin;
                StatusText = StatusTextLaunchFailed;
            });

            RefreshAppStatus();
        }
    }
}
