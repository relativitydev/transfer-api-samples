// ----------------------------------------------------------------------------
// <copyright file="ConfigurationValueInvalidException.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer.Sample.Exceptions
{
    using System;

    public class ConfigurationValueInvalidException: Exception
    {
        public ConfigurationValueInvalidException()
        {
        }

        public ConfigurationValueInvalidException(string message)
            : base(message)
        {
        }

        public ConfigurationValueInvalidException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}