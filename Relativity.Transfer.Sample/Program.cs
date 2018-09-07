// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer.Sample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Program
    {
        /// <summary>
        /// This object provides cancellation functionality.
        /// </summary>
        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// This object provides standard and extended I/O and file system functionality.
        /// </summary>
        private static readonly IFileSystemService FileSystemService = new FileSystemService();

        public static void Main(string[] args)
        {
            Console2.Initialize();
            Console2.WriteLine("Relativity Transfer Sample");
            int exitCode = 1;

            try
            {
                InitializeGlobalSettings();
                Task.Run(
                    async () =>
                        {
                            ClientConfiguration configuration = CreateClientConfiguration(WellKnownTransferClient.Unassigned);
                            using (ITransferLog transferLog = CreateTransferLog())
                            using (IRelativityTransferHost host = CreateRelativityTransferHost(transferLog))
                            using (ITransferClient client = await CreateClientAsync(host, configuration).ConfigureAwait(false))
                            {
                                await DemoBasicTransferAsync(host, client).ConfigureAwait(false);
                                await DemoAdvancedTransferAsync(host, client).ConfigureAwait(false);
                                exitCode = 0;
                            }
                        },
                    CancellationTokenSource.Token).GetAwaiter().GetResult();
            }
            catch (TransferException e)
            {
                if (e.Fatal)
                {
                    Console2.WriteLine(ConsoleColor.Red, "A fatal transfer failure has occurred. Error: " + e);
                }
                else
                {
                    Console2.WriteLine(ConsoleColor.Red, "A non-fatal transfer failure has occurred. Error: " + e);
                }
            }
            catch (Exception e)
            {
                Console2.WriteLine(ConsoleColor.Red, "An unexpected error has occurred. Error: " + e);
            }
            finally
            {
                Console2.WriteTerminateLine(exitCode);
                Environment.Exit(exitCode);
            }
        }

        private static async Task DemoBasicTransferAsync(IRelativityTransferHost host, ITransferClient client)
        {
            // This approach can be useful when the number of files is small and when all paths are known.
            using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
            {
                // Use the workspace default file share to setup the target path.
                RelativityFileShare fileShare = await GetWorkspaceDefaultFileShareAsync(host).ConfigureAwait(false);
                string uploadTargetPath = FileSystemService.CombineUnc(fileShare.Url, "UploadDirectDataset-" + Environment.MachineName);
                TransferPath localSourcePath = new TransferPath
                {
                    PathAttributes = TransferPathAttributes.File,
                    SourcePath = FileSystemService.Combine(FileSystemService.Combine(Environment.CurrentDirectory, "Resources"), "EDRM-Sample1.JPG"),
                    TargetPath = uploadTargetPath
                };

                // Create a transfer request and upload a single local file to the remote target path.
                Console2.WriteLine();
                Console2.WriteStartHeader("Basic Transfer - Upload");
                TransferRequest uploadRequest = TransferRequest.ForUpload(localSourcePath, uploadTargetPath);
                uploadRequest.Application = "Github Sample";
                uploadRequest.Name = "Basic Upload Sample";
                Console2.WriteLine("Basic upload transfer started.");
                ITransferResult uploadResult = await client.TransferAsync(uploadRequest, CancellationTokenSource.Token).ConfigureAwait(false);
                Console2.WriteLine("Basic upload transfer completed.");
                DisplayTransferResult(uploadResult);
                Console2.WriteEndHeader();

                // Create a transfer request to download a single remote file to the local target path.
                Console2.WriteLine();
                Console2.WriteStartHeader("Basic Transfer - Download");
                string downloadTargetPath = directory.Path;
                TransferPath remotePath = new TransferPath
                {
                    PathAttributes = TransferPathAttributes.File,
                    SourcePath = FileSystemService.CombineUnc(uploadTargetPath, "EDRM-Sample1.JPG"),
                    TargetPath = downloadTargetPath
                };

                TransferRequest downloadRequest = TransferRequest.ForDownload(remotePath, downloadTargetPath);
                downloadRequest.Application = "Github Sample";
                downloadRequest.Name = "Basic Download Sample";
                Console2.WriteLine("Basic download transfer started.");
                ITransferResult downloadResult = await client.TransferAsync(downloadRequest, CancellationTokenSource.Token).ConfigureAwait(false);
                Console2.WriteLine("Basic download transfer completed.");
                DisplayTransferResult(downloadResult);
                Console2.WriteEndHeader();
            }
        }

        private static async Task DemoAdvancedTransferAsync(IRelativityTransferHost host, ITransferClient client)
        {
            using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
            {
                // Use the specified file share to setup the transfer.
                RelativityFileShare fileShare = await GetFileShareAsync(host).ConfigureAwait(false);
                string uploadTargetPath = FileSystemService.CombineUnc(fileShare.Url, "UploadJobDataset-" + Environment.MachineName);
                IList<TransferPath> localSourcePaths = await GetLocalSourcePathsAsync(client, uploadTargetPath).ConfigureAwait(false);

                // This approach is required when the number of files is 1M+ or when the paths are obtained through some type of reader.
                Console2.WriteLine();
                Console2.WriteStartHeader("Advanced Transfer - Upload");

                // Create a transfer job to upload the local sample dataset to the target remote path.
                TransferContext context = CreateTransferContext();
                TransferRequest uploadRequest = TransferRequest.ForUploadJob(context);
                uploadRequest.Application = "Github Sample";
                uploadRequest.Name = "Advanced Upload Sample";
                using (ITransferJob job = await client.CreateJobAsync(uploadRequest, CancellationTokenSource.Token).ConfigureAwait(false))
                {
                    Console2.WriteLine("Advanced upload started.");

                    // Paths added to the async job are transferred immediately.
                    await job.AddPathsAsync(localSourcePaths, CancellationTokenSource.Token).ConfigureAwait(false);

                    // Await completion of the job.
                    ITransferResult result = await job.CompleteAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                    Console2.WriteLine("Advanced upload completed.");
                    DisplayTransferResult(result);
                    Console2.WriteEndHeader();
                }

                // Create a transfer job to download the sample dataset to the target local path.
                Console2.WriteLine();
                Console2.WriteStartHeader("Advanced Transfer - Download");
                TransferRequest downloadRequest = TransferRequest.ForDownloadJob(context);
                downloadRequest.Application = "Github Sample";
                downloadRequest.Name = "Advanced Download Sample";
                using (ITransferJob job = await client.CreateJobAsync(downloadRequest, CancellationTokenSource.Token).ConfigureAwait(false))
                {
                    Console2.WriteLine("Advanced download started.");
                    string downloadTargetPath = directory.Path;
                    IEnumerable<TransferPath> remotePaths = GetRemotePaths(localSourcePaths, downloadTargetPath, uploadTargetPath);
                    await job.AddPathsAsync(remotePaths, CancellationTokenSource.Token).ConfigureAwait(false);

                    // Await completion of the job.
                    ITransferResult result = await job.CompleteAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                    Console2.WriteLine("Advanced download completed.");
                    DisplayTransferResult(result);
                    Console2.WriteEndHeader();
                }
            }
        }

        private static void InitializeGlobalSettings()
        {
            Console2.WriteLine();
            Console2.WriteStartHeader("Initialize Global Settings");

            // Configure settings for a console-based application.
            GlobalSettings.Instance.ApmFireAndForgetEnabled = false;
            GlobalSettings.Instance.ApplicationName = "sample-app";
            GlobalSettings.Instance.CommandLineModeEnabled = true;
            Console2.WriteLine("Configured console-based settings.");

            // This will automatically write real-time entries into the transfer log.
            GlobalSettings.Instance.StatisticsLogEnabled = true;
            GlobalSettings.Instance.StatisticsLogIntervalSeconds = .5;
            Console2.WriteLine("Configured statistics-based settings.");

            // Limit the max target rate and throw exceptions when invalid paths are specified.
            GlobalSettings.Instance.MaxAllowedTargetDataRateMbps = 10;
            GlobalSettings.Instance.ValidateResolvedPaths = true;
            Console2.WriteLine("Configured miscellaneous settings.");
            Console2.WriteEndHeader();
        }

        private static ClientConfiguration CreateClientConfiguration(WellKnownTransferClient client)
        {
            // The configuration object provides numerous options to customize the transfer.
            return new ClientConfiguration
            {
                Client = client,
                FileNotFoundErrorsRetry = false,
                MaxHttpRetryAttempts = 2,
                PreserveDates = true,

                // The target data rate must be < GlobalSettings.Instance.MaxAllowedTargetDataRateMbps.
                TargetDataRateMbps = 5
            };
        }

        private static IRelativityTransferHost CreateRelativityTransferHost(ITransferLog log)
        {
            // TODO: Update with the Relativity instance, credentials, and optional workspace.
            Uri url = new Uri("https://relativity_host.com/Relativity");
            IHttpCredential credential = new BasicAuthenticationCredential("jsmith@example.com", "UnbreakableP@ssword777");
            const int WorkspaceId = 1027428;
            if (string.Compare(url.Host, "relativity_host.com", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new InvalidOperationException("This operation cannot be performed because the Relativity parameters have not been assigned.");
            }

            RelativityConnectionInfo connectionInfo = new RelativityConnectionInfo(url, credential, WorkspaceId);
            return new RelativityTransferHost(connectionInfo, log);
        }

        private static async Task<ITransferClient> CreateClientAsync(IRelativityTransferHost host, ClientConfiguration configuration)
        {
            Console2.WriteLine();
            Console2.WriteStartHeader("Create Client");
            ITransferClient client;
            if (configuration.Client == WellKnownTransferClient.Unassigned)
            {
                // The CreateClientAsync method chooses the best client at runtime.
                Console2.WriteLine("TAPI is choosing the best transfer client...");
                client = await host.CreateClientAsync(configuration, CancellationTokenSource.Token).ConfigureAwait(false);
            }
            else
            {
                // The CreateClient method creates the specified client.
                Console2.WriteLine("The API caller specified the {0} transfer client.", configuration.Client);
                client = host.CreateClient(configuration);
            }

            if (client == null)
            {
                throw new InvalidOperationException("This operation cannot be performed because a transfer client could not be created.");
            }

            Console2.WriteLine("TAPI created the {0} transfer client.", client.DisplayName);
            Console2.WriteEndHeader();
            return host.CreateClient(configuration);
        }

        private static async Task<RelativityFileShare> GetWorkspaceDefaultFileShareAsync(IRelativityTransferHost host)
        {
            Console2.WriteLine();
            Console2.WriteStartHeader("Get Workspace File Share");
            Workspace workspace = await host.GetWorkspaceAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            RelativityFileShare fileShare = workspace.DefaultFileShare;
            DisplayFileShare(fileShare);
            Console2.WriteEndHeader();
            return fileShare;
        }

        private static async Task<RelativityFileShare> GetFileShareAsync(IRelativityTransferHost host)
        {
            Console2.WriteLine();
            Console2.WriteStartHeader("Get File Share");
            IFileStorageSearch fileStorageSearch = host.CreateFileStorageSearch();

            // Admin rights are required but you can search for all possible file shares within the instance.
            FileStorageSearchContext context = new FileStorageSearchContext { WorkspaceId = Workspace.AdminWorkspaceId };
            FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, CancellationTokenSource.Token).ConfigureAwait(false);

            // Specify the cloud-based logical file share number - or just the 1st file share when all else fails.
            RelativityFileShare fileShare = results.GetRelativityFileShare(1) ?? results.FileShares.FirstOrDefault();
            if (fileShare == null)
            {
                throw new InvalidOperationException("This operation cannot be performed because there are no file shares available.");
            }

            DisplayFileShare(fileShare);
            Console2.WriteEndHeader();
            return fileShare;
        }

        private static IEnumerable<TransferPath> GetRemotePaths(IEnumerable<TransferPath> localPaths, string localTargetPath, string remoteTargetPath)
        {
            return localPaths.Select(localPath => new TransferPath
            {
                SourcePath = FileSystemService.CombineUnc(remoteTargetPath, FileSystemService.GetFileName(localPath.SourcePath)),
                PathAttributes = TransferPathAttributes.File,
                TargetPath = localTargetPath
            }).ToList();
        }

        private static async Task<IList<TransferPath>> GetLocalSourcePathsAsync(ITransferClient client, string uploadTargetPath)
        {
            string searchLocalPath = FileSystemService.Combine(Environment.CurrentDirectory, "Resources");
            const bool Local = true;
            IList<TransferPath> localSourcePaths = await SearchPathsAsync(client, Local, searchLocalPath, uploadTargetPath).ConfigureAwait(false);
            return localSourcePaths;
        }

        private static async Task<IList<TransferPath>> SearchPathsAsync(ITransferClient client, bool local, string searchPath, string targetPath)
        {
            Console2.WriteLine();
            Console2.WriteStartHeader("Search Paths");
            PathEnumeratorContext pathEnumeratorContext = new PathEnumeratorContext(client.Configuration, new[] { searchPath }, targetPath);
            pathEnumeratorContext.PreserveFolders = false;
            IPathEnumerator pathEnumerator = client.CreatePathEnumerator(local);
            EnumeratedPathsResult result = await pathEnumerator.EnumerateAsync(pathEnumeratorContext, CancellationTokenSource.Token).ConfigureAwait(false);
            Console2.WriteLine("Local Paths: {0}", result.LocalPaths);
            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", result.Elapsed);
            Console2.WriteLine("Total files: {0:n0}", result.TotalFileCount);
            Console2.WriteLine("Total bytes: {0:n0}", result.TotalByteCount);
            Console2.WriteEndHeader();
            return result.Paths.ToList();
        }

        private static TransferContext CreateTransferContext()
        {
            // The context object is used to decouple operations such as progress from other TAPI objects.
            TransferContext context = new TransferContext { StatisticsRateSeconds = 2.0 };
            context.TransferPathIssue += (sender, args) =>
            {
                Console2.WriteLine("Event=TransferPathIssue, Attributes={0}", args.Issue.Attributes);
            };

            context.TransferRequest += (sender, args) =>
            {
                Console2.WriteLine("Event=TransferRequest, Status={0}", args.Status);
            };

            context.TransferPathProgress += (sender, args) =>
            {
                Console2.WriteLine("Event=TransferPathProgress, Filename={0}, Status={1}", FileSystemService.GetFileName(args.Path.SourcePath), args.Status);
            };

            context.TransferJobRetry += (sender, args) =>
            {
                Console2.WriteLine("Event=TransferJobRetry, Retry={0}", args.Count);
            };

            context.TransferStatistics += (sender, args) =>
            {
                // Progress has already factored in file-level vs byte-level progress.
                Console2.WriteLine(
                    "EWvent=TransferStatistics, Progress: {0:00.00}%, Transfer rate: {1:00.00} Mbps, Remaining: {2:hh\\:mm\\:ss}",
                    args.Statistics.Progress,
                    args.Statistics.TransferRateMbps,
                    args.Statistics.RemainingTime);
            };

            return context;
        }

        private static ITransferLog CreateTransferLog()
        {
            Logging.LoggerOptions loggerOptions = new Logging.LoggerOptions
            {
                Application = "F456D022-5F91-42A5-B00F-5609AED8C9EF",
                ConfigurationFileLocation = FileSystemService.Combine(Environment.CurrentDirectory, "LogConfig.xml"),
                System = "Data-Transfer",
                SubSystem = "Sample-Cli"
            };

            // Configure the optional SEQ sink.
            loggerOptions.AddSinkParameter(Logging.Configuration.SeqSinkConfig.ServerUrlSinkParameterKey, new Uri("http://localhost:5341"));
            Relativity.Logging.ILog logger = Logging.Factory.LogFactory.GetLogger(loggerOptions);
            return new RelativityTransferLog(logger, true);
        }

        private static void DisplayFileShare(RelativityFileShare fileShare)
        {
            Console2.WriteLine("Artifact ID: {0}", fileShare.ArtifactId);
            Console2.WriteLine("Name: {0}", fileShare.Name);
            Console2.WriteLine("Number: {0}", fileShare.Number);
            Console2.WriteLine("Tenant ID: {0}", fileShare.TenantId);
            Console2.WriteLine("UNC Path: {0}", fileShare.Url);
        }

        private static void DisplayTransferResult(ITransferResult result)
        {
            // The original request can be accessed within the transfer result.
            Console2.WriteLine();
            Console2.WriteLine("Transfer Summary");
            Console2.WriteLine("Name: {0}", result.Request.Name);
            Console2.WriteLine("Direction: {0}", result.Request.Direction);
            if (result.Status == TransferStatus.Successful || result.Status == TransferStatus.Canceled)
            {
                Console2.WriteLine("Result: {0}", result.Status);
            }
            else
            {
                Console2.WriteLine(ConsoleColor.Red, "Result: {0}", result.Status);
            }

            // Display useful transfer metrics.
            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", result.Elapsed);
            Console2.WriteLine("Total files: Files: {0:n0}", result.TotalTransferredFiles);
            Console2.WriteLine("Total bytes: Files: {0:n0}", result.TotalTransferredBytes);
            Console2.WriteLine("Data rate: {0:#.##} Mbps", result.TransferRateMbps);
            Console2.WriteLine("Retry count: {0}", result.RetryCount);
            if (result.TransferError != null)
            {
                Console2.WriteLine(ConsoleColor.Red, "Error: {0}", result.TransferError.Message);
            }
        }
    }
}