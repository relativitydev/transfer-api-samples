// ----------------------------------------------------------------------------
// <copyright file="SampleRunner.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Relativity.DataTransfer.Nodes;
    using Relativity.DataTransfer.Nodes.PathConversion;
    using Relativity.Logging;
    using Relativity.Logging.Configuration;
    using Relativity.Logging.Factory;
	using Relativity.Transfer;
	using Relativity.Transfer.Enumeration;
    using Relativity.Server.Transfer.SDK.Samples.Enums;
    using Relativity.Server.Transfer.SDK.Samples.Exceptions;

    public class SampleRunner
    {
        private readonly string _relativityUrl = ConfigurationManager.AppSettings["RelativityUrl"];

        private readonly string _relativityUserName = ConfigurationManager.AppSettings["RelativityUserName"];

        private readonly string _relativityPassword = ConfigurationManager.AppSettings["RelativityPassword"];

        private readonly int _workspaceId = int.Parse(ConfigurationManager.AppSettings["WorkspaceId"]);

        private readonly ClientConfiguration _clientConfiguration;

        private readonly ConsolePrinter _consolePrinter = new ConsolePrinter();

        public string TransferModeName { get; }

        public SampleRunner(ClientConfiguration clientConfiguration)
        {
            _clientConfiguration = clientConfiguration;
            TransferModeName = clientConfiguration["client"].ToString();
        }

        public static TransferMode GetTransferMode()
        {
            string transferMode = ConfigurationManager.AppSettings["TransferMode"];

            if (string.Equals(transferMode, "Aspera", StringComparison.OrdinalIgnoreCase))
            {
                return TransferMode.Aspera;
            }
            else if (string.Equals(transferMode, "Fileshare", StringComparison.OrdinalIgnoreCase))
            {
                return TransferMode.Fileshare;
            }

            throw new ConfigurationValueInvalidException("Specified TransferMode is invalid");
        }

        public void InitializeGlobalSettings()
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

            Console2.WriteLine("Configured miscellaneous settings.");
            Console2.WriteEndHeader();
        }

        public void DisplayTransferResult(ITransferResult result)
        {
            this._consolePrinter.DisplayTransferResult(result);
        }

        public void AssignFileshare(RelativityFileShare fileshare)
        {
            _clientConfiguration.TargetFileShare = fileshare;
        }

        public string GetUniqueRemoteTargetPath(RelativityFileShare fileShare)
        {
            string uniqueFolder = Guid.NewGuid().ToString();
            string path = string.Join("\\", fileShare.Url.TrimEnd('\\'), "_Relativity-Transfer-Sample", uniqueFolder);
            return path;
        }

        public ITransferLog CreateTransferLog()
        {
            // This is a standard set of options for any logger.
            LoggerOptions loggerOptions = new LoggerOptions
            {
                Application = "F456D022-5F91-42A5-B00F-5609AED8C9EF",
                ConfigurationFileLocation = Path.Combine(Environment.CurrentDirectory, "LogConfig.xml"),
                System = "Data-Transfer",
                SubSystem = "Sample-Cli"
            };

            // Configure the optional SEQ sink.
            loggerOptions.AddSinkParameter(SeqSinkConfig.ServerUrlSinkParameterKey, new Uri("http://localhost:5341"));
            ILog logger = LogFactory.GetLogger(loggerOptions);
            return new RelativityTransferLog(logger, true);
        }

        public IRelativityTransferHost CreateRelativityTransferHost(ITransferLog log)
        {
            // Make sure the user actually changed the sample parameters!
            if (string.Compare(this._relativityUrl, "https://relativity_host.com", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(this._relativityUserName, "jsmith@example.com", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(this._relativityPassword, "UnbreakableP@ssword777", StringComparison.OrdinalIgnoreCase) == 0 ||
                this._workspaceId == 123456)
            {
                throw new ConfigurationValueInvalidException("You must update all Relativity connection parameters in app.config in order to run this sample.");
            }

            Uri url = this.GetInstanceUrl();
            IHttpCredential credential = new BasicAuthenticationCredential(this._relativityUserName, this._relativityPassword);
            RelativityConnectionInfo connectionInfo = new RelativityConnectionInfo(url, credential, this._workspaceId);
            return new RelativityTransferHost(connectionInfo, log);
        }

        public TransferContext CreateTransferContext()
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
                        "Event=TransferStatistics, Progress: {0:00.00}%, Transfer rate: {1:00.00} Mbps, Remaining: {2:hh\\:mm\\:ss}",
                        args.Statistics.Progress,
                        args.Statistics.TransferRateMbps,
                        args.Statistics.RemainingTime);
                };

            return context;
        }

        public async Task<ITransferClient> CreateClientAsync(IRelativityTransferHost host, CancellationToken token)
        {
            Console2.WriteStartHeader("Create Client");
            ITransferClient client;
            if (this._clientConfiguration.Client == WellKnownTransferClient.Unassigned)
            {
                // The CreateClientAsync method chooses the best client at runtime.
                Console2.WriteLine("TAPI is choosing the best transfer client...");
                client = await host.CreateClientAsync(this._clientConfiguration, token).ConfigureAwait(false);
            }
            else
            {
                // The CreateClient method creates the specified client.
                Console2.WriteLine("The API caller specified the {0} transfer client.", this._clientConfiguration.Client);
                client = host.CreateClient(this._clientConfiguration);
            }

            if (client == null)
            {
                throw new InvalidOperationException("This operation cannot be performed because a transfer client could not be created.");
            }

            Console2.WriteLine("TAPI created the {0} transfer client.", client.DisplayName);
            Console2.WriteEndHeader();
            return client;
        }

        public async Task<RelativityFileShare> GetFileShareAsync(IRelativityTransferHost host, int number, CancellationToken token)
        {
            Console2.WriteStartHeader("Get Specified file share");
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

            this._consolePrinter.DisplayFileShare(fileShare);
            Console2.WriteEndHeader();
            return fileShare;
        }

        public async Task<IList<TransferPath>> SearchLocalSourcePathsAsync(CancellationToken token)
        {
            Console2.WriteStartHeader("Search Paths");
            string searchLocalPath = Path.Combine(Environment.CurrentDirectory, "Resources");

            var paths = new List<TransferPath>();
            long totalFileCount = 0;
            long totalByteCount = 0;

            var logger = this.CreateTransferLog();

            var sourceNode = NodeParser.Node()
                .WithContext(NullNodeContext.Instance)
                .WithPath(searchLocalPath)
                .Parse<IDirectory>();

            INode[] sourceNodes = { sourceNode };
            var pathEnumerator = EnumerationBuilder.ForUpload(logger, Guid.NewGuid())
                .StartFrom(sourceNodes)
                .WithStatistics(new SynchronousHandler<EnumerationStatistic>(
                    statistic =>
                        {
                            totalFileCount = statistic.TotalFiles;
                            totalByteCount = statistic.TotalBytes;
                        }))
                .Create();


            var stopWatch = new Stopwatch();
            stopWatch.Start();
            await Task.Run(() =>
                {
                    paths.AddRange(pathEnumerator.LazyEnumerate(token)
                        .Select(node => new TransferPath(node.AbsolutePath)));
                }, token).ConfigureAwait(false);

            stopWatch.Stop();

            this._consolePrinter.DisplaySearchSummary(sourceNode, stopWatch, totalFileCount, totalByteCount);
            return paths;
        }

        public async Task ChangeDataRateAsync(ITransferJob job, CancellationToken token)
        {
            if (job.IsDataRateChangeSupported)
            {
                Console2.WriteLine("Changing the transfer data rate...");
                await job.ChangeDataRateAsync(0, 10, token).ConfigureAwait(false);
                Console2.WriteLine("Changed the transfer data rate.");
            }
        }

        private Uri GetInstanceUrl()
        {
            var urlTmp = new Uri(this._relativityUrl);
            var uriString = urlTmp.GetLeftPart(UriPartial.Authority);
            var url = new Uri(uriString);

            return url;
        }
    }
}