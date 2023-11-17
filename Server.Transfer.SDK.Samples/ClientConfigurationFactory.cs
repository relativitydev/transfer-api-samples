// ----------------------------------------------------------------------------
// <copyright file="ClientConfigurationFactory.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System.ComponentModel;
	using Relativity.Transfer;
	using Relativity.Transfer.Aspera;
    using Relativity.Transfer.FileShare;
    using Relativity.Server.Transfer.SDK.Samples.Enums;

    public class ClientConfigurationFactory
    {
        public ClientConfiguration Create()
        {
            TransferMode transferMode = SampleRunner.GetTransferMode();

            switch (transferMode)
            {
                case TransferMode.Aspera:
                    return new AsperaClientConfiguration
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
                case TransferMode.Fileshare:
                    return new FileShareClientConfiguration()
                    {
                        // Common properties
                        BadPathErrorsRetry = false,
                        FileNotFoundErrorsRetry = false,
                        MaxHttpRetryAttempts = 2,
                        PreserveDates = true,
                        TargetDataRateMbps = 5,
                    };
                default:
                    throw new InvalidEnumArgumentException("Specified TransferMode enum value is invalid");
            }
        }
    }
}