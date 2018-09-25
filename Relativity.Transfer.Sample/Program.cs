// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer.Sample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class Program
    {
        // TODO: Update these parameters.
        private const string RelativityUrl = "https://relativity_host.com/Relativity";
        private const string RelativityUserName = "jsmith@example.com";
        private const string RelativityPassword = "UnbreakableP@ssword777";
        private const int WorkspaceId = 1027428;

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
                            using (ITransferLog transferLog = CreateTransferLog())
                            using (IRelativityTransferHost host = CreateRelativityTransferHost(transferLog))
                            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                            {
                                CancellationToken token = cancellationTokenSource.Token;
                                await DemoBasicTransferAsync(host, token).ConfigureAwait(false);
                                await DemoAdvancedTransferAsync(host, token).ConfigureAwait(false);
                                exitCode = 0;
                            }
                        }).GetAwaiter().GetResult();
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
            catch (ApplicationException e)
            {
                // No need to include the stacktrace.
                Console2.WriteLine(ConsoleColor.Red, e.Message);
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

        private static async Task DemoBasicTransferAsync(IRelativityTransferHost host, CancellationToken token)
        {
            RelativityFileShare fileShare = await GetWorkspaceDefaultFileShareAsync(host, token).ConfigureAwait(false);
            ClientConfiguration configuration = CreateClientConfiguration();
            using (ITransferClient client = await CreateClientAsync(host, configuration, token).ConfigureAwait(false))
            using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
            {
                // Use the workspace default file share to setup the target path.
                string uploadTargetPath = GetUniqueRemoteTargetPath(fileShare);

                // Manually constructing the transfer path to demonstrate basic properties.
                string sourceFile = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Resources"), "EDRM-Sample1.JPG");
                TransferPath localSourcePath = new TransferPath
                {
                    // If the following 2 aren't specified, they're inherited from TransferRequest.
                    Direction = TransferDirection.Upload,
                    TargetPath = uploadTargetPath,

                    PathAttributes = TransferPathAttributes.File,
                    Bytes = new System.IO.FileInfo(sourceFile).Length,
                    SourcePath = sourceFile,
                    Tag = new { Name = "Hello", Value = "World" }
                };

                // This decouples all transfer events into a separate object.
                TransferContext context = CreateTransferContext();

                // Create a transfer request and upload a single local file to the remote target path.
                Console2.WriteStartHeader("Basic Transfer - Upload");
                TransferRequest uploadRequest = TransferRequest.ForUpload(localSourcePath, context);
                Console2.WriteLine("Basic upload transfer started.");
                ITransferResult uploadResult = await client.TransferAsync(uploadRequest, token).ConfigureAwait(false);
                Console2.WriteLine("Basic upload transfer completed.");
                DisplayTransferResult(uploadResult);
                Console2.WriteEndHeader();

                // Use the local directory to setup the target path.
                string downloadTargetPath = directory.Path;
                TransferPath remotePath = new TransferPath
                {
                    PathAttributes = TransferPathAttributes.File,
                    SourcePath = uploadTargetPath + "\\EDRM-Sample1.JPG",
                    TargetPath = downloadTargetPath
                };

                // Create a transfer request to download a single remote file to the local target path.
                Console2.WriteStartHeader("Basic Transfer - Download");
                TransferRequest downloadRequest = TransferRequest.ForDownload(remotePath, context);
                Console2.WriteLine("Basic download transfer started.");
                ITransferResult downloadResult = await client.TransferAsync(downloadRequest, token).ConfigureAwait(false);
                Console2.WriteLine("Basic download transfer completed.");
                DisplayTransferResult(downloadResult);
                Console2.WriteEndHeader();
            }
        }

        private static async Task DemoAdvancedTransferAsync(IRelativityTransferHost host, CancellationToken token)
        {
            // Search for the first logical file share.
            const int LogicalFileShareNumber = 1;
            RelativityFileShare fileShare = await GetFileShareAsync(host, LogicalFileShareNumber, token).ConfigureAwait(false);

            // Configure an Aspera specific transfer.
            Relativity.Transfer.Aspera.AsperaClientConfiguration configuration = CreateAsperaClientConfiguration();

            // Assigning the file share bypasses auto-configuration that will normally use the default workspace repository.
            configuration.TargetFileShare = fileShare;
            using (ITransferClient client = await CreateClientAsync(host, configuration, token).ConfigureAwait(false))
            using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
            {
                // Create a job-based upload transfer request.
                Console2.WriteStartHeader("Advanced Transfer - Upload");
                string uploadTargetPath = GetUniqueRemoteTargetPath(fileShare);
                IList<TransferPath> localSourcePaths = await SearchLocalSourcePathsAsync(client, uploadTargetPath, token).ConfigureAwait(false);
                TransferContext context = CreateTransferContext();
                TransferRequest uploadJobRequest = TransferRequest.ForUploadJob(uploadTargetPath, context);
                uploadJobRequest.Application = "Github Sample";
                uploadJobRequest.Name = "Advanced Upload Sample";

                // Create a transfer job to upload the local sample dataset to the target remote path.
                using (ITransferJob job = await client.CreateJobAsync(uploadJobRequest, token).ConfigureAwait(false))
                {
                    Console2.WriteLine("Advanced upload started.");

                    // Paths added to the async job are transferred immediately.
                    await job.AddPathsAsync(localSourcePaths, token).ConfigureAwait(false);

                    // Await completion of the job.
                    ITransferResult result = await job.CompleteAsync(token).ConfigureAwait(false);
                    Console2.WriteLine("Advanced upload completed.");
                    DisplayTransferResult(result);
                    Console2.WriteEndHeader();
                }
                
                // Create a job-based download transfer request.
                Console2.WriteStartHeader("Advanced Transfer - Download");
                string downloadTargetPath = directory.Path;
                TransferRequest downloadJobRequest = TransferRequest.ForDownloadJob(downloadTargetPath, context);
                downloadJobRequest.Application = "Github Sample";
                downloadJobRequest.Name = "Advanced Download Sample";
                Console2.WriteLine("Advanced download started.");

                // Create a transfer job to download the sample dataset to the target local path.
                using (ITransferJob job = await client.CreateJobAsync(downloadJobRequest, token).ConfigureAwait(false))
                {
                    IEnumerable<TransferPath> remotePaths = localSourcePaths.Select(localPath => new TransferPath
                    {
                        SourcePath = uploadTargetPath + "\\" + Path.GetFileName(localPath.SourcePath),
                        PathAttributes = TransferPathAttributes.File,
                        TargetPath = downloadTargetPath
                    });
                    
                    await job.AddPathsAsync(remotePaths, token).ConfigureAwait(false);
                    await ChangeDataRateAsync(job, token).ConfigureAwait(false);

                    // Await completion of the job.
                    ITransferResult result = await job.CompleteAsync(token).ConfigureAwait(false);
                    Console2.WriteLine("Advanced download completed.");
                    DisplayTransferResult(result);
                    Console2.WriteEndHeader();
                }
            }
        }

        private static void InitializeGlobalSettings()
        {
            Console2.WriteStartHeader("Initialize GlobalSettings");

            // A meaningful application name is encoded within monitoring data.
            GlobalSettings.Instance.ApplicationName = "sample-app";

            // Configure for a console-based application.
            GlobalSettings.Instance.CommandLineModeEnabled = true;
            Console2.WriteLine("Configured console settings.");

            // This will automatically write real-time entries into the transfer log.
            GlobalSettings.Instance.StatisticsLogEnabled = true;
            GlobalSettings.Instance.StatisticsLogIntervalSeconds = .5;
            Console2.WriteLine("Configured statistics settings.");

            // Limit the max target rate and throw exceptions when invalid paths are specified.
            GlobalSettings.Instance.MaxAllowedTargetDataRateMbps = 10;
            Console2.WriteLine("Configured miscellaneous settings.");
            Console2.WriteEndHeader();
        }

        private static ClientConfiguration CreateClientConfiguration()
        {
            // The configuration object provides numerous options to customize the transfer.
            return new ClientConfiguration
            {
                FileNotFoundErrorsRetry = false,
                MaxHttpRetryAttempts = 2,
                PreserveDates = true,

                // The target data rate must be < GlobalSettings.Instance.MaxAllowedTargetDataRateMbps.
                TargetDataRateMbps = 5
            };
        }

        private static Relativity.Transfer.Aspera.AsperaClientConfiguration CreateAsperaClientConfiguration()
        {
            // Each transfer client can provide a specialized The specialized configuration object provides numerous options to customize the transfer.
            return new Relativity.Transfer.Aspera.AsperaClientConfiguration
            {
                // Common properties
                BadPathErrorsRetry = false,
                FileNotFoundErrorsRetry = false,
                MaxHttpRetryAttempts = 2,
                PreserveDates = true,
                TargetDataRateMbps = 5,

                // Aspera specific properties
                EncryptionCipher = "AES_256",
                OverwritePolicy = "ALWAYS",
                Policy = "FAIR",
            };
        }

        private static ITransferLog CreateTransferLog()
        {
            // This is a standard set of options for any logger.
            Logging.LoggerOptions loggerOptions = new Logging.LoggerOptions
            {
                Application = "F456D022-5F91-42A5-B00F-5609AED8C9EF",
                ConfigurationFileLocation = Path.Combine(Environment.CurrentDirectory, "LogConfig.xml"),
                System = "Data-Transfer",
                SubSystem = "Sample-Cli"
            };

            // Configure the optional SEQ sink.
            loggerOptions.AddSinkParameter(Logging.Configuration.SeqSinkConfig.ServerUrlSinkParameterKey, new Uri("http://localhost:5341"));
            Relativity.Logging.ILog logger = Logging.Factory.LogFactory.GetLogger(loggerOptions);
            return new RelativityTransferLog(logger, true);
        }

        private static IRelativityTransferHost CreateRelativityTransferHost(ITransferLog log)
        {
            if (string.Compare(RelativityUrl, "https://relativity_host.com/Relativity", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(RelativityUserName, "jsmith@example.com", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(RelativityPassword, "UnbreakableP@ssword777", StringComparison.OrdinalIgnoreCase) == 0 ||
                WorkspaceId == 1)
            {
                throw new ApplicationException("You must update all Relativity connection parameters in order to run this application.");
            }
        
            Uri url = new Uri(RelativityUrl);
            IHttpCredential credential = new BasicAuthenticationCredential(RelativityUserName, RelativityPassword);
            RelativityConnectionInfo connectionInfo = new RelativityConnectionInfo(url, credential, WorkspaceId);
            return new RelativityTransferHost(connectionInfo, log);
        }

        private static async Task<ITransferClient> CreateClientAsync(IRelativityTransferHost host, ClientConfiguration configuration, CancellationToken token)
        {
            Console2.WriteStartHeader("Create Client");
            ITransferClient client;
            if (configuration.Client == WellKnownTransferClient.Unassigned)
            {
                // The CreateClientAsync method chooses the best client at runtime.
                Console2.WriteLine("TAPI is choosing the best transfer client...");
                client = await host.CreateClientAsync(configuration, token).ConfigureAwait(false);
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

        private static async Task<RelativityFileShare> GetWorkspaceDefaultFileShareAsync(IRelativityTransferHost host, CancellationToken token)
        {
            Console2.WriteStartHeader("Get Workspace File Share");
            Workspace workspace = await host.GetWorkspaceAsync(token).ConfigureAwait(false);
            RelativityFileShare fileShare = workspace.DefaultFileShare;
            DisplayFileShare(fileShare);
            Console2.WriteEndHeader();
            return fileShare;
        }

        private static TransferContext CreateTransferContext()
        {
            // The context object is used to decouple operations such as progress from other TAPI objects.
            TransferContext context = new TransferContext { StatisticsRateSeconds = 0.5, StatisticsEnabled = true };
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
                    Console2.WriteLine(
                        "Event=TransferPathProgress, Filename={0}, Status={1}",
                        Path.GetFileName(args.Path.SourcePath),
                        args.Status);
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

        private static async Task<RelativityFileShare> GetFileShareAsync(IRelativityTransferHost host, int number, CancellationToken token)
        {
            Console2.WriteStartHeader("Get Specified File Share");
            IFileStorageSearch fileStorageSearch = host.CreateFileStorageSearch();

            // Admin rights are required but this allows searching for all possible file shares within the instance.
            FileStorageSearchContext context = new FileStorageSearchContext { WorkspaceId = Workspace.AdminWorkspaceId };
            FileStorageSearchResults results = await fileStorageSearch.SearchAsync(context, token).ConfigureAwait(false);

            // Specify the cloud-based logical file share number - or just the 1st file share when all else fails.
            RelativityFileShare fileShare = results.GetRelativityFileShare(number) ?? results.FileShares.FirstOrDefault();
            if (fileShare == null)
            {
                throw new InvalidOperationException("This operation cannot be performed because there are no file shares available.");
            }

            DisplayFileShare(fileShare);
            Console2.WriteEndHeader();
            return fileShare;
        }

        private static async Task<IList<TransferPath>> SearchLocalSourcePathsAsync(ITransferClient client, string uploadTargetPath, CancellationToken token)
        {
            Console2.WriteStartHeader("Search Paths");
            string searchLocalPath = Path.Combine(Environment.CurrentDirectory, "Resources");
            const bool Local = true;
            PathEnumeratorContext pathEnumeratorContext = new PathEnumeratorContext(client.Configuration, new[] { searchLocalPath }, uploadTargetPath);
            pathEnumeratorContext.PreserveFolders = false;
            IPathEnumerator pathEnumerator = client.CreatePathEnumerator(Local);
            EnumeratedPathsResult result = await pathEnumerator.EnumerateAsync(pathEnumeratorContext, token).ConfigureAwait(false);
            Console2.WriteLine("Local Paths: {0}", result.LocalPaths);
            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", result.Elapsed);
            Console2.WriteLine("Total files: {0:n0}", result.TotalFileCount);
            Console2.WriteLine("Total bytes: {0:n0}", result.TotalByteCount);
            Console2.WriteEndHeader();
            return result.Paths.ToList();
        }

        private static async Task ChangeDataRateAsync(ITransferJob job, CancellationToken token)
        {
            if (job.IsDataRateChangeSupported)
            {
                Console2.WriteLine("Changing the transfer data rate...");
                await job.ChangeDataRateAsync(0, 10, token).ConfigureAwait(false);
                Console2.WriteLine("Changed the transfer data rate.");
            }
        }

        private static void DisplayFileShare(RelativityFileShare fileShare)
        {
            Console2.WriteLine("Artifact ID: {0}", fileShare.ArtifactId);
            Console2.WriteLine("Name: {0}", fileShare.Name);
            Console2.WriteLine("UNC Path: {0}", fileShare.Url);
            Console2.WriteLine("Cloud Instance: {0}", fileShare.CloudInstance);

            // RelativityOne specific properties.
            Console2.WriteLine("Number: {0}", fileShare.Number);
            Console2.WriteLine("Tenant ID: {0}", fileShare.TenantId);
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
                if (result.TransferError != null)
                {
                    Console2.WriteLine(ConsoleColor.Red, "Error: {0}", result.TransferError.Message);
                }
                else
                {
                    Console2.WriteLine(ConsoleColor.Red, "Error: Check the error log for more details.");
                }
            }

            // Display useful transfer metrics.
            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", result.Elapsed);
            Console2.WriteLine("Total files: Files: {0:n0}", result.TotalTransferredFiles);
            Console2.WriteLine("Total bytes: Files: {0:n0}", result.TotalTransferredBytes);
            Console2.WriteLine("Total files not found: {0:n0}", result.TotalFilesNotFound);
            Console2.WriteLine("Total bad path errors: {0:n0}", result.TotalBadPathErrors);
            Console2.WriteLine("Data rate: {0:#.##} Mbps", result.TransferRateMbps);
            Console2.WriteLine("Retry count: {0}", result.RetryCount);
        }

        private static string GetUniqueRemoteTargetPath(RelativityFileShare fileShare)
        {
            // Note: replace this with an easier to recognize folder name.
            string uniqueFolder = Guid.NewGuid().ToString();
            string path = string.Join("\\", fileShare.Url.TrimEnd('\\'), "_Relativity-Transfer-Sample", uniqueFolder);
            return path;
        }
    }
}