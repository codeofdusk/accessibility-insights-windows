﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace AccessibilityInsights.SetupLibrary
{
    /// <summary>
    /// Utilities to help fetch EnrichedChannelInfo objects
    /// </summary>
    public static class ChannelInfoUtilities
    {
        /// <summary>
        /// Given a stream containing a config file, get a specific channel
        /// </summary>
        /// <param name="stream">The stream containing the config file</param>
        /// <returns>The valid EnrichedChannelInfo</returns>
        public static EnrichedChannelInfo GetChannelFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            EnrichedChannelInfo info = GetChannelInfoFromSignedManifest(stream);

#pragma warning disable CA1303 // Do not pass literals as localized parameters
            return info ?? throw new InvalidDataException("Unable to get ChannelInfo");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
        }

        private static bool IsStreamTrusted(Stream stream)
        {
            using (TrustVerifier verifier = new TrustVerifier(stream))
            {
                return verifier.IsVerified;
            }
        }

        internal static EnrichedChannelInfo GetChannelInfoFromSignedManifest(Stream stream, Func<Stream, bool> streamTrustCheck = null)
        {
            Func<Stream, bool> trustCheck = streamTrustCheck ?? IsStreamTrusted;

            if (!trustCheck(stream))
            {
                return null; // TODO: Capture this case in telemetry when we deprecate unsigned manifests
            }

            stream.Position = 0;
            byte[] bytes = FileHelpers.ExtractResourceFromStream(stream, "AccessibilityInsights.Manifest.ReleaseInfo.json");
            string json = StringFromResourceByteArray(bytes);
            EnrichedChannelInfo info = JsonConvert.DeserializeObject<EnrichedChannelInfo>(json);
            return info;
        }

        private static string StringFromResourceByteArray(byte[] bytes)
        {
            // Skip the Byte Order Mark when deserializing, since it's invalid JSON
            int index = (bytes[0] == 0xFF & bytes[1] == 0xFE) ? 2 : 0;
            return Encoding.Unicode.GetString(bytes, index, bytes.Length - index);
        }

        /// <summary>
        /// Given a ReleaseChannel and a keyName, attempt to load the corresponding EnrichedChannelInfo object
        /// </summary>
        /// <param name="releaseChannel">The ReleaseChannel being queried</param>
        /// <param name="enrichedChannelInfo">Returns the EnrichedChannelInfo here</param>
        /// <param name="gitHubWrapper">An optional wrapper to the GitHub data</param>
        /// <param name="exceptionReporter">An optional IExceptionReporter if you want exception details</param>
        /// <returns>true if we found data</returns>
        public static bool TryGetChannelInfo(ReleaseChannel releaseChannel, out EnrichedChannelInfo enrichedChannelInfo, IGitHubWrapper gitHubWrapper, IExceptionReporter exceptionReporter = null)
        {
            try
            {
                IGitHubWrapper wrapper = gitHubWrapper ?? new GitHubWrapper(exceptionReporter);
                using (Stream stream = new MemoryStream())
                {
                    StreamMetadata streamMetadata = wrapper.LoadChannelInfoIntoStream(releaseChannel, stream);
                    enrichedChannelInfo = GetChannelFromStream(stream);
                    enrichedChannelInfo.Metadata = streamMetadata;

                    enrichedChannelInfo.MinimumVersion = 
                        releaseChannel == ReleaseChannel.Production ?
                        enrichedChannelInfo.ProductionMinimumVersion :
                        enrichedChannelInfo.CurrentVersion;

                    return true;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
            {
                exceptionReporter?.ReportException(e);
            }
#pragma warning restore CA1031 // Do not catch general exception types

            // Default values
            enrichedChannelInfo = null;
            return false;
        }
    }
}
