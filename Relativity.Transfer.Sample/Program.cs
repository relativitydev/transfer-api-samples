// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="kCura Corp">
//   Relativity ODA LLC (C) 2017 All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer
{
    using System;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// Represents a sample console application to demo basic Transfer API usage.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        public static void Main(string[] args)
        {
            // Uncomment to suppress SSL validation errors.
            //// ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;

            try
            {
                // Setup global settings for all transfers.
                GlobalSettings.Instance.ApmFireAndForgetEnabled = false;
                GlobalSettings.Instance.ApplicationName = "sample-app";
                GlobalSettings.Instance.CommandLineModeEnabled = true;                

                // Don't be too aggressive with logging statistics.
                GlobalSettings.Instance.StatisticsLogEnabled = true;
                GlobalSettings.Instance.StatisticsLogIntervalSeconds = 2;

                // Any attempt to exceed this max will throw an exception.
                GlobalSettings.Instance.MaxAllowedTargetDataRateMbps = 100;

                // All temp files can be stored in a specific directory or UNC share.
                GlobalSettings.Instance.TempDirectory = Path.Combine(Environment.CurrentDirectory, "Transfer-Logs");

                // Setup Relativity Logging
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
                string directory = Directory.GetParent(assembly.Location).FullName;
                Relativity.Logging.LoggerOptions loggerOptions = new Relativity.Logging.LoggerOptions
                {
                    Application = "F456D022-5F91-42A5-B00F-5609AED8C9EF",
                    ConfigurationFileLocation = Path.Combine(directory, "LogConfig.xml"),
                    System = "Data-Transfer",
                    SubSystem = "Sample-Cli"
                };

                loggerOptions.AddSinkParameter(
                    Logging.Configuration.SeqSinkConfig.ServerUrlSinkParameterKey,
                    new Uri("http://localhost:5341"));
                Relativity.Logging.ILog logger = Relativity.Logging.Factory.LogFactory.GetLogger(loggerOptions);
                using (ITransferLog transferLog = new RelativityTransferLog(logger, false))
                {
                    ExecuteUploadDemo(transferLog);
                }
            }
            catch (TransferException e)
            {
                if (e.Fatal)
                {
                    Console.WriteLine("A fatal transfer failure has occurred. Error: " + e);
                }
                else
                {
                    Console.WriteLine("A non-fatal transfer failure has occurred. Error: " + e);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("A serious transfer failure has occurred. Error: " + e);
            }
            finally
            {
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Executes an upload demo.
        /// </summary>
        /// <param name="transferLog">
        /// The custom transfer log.
        /// </param>
        private static async void ExecuteUploadDemo(ITransferLog transferLog)
        {
            // The context object is used to decouple operations such as progress from other TAPI objects.
            TransferContext context = new TransferContext { StatisticsRateSeconds = 2.0 };
            context.TransferPathIssue += (sender, args) =>
                {
                    Console.WriteLine($"*** The transfer error has occurred. Attributes={args.Issue.Attributes}");
                };

            context.TransferRequest += (sender, args) =>
                {
                    Console.WriteLine($"*** The transfer request {args.Request.ClientRequestId} status has changed. Status={args.Status}");
                };

            context.TransferPathProgress += (sender, args) =>
                {
                    if (args.Status == TransferPathStatus.Successful)
                    {
                        Console.WriteLine($"*** The source file '{args.Path.SourcePath}' transfer is successful.");
                    }
                };

            context.TransferJobRetry += (sender, args) =>
                {
                    Console.WriteLine($"*** Job {args.Request.ClientRequestId} is retrying. Retry={args.Count}");
                };

            context.TransferStatistics += (sender, args) =>
                {
                    // Disabling since the GlobalSettings are already configured to write the transfer statistics to the console.
                    // Console.WriteLine($"*** Progress: {args.Statistics.Progress:00.00}%, Transfer rate: {args.Statistics.TransferRateMbps:00.00} Mbps, Remaining: {args.Statistics.RemainingTime:hh\\:mm\\:ss}");
                };

            // The CancellationTokenSource is used to cancel the transfer operation.
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            //// cancellationTokenSource.Cancel();

            // TODO: Update with the Relativity instance, credentials, and workspace.
            Uri relativityHost = new Uri("https://relativity_host.com/Relativity");
            IHttpCredential credential = new BasicAuthenticationCredential("jsmith@example.com", "UnbreakableP@ssword777");
            int workspaceId = 1027428;

            // The configuration object provides numerous options to customize the transfer.
            ClientConfiguration configuration =
                new ClientConfiguration
                    {
                        PreCalculateJobSize = false,
                        FileNotFoundErrorsRetry = false,
                        PreserveDates = true
                    };

            // The CreateClientAsync method chooses the best client at runtime.
            using (IRelativityTransferHost host = new RelativityTransferHost(new RelativityConnectionInfo(relativityHost, credential, workspaceId), transferLog))
            using (ITransferClient client = await host.CreateClientAsync(configuration, cancellationTokenSource.Token))
            {
                // Display a friendly name for the client that was just created.
                Console.WriteLine($"Client {client.DisplayName} has been created.");

                // Retrieve workspace details in order to specify the correct target path.
                Workspace workspace = await client.GetWorkspaceAsync(cancellationTokenSource.Token);
                string targetPath = Path.Combine(workspace.DefaultFileShareUncPath, "UploadDataset");
                TransferRequest uploadRequest = TransferRequest.ForUploadJob(targetPath, context);

                // Once the job is created, an asynchronous queue is available to add paths and perform immediate transfers.
                using (ITransferJob job = await client.CreateJobAsync(uploadRequest, cancellationTokenSource.Token))
                {
                    // Setup the upload request using 1 of 2 approaches.
                    // 1. Use the IPathEnumerator API.
                    // 2. Specify the paths.
                    string sampleDirectory = Path.Combine(Environment.CurrentDirectory, "Resources");
                    string samplePdfFile = Path.Combine(sampleDirectory, "Sample.pdf");
                    
                    //// Uncomment to search for the transfer paths.
                    ////IPathEnumerator pathEnumerator = client.CreatePathEnumerator(true);
                    ////PathEnumeratorContext pathEnumeratorContext =
                    ////    new PathEnumeratorContext(configuration, new[] { sampleDirectory }, targetPath);
                    ////EnumeratedPathsResult searchResults = await pathEnumerator.EnumerateAsync(
                    ////    pathEnumeratorContext,
                    ////    cancellationTokenSource.Token);
                    ////await job.AddPathsAsync(searchResults.Paths, cancellationTokenSource.Token);

                    // The transfer begins once the transfer paths are added to the job.
                    await job.AddPathsAsync(new[]
                        { new TransferPath { SourcePath = samplePdfFile }}, cancellationTokenSource.Token);

                    //// Uncomment to change the data rate at runtime.
                    //if (job.IsDataRateChangeSupported)
                    //{
                    //    job.ChangeDataRate(0, 200);
                    //}

                    // Await completion of the job up to the specified max time period. Events will continue to provide feedback.
                    ITransferResult uploadResult = await job.CompleteAsync(cancellationTokenSource.Token);
                    Console.WriteLine($"Upload transfer result: {uploadResult.Status}, Files: {uploadResult.TotalTransferredFiles}");
                    Console.WriteLine($"Upload transfer data rate: {uploadResult.TransferRateMbps:#.##} Mbps");
                    Console.WriteLine("Press ENTER to terminate.");
                }
            }
        }
    }
}