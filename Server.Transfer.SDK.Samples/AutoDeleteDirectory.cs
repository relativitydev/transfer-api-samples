// ----------------------------------------------------------------------------
// <copyright file="AutoDeleteDirectory.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>
    /// Represents a class object that creates a sub-directory and automatically deletes it through the <see cref="Dispose"/> method.
    /// </summary>
    public class AutoDeleteDirectory : IDisposable
    {
        /// <summary>
        /// The disposed backing.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDeleteDirectory" /> class.
        /// </summary>
        public AutoDeleteDirectory()
        {
            string downloadUniqueFolder = string.Format(CultureInfo.InvariantCulture, "Downloads-{0:MM-dd-yyyy-hh-mm-ss}", DateTime.Now);
            this.Path = System.IO.Path.Combine(Environment.CurrentDirectory, downloadUniqueFolder);
            Directory.CreateDirectory(this.Path);
        }

        /// <summary>
        /// Gets the directory path.
        /// </summary>
        /// <value>
        /// The full path.
        /// </value>
        public string Path
        {
            get;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
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

            if (disposing)
            {
                if (!string.IsNullOrEmpty(this.Path) && Directory.Exists(this.Path))
                {
                    try
                    {
                        string[] files = Directory.GetFiles(this.Path, "*", SearchOption.AllDirectories);
                        foreach (string file in files)
                        {
                            FileAttributes attributes = File.GetAttributes(file);
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                            File.Delete(file);
                        }

                        Directory.Delete(this.Path, true);
                    }
                    catch (IOException e)
                    {
                        Console2.WriteLine(
                            ConsoleColor.Red,
                            $"Failed to tear down the '{this.Path}' temp directory due to an I/O issue. Exception: "
                            + e);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Console2.WriteLine(
                            ConsoleColor.Red,
                            $"Failed to tear down the '{this.Path}' temp directory due to unauthorized access. Exception: "
                            + e);
                    }
                }
            }

            this.disposed = true;
        }
    }
}