// ----------------------------------------------------------------------------
// <copyright file="ConsolePrinter.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System;
    using System.Diagnostics;

    using Relativity.DataTransfer.Nodes;
	using Relativity.Transfer;

	public class ConsolePrinter
    {
        public void DisplayFileShare(RelativityFileShare fileShare)
        {
            Console2.WriteLine("Artifact ID: {0}", fileShare.ArtifactId);
            Console2.WriteLine("ModeName: {0}", fileShare.Name);
            Console2.WriteLine("UNC Path: {0}", fileShare.Url);
            Console2.WriteLine("Cloud Instance: {0}", fileShare.CloudInstance);

            // RelativityOne specific properties.
            Console2.WriteLine("Number: {0}", fileShare.Number);
            Console2.WriteLine("Tenant ID: {0}", fileShare.TenantId);
        }

        public void DisplayTransferResult(ITransferResult result)
        {
            // The original request can be accessed within the transfer result.
            Console2.WriteLine();
            Console2.WriteLine("Transfer Summary");
            Console2.WriteLine("ModeName: {0}", result.Request.Name);
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

            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", result.Elapsed);
            Console2.WriteLine("Total files: Files: {0:n0}", result.TotalTransferredFiles);
            Console2.WriteLine("Total bytes: Files: {0:n0}", result.TotalTransferredBytes);
            Console2.WriteLine("Total files not found: {0:n0}", result.TotalFilesNotFound);
            Console2.WriteLine("Total bad path errors: {0:n0}", result.TotalBadPathErrors);
            Console2.WriteLine("Data rate: {0:#.##} Mbps", result.TransferRateMbps);
            Console2.WriteLine("Retry count: {0}", result.RetryCount);
        }

        public void DisplaySearchSummary(
            IDirectory sourceNode,
            Stopwatch stopWatch,
            long totalFileCount,
            long totalByteCount)
        {
            Console2.WriteLine("Local Path: {0}", sourceNode.AbsolutePath);
            Console2.WriteLine("Elapsed time: {0:hh\\:mm\\:ss}", stopWatch.Elapsed);
            Console2.WriteLine("Total files: {0:n0}", totalFileCount);
            Console2.WriteLine("Total bytes: {0:n0}", totalByteCount);
            Console2.WriteEndHeader();
        }
    }
}