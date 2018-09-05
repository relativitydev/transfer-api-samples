// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="kCura Corp">
//   Relativity ODA LLC (C) 2018 All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;

	public class Program
	{
		private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
	    private static readonly IFileSystemService fileSystemService = new FileSystemService();

		public static void Main(string[] args)
        {
			Console2.Initialize();
			Console2.WriteLine("Relativity Transfer Sample");
	        int exitCode = 1;

            try
            {
	            // Setup all 1-time global settings before any transfers are created.
	            InitializeGlobalSettings();
				Task.Run(async () =>
                {
					ClientConfiguration configuration = CreateClientConfiguration(WellKnownTransferClient.Unassigned);

					// Ensure these objects are properly disposed.
					using (ITransferLog transferLog = CreateTransferLog())
                    using (IRelativityTransferHost host = CreateRelativityTransferHost(transferLog))
                    using (ITransferClient client = await CreateClientAsync(host, configuration).ConfigureAwait(false))
                    {
						// Simple direct transfer.
	                    await DemoDirectTransferAsync(client).ConfigureAwait(false);

						// Advanced job-based transfer.
	                    await DemoJobTransferAsync(host, client).ConfigureAwait(false);
	                    exitCode = 0;
                    }
                }, cancellationTokenSource.Token).GetAwaiter().GetResult();
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
	            Console2.WriteTerminateLine();
				Environment.Exit(exitCode);
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

		private static async Task<RelativityFileShare> GetWorkspaceDefaultFileShareAsync(ITransferClient client)
	    {
		    Console2.WriteLine(); 
			Console2.WriteStartHeader("Get Workspace File Share");
			Workspace workspace = await client.GetWorkspaceAsync(cancellationTokenSource.Token).ConfigureAwait(false);
		    RelativityFileShare fileShare = workspace.DefaultFileShare;
		    DisplayFileShare(fileShare);
		    Console2.WriteEndHeader();
			return workspace.DefaultFileShare;
	    }

		private static async Task<RelativityFileShare> GetFileShareAsync(IRelativityTransferHost host)
		{
			Console2.WriteLine();
			Console2.WriteStartHeader("Get File Share");
			IFileStorageSearch fileStorageSearch = host.CreateFileStorageSearch();

			// Admin rights are required but you can search for all possible file shares within the instance.
			FileStorageSearchContext context = new FileStorageSearchContext { WorkspaceId = Workspace.AdminWorkspaceId };
			FileStorageSearchResults results =  await fileStorageSearch.SearchAsync(context, cancellationTokenSource.Token).ConfigureAwait(false);

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

	    private static async Task<ITransferClient> CreateClientAsync(IRelativityTransferHost host, ClientConfiguration configuration)
	    {
		    Console2.WriteLine();
			Console2.WriteStartHeader("Create Client");
		    ITransferClient client;
			if (configuration.Client == WellKnownTransferClient.Unassigned)
		    {
			    // The CreateClientAsync method chooses the best client at runtime.
				Console2.WriteLine("TAPI is choosing the best transfer client...");
			    client = await host.CreateClientAsync(configuration, cancellationTokenSource.Token).ConfigureAwait(false);
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

		private static async Task DemoDirectTransferAsync(ITransferClient client)
	    {
			// This approach can be useful when the number of files is small and when all paths are known.
		    using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
		    {
			    // Use the workspace default file share to setup the target paths.
			    RelativityFileShare fileShare = await GetWorkspaceDefaultFileShareAsync(client).ConfigureAwait(false);
			    string uploadTargetPath = Path.Combine(fileShare.Url, "UploadDirectDataset-" + Environment.MachineName);
			    IList<TransferPath> localSourcePaths = await GetLocalSourcePathsAsync(client, uploadTargetPath).ConfigureAwait(false);

			    // Create a transfer request and upload the local sample dataset to the target remote path.
				Console2.WriteLine();
			    Console2.WriteStartHeader("Direct Transfer - Upload");
			    TransferContext context = CreateTransferContext();
			    TransferRequest uploadRequest = TransferRequest.ForUpload(localSourcePaths, uploadTargetPath, context);
			    uploadRequest.Application = "Github Sample";
			    uploadRequest.Name = "Direct Upload Sample";
				Console2.WriteLine("Direct upload transfer started.");
			    ITransferResult uploadResult = await client.TransferAsync(uploadRequest, cancellationTokenSource.Token).ConfigureAwait(false);
			    Console2.WriteLine("Direct upload transfer completed.");
				DisplayTransferResult(uploadResult);
			    Console2.WriteEndHeader();

			    // Create a transfer request to download the sample dataset to the target local path.
			    Console2.WriteLine();
			    Console2.WriteStartHeader("Direct Transfer - Download");
			    string downloadTargetPath = directory.Path;
				IEnumerable<TransferPath> remotePaths = GetRemotePaths(localSourcePaths, downloadTargetPath, uploadTargetPath);
			    TransferRequest downloadRequest = TransferRequest.ForDownload(remotePaths, downloadTargetPath, context);
			    downloadRequest.Application = "Github Sample";
			    downloadRequest.Name = "Direct Download Sample";
			    Console2.WriteLine("Direct download transfer started.");
				ITransferResult downloadResult = await client.TransferAsync(downloadRequest, cancellationTokenSource.Token).ConfigureAwait(false);
			    Console2.WriteLine("Direct download transfer completed.");
				DisplayTransferResult(downloadResult);
			    Console2.WriteEndHeader();
		    }
	    }

		private static async Task DemoJobTransferAsync(IRelativityTransferHost host, ITransferClient client)
	    {
		    using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
		    {
			    // Use the specified file share to setup the transfer.
			    RelativityFileShare fileShare = await GetFileShareAsync(host).ConfigureAwait(false);
			    string uploadTargetPath = Path.Combine(fileShare.Url, "UploadJobDataset-" + Environment.MachineName);
			    IList<TransferPath> localSourcePaths = await GetLocalSourcePathsAsync(client, uploadTargetPath).ConfigureAwait(false);

				// This approach is required when the number of files is 1M+ or when the paths are obtained through some type of reader.
				Console2.WriteLine();
			    Console2.WriteStartHeader("Job Transfer - Upload");

			    // Create a transfer job to upload the local sample dataset to the target remote path.
			    TransferContext context = CreateTransferContext();
			    TransferRequest uploadRequest = TransferRequest.ForUploadJob(context);
			    uploadRequest.Application = "Github Sample";
			    uploadRequest.Name = "Job Upload Sample";
				using (ITransferJob job = await client.CreateJobAsync(uploadRequest, cancellationTokenSource.Token).ConfigureAwait(false))
			    {
				    Console2.WriteLine("Job upload started.");

					// Paths added to the async job are transferred immediately.
					await job.AddPathsAsync(localSourcePaths, cancellationTokenSource.Token).ConfigureAwait(false);

				    // Await completion of the job.
				    ITransferResult result = await job.CompleteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
				    Console2.WriteLine("Job upload completed.");
					DisplayTransferResult(result);
				    Console2.WriteEndHeader();
			    }

			    // Create a transfer job to download the sample dataset to the target local path.
			    Console2.WriteLine();
			    Console2.WriteStartHeader("Job Transfer - Download");
			    TransferRequest downloadRequest = TransferRequest.ForDownloadJob(context);
			    downloadRequest.Application = "Github Sample";
			    downloadRequest.Name = "Job Download Sample";
			    using (ITransferJob job = await client.CreateJobAsync(downloadRequest, cancellationTokenSource.Token).ConfigureAwait(false))
			    {
				    Console2.WriteLine("Job download started.");
					string downloadTargetPath = directory.Path;
					IEnumerable<TransferPath> remotePaths = GetRemotePaths(localSourcePaths, downloadTargetPath, uploadTargetPath);
				    await job.AddPathsAsync(remotePaths, cancellationTokenSource.Token).ConfigureAwait(false);

				    // Await completion of the job.
				    ITransferResult result = await job.CompleteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
				    Console2.WriteLine("Job download completed.");
					DisplayTransferResult(result);
				    Console2.WriteEndHeader();
			    }
		    }
	    }

	    private static IEnumerable<TransferPath> GetRemotePaths(IEnumerable<TransferPath> localPaths, string localTargetPath, string remoteTargetPath)
	    {
		    return localPaths.Select(localPath => new TransferPath
			    {
				    SourcePath = fileSystemService.CombineUnc(remoteTargetPath, fileSystemService.GetFileName(localPath.SourcePath)),
				    PathAttributes = TransferPathAttributes.File,
				    TargetPath = localTargetPath
			    })
			    .ToList();
	    }

	    private static Task<IList<TransferPath>> GetLocalSourcePathsAsync(ITransferClient client, string uploadTargetPath)
	    {
		    string searchLocalPath = Path.Combine(Environment.CurrentDirectory, "Resources");
		    const bool Local = true;
			Task<IList<TransferPath>> localSourcePaths = SearchPathsAsync(client, Local, searchLocalPath, uploadTargetPath);
		    return localSourcePaths;
	    }

		private static async Task<IList<TransferPath>> SearchPathsAsync(ITransferClient client, bool local, string searchPath, string targetPath)
	    {
		    Console2.WriteLine();
		    Console2.WriteStartHeader("Search Paths");
			PathEnumeratorContext pathEnumeratorContext = new PathEnumeratorContext(client.Configuration, new[] {searchPath}, targetPath);
		    pathEnumeratorContext.PreserveFolders = false;
		    IPathEnumerator pathEnumerator = client.CreatePathEnumerator(local);
		    EnumeratedPathsResult result = await pathEnumerator.EnumerateAsync(pathEnumeratorContext, cancellationTokenSource.Token).ConfigureAwait(false);
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
		        Console2.WriteLine("Event=TransferPathProgress, Filename={0}, Status={1}", Path.GetFileName(args.Path.SourcePath), args.Status);
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
			Assembly assembly = Assembly.GetEntryAssembly();
			string directory = Directory.GetParent(assembly.Location).FullName;
			Logging.LoggerOptions loggerOptions = new Logging.LoggerOptions
			{
				Application = "F456D022-5F91-42A5-B00F-5609AED8C9EF",
				ConfigurationFileLocation = Path.Combine(directory, "LogConfig.xml"),
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