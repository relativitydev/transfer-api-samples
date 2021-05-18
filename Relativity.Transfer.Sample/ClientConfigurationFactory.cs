// ----------------------------------------------------------------------------
// <copyright file="ClientConfigurationFactory.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer.Sample
{
    using System.ComponentModel;

    using Relativity.Transfer.Aspera;
    using Relativity.Transfer.FileShare;
    using Relativity.Transfer.Sample.Enums;

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