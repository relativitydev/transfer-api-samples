// ----------------------------------------------------------------------------
// <copyright file="CustomTransferLog.cs" company="kCura Corp">
//   Relativity ODA LLC (C) 2017 All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer
{
    using System;

    using Serilog;
    using Serilog.Enrichers;

    /// <summary>
    /// Represents a thread-safe class object to write debug, information, warning, and error logs using Serilog.
    /// </summary>
    /// <remarks>
    /// This is an alternative implementation of Relativity Logging <see cref="ITransferLog"/> and can be used in client-side scenarios.
    /// </remarks>
    public class CustomTransferLog : ITransferLog
    {
        /// <summary>
        /// The Serilog logger backing.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// The flag that dictates whether the logger should be disposed.
        /// </summary>
        private readonly bool disposeLogger;

        /// <summary>
        /// The disposed backing.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomTransferLog"/> class.
        /// </summary>
        public CustomTransferLog()
        {
            this.logger = CreateLogger();
            this.disposeLogger = true;
            this.IsEnabled = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomTransferLog"/> class.
        /// </summary>
        /// <param name="logger">
        /// The logger.
        /// </param>
        /// <param name="disposeLogger">
        /// Specify whether this instance should dispose the logger when the <see cref="Dispose()"/> method is called.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public CustomTransferLog(ILogger logger, bool disposeLogger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            this.logger = logger;
            this.disposeLogger = disposeLogger;
            this.IsEnabled = true;
        }

        /// <inheritdoc />
        public bool IsEnabled
        {
            get;
            set;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void LogInformation(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Information(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogInformation(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Information(exception, messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogDebug(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Debug(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogDebug(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Debug(exception, messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogWarning(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Warning(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogWarning(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Warning(exception, messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogError(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Error(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Error(exception, messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogVerbose(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Verbose(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogVerbose(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Verbose(exception, messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogFatal(string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Fatal(messageTemplate, propertyValues);
        }

        /// <inheritdoc />
        public void LogFatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.logger.Fatal(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Creates the logger.
        /// </summary>
        /// <returns>
        /// The <see cref="ILogger"/> instance.
        /// </returns>
        private static ILogger CreateLogger()
        {
            var configuration = new LoggerConfiguration().MinimumLevel.Debug()
                .Enrich.WithProperty("App", GlobalSettings.Instance.ApplicationName)
                .Enrich.With(
                    new MachineNameEnricher(),
                    new ProcessIdEnricher(),
                    new ThreadIdEnricher());
            configuration.MinimumLevel.Debug();

            // Provide a custom logfile here.
            //// configuration = configuration.WriteTo.RollingFile(settings.LogFile);
            configuration = configuration.WriteTo.Console();
            configuration = configuration.WriteTo.Trace();
            return configuration.CreateLogger();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        private void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing && this.disposeLogger)
            {
                var disposableLogger = this.logger as IDisposable;
                disposableLogger?.Dispose();
            }

            this.disposed = true;
        }
    }
}