﻿using System.Reflection;

namespace SampWeb
{
    /// <summary>
    /// Constants class.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Field DirListing.
        /// </summary>
        public const string DirListing = "Directory Listing -- {0}";

        /// <summary>
        /// Field HTTPError.
        /// </summary>
        public const string HTTPError = "HTTP Error {0} - {1}.";

        /// <summary>
        /// Field ServerError.
        /// </summary>
        public const string ServerError = "Server Error in '{0}' Application.";

        /// <summary>
        /// Field UnhandledException.
        /// </summary>
        public const string UnhandledException = "An unhandled {0} exception occurred while processing the request.";

        /// <summary>
        /// Field VersionInfo.
        /// </summary>
        public const string VersionInfo = "Version Information";

        /// <summary>
        /// Field VWDName.
        /// </summary>
        public const string VWDName = RuntimeVars.CopyingRight;

        /// <summary>
        /// Field VersionString.
        /// </summary>
        public static readonly string VersionString = RuntimeVars.ServerVersion;
    }
}