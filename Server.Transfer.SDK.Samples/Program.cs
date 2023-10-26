// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
	using Relativity.Transfer;
	using Relativity.Server.Transfer.SDK.Samples.Exceptions;

    public class Program
    {
        public static void Main(string[] args)
        {
            ClientConfigurationFactory clientConfigurationFactory = new ClientConfigurationFactory();

            Console2.Initialize();

            int exitCode = 1;

            try
            {
                //Create specific ClientConfiguration based on TransferMode in app.config
                ClientConfiguration clientConfiguration = clientConfigurationFactory.Create();

                SampleRunner sampleRunner = new SampleRunner(clientConfiguration);
                
                sampleRunner.InitializeGlobalSettings();

                Console2.WriteLine($"Relativity {sampleRunner.TransferModeName} Transfer Sample");

                Task.Run(
                    async () =>
                        {
                            // Note: the RelativityTransferLog demonstrates how to create an ITransferLog implementation for Relativity Logging.
                            using (ITransferLog transferLog = sampleRunner.CreateTransferLog())
                            using (IRelativityTransferHost host = sampleRunner.CreateRelativityTransferHost(transferLog)
                            )
                            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                            {
                                CancellationToken token = cancellationTokenSource.Token;
                                await DemoTransferAsync(host, token, sampleRunner).ConfigureAwait(false);
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
            catch (ConfigurationValueInvalidException e)
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

        private static async Task DemoTransferAsync(IRelativityTransferHost host, CancellationToken token, SampleRunner sampleRunner)
        {
            // Search for the first logical file share.
            const int LogicalFileShareNumber = 1;
            RelativityFileShare fileShare = await sampleRunner.GetFileShareAsync(host, LogicalFileShareNumber, token).ConfigureAwait(false);

            // Assigning the file share bypasses auto-configuration that will normally use the default workspace repository.
            sampleRunner.AssignFileshare(fileShare);

            // Prepare transfer setup common for upload and download.
            IList<TransferPath> localSourcePaths = await sampleRunner.SearchLocalSourcePathsAsync(token).ConfigureAwait(false);
            string uploadTargetPath = sampleRunner.GetUniqueRemoteTargetPath(fileShare);
            TransferContext context = sampleRunner.CreateTransferContext();

            using (ITransferClient client = await sampleRunner.CreateClientAsync(host, token).ConfigureAwait(false))
            using (AutoDeleteDirectory directory = new AutoDeleteDirectory())
            {

                await Upload(client, context, localSourcePaths, uploadTargetPath, sampleRunner, token);

                await Download(client, directory, context, localSourcePaths, uploadTargetPath, sampleRunner, token);
            }
        }

        private static async Task Upload(
            ITransferClient client,
            TransferContext context,
            IList<TransferPath> localSourcePaths,
            string uploadTargetPath,
            SampleRunner sampleRunner,
            CancellationToken token)
        {
            // Create a job-based upload transfer request.
            Console2.WriteStartHeader("Transfer - Upload");
            TransferRequest uploadJobRequest = TransferRequest.ForUploadJob(uploadTargetPath, context);
            uploadJobRequest.Application = "Github Sample";
            uploadJobRequest.Name = "Upload Sample";

            // Create a transfer job to upload the local sample dataset to the target remote path.
            using (ITransferJob job = await client.CreateJobAsync(uploadJobRequest, token).ConfigureAwait(false))
            {
                Console2.WriteLine("Upload started.");

                // Paths added to the async job are transferred immediately.
                await job.AddPathsAsync(localSourcePaths, token).ConfigureAwait(false);

                // Await completion of the job.
                ITransferResult result = await job.CompleteAsync(token).ConfigureAwait(false);
                Console2.WriteLine("Upload completed.");
                sampleRunner.DisplayTransferResult(result);
                Console2.WriteEndHeader();
            }
        }

        private static async Task Download(
            ITransferClient client,
            AutoDeleteDirectory directory,
            TransferContext context,
            IList<TransferPath> localSourcePaths,
            string uploadTargetPath,
            SampleRunner sampleRunner,
            CancellationToken token)
        {
            // Create a job-based download transfer request.
            Console2.WriteStartHeader("Transfer - Download");
            string downloadTargetPath = directory.Path;
            TransferRequest downloadJobRequest = TransferRequest.ForDownloadJob(downloadTargetPath, context);
            downloadJobRequest.Application = "Github Sample";
            downloadJobRequest.Name = "Download Sample";
            Console2.WriteLine("Download started.");

            // Create a transfer job to download the sample dataset to the target local path.
            using (ITransferJob job = await client.CreateJobAsync(downloadJobRequest, token).ConfigureAwait(false))
            {
                IEnumerable<TransferPath> remotePaths = localSourcePaths.Select(
                    localPath => new TransferPath
                    {
                        SourcePath = uploadTargetPath + "\\" + Path.GetFileName(localPath.SourcePath),
                        PathAttributes = TransferPathAttributes.File,
                        TargetPath = downloadTargetPath
                    });

                await job.AddPathsAsync(remotePaths, token).ConfigureAwait(false);
                await sampleRunner.ChangeDataRateAsync(job, token).ConfigureAwait(false);

                // Await completion of the job.
                ITransferResult result = await job.CompleteAsync(token).ConfigureAwait(false);
                Console2.WriteLine("Download completed.");
                sampleRunner.DisplayTransferResult(result);
                Console2.WriteEndHeader();
            }
        }
    }
}