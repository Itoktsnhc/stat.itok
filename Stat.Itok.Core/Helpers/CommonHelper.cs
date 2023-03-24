using System.Dynamic;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NeoSmart.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stat.Itok.Core.Helpers
{
    public static class StatInkHelper
    {
        private static readonly Random _random = new Random();

        public static NinMiscConfig ParseNinWebViewData(string str)
        {
            var webViewData = new NinMiscConfig();

            var versionMatchRes = Regex.Match(str,
                "=.(?<revision>[0-9a-f]{40}).*revision_info_not_set.*=.(?<version>\\d+\\.\\d+\\.\\d+)-");
            if (versionMatchRes.Groups.Count < 3) return null;
            var versionRange = versionMatchRes.Groups["version"];

            var revisionRange = versionMatchRes.Groups["revision"];
            var revision = str.Substring(revisionRange.Index, 8);
            webViewData.WebViewVersion = $"{versionRange.Value}-{revision}";
            var graphQLMatchRes = Regex.Matches(str,
                "params:\\{id:.(?<id>[0-9a-f]{32}).,metadata:\\{\\},name:.(?<name>[a-zA-Z0-9_]+).,");

            foreach (Match match in graphQLMatchRes)
            {
                if (!match.Success || match.Groups.Count < 3)
                {
                    continue;
                }

                webViewData.GraphQL.APIs[match.Groups["name"].Value] = match.Groups["id"].Value;
            }

            return webViewData;
        }

        public static string BuildRandomSizedBased64Str(int size)
        {
            var arr = new byte[size];
            _random.NextBytes(arr);
            return UrlBase64.Encode(arr);
        }

        public static void CorrectUserInfoLang(this JobConfig jobConfig)
        {
            if (string.IsNullOrEmpty(jobConfig.ForcedUserLang))
            {
                jobConfig.ForcedUserLang = jobConfig.NinAuthContext.UserInfo.Lang;
            }

            jobConfig.NinAuthContext.UserInfo.Lang = jobConfig.ForcedUserLang;
        }

        public static JToken ThrowIfJsonPropNotFound(this string json, params string[] propNames)
        {
            JToken jToken;
            try
            {
                jToken = JToken.Parse(json);
            }
            catch (Exception e)
            {
                throw new Exception($"parse json Error, rawJson is: {json}", e);
            }

            foreach (var propName in propNames)
            {
                if (jToken[propName] == null || jToken[propName].Type == JTokenType.Null)
                {
                    throw new Exception($"[{propName}] is null, rawJson is: {json}");
                }
            }

            return jToken;
        }

        public static JToken ThrowIfJsonPropChainNotFound(this string json, string[] propNames)
        {
            JToken jToken;
            try
            {
                jToken = JToken.Parse(json);
            }
            catch (Exception e)
            {
                throw new Exception($"parse json Error, rawJson is: {json}", e);
            }

            var curToken = jToken;
            foreach (var propName in propNames)
            {
                if (curToken[propName] == null || curToken[propName].Type == JTokenType.Null)
                {
                    throw new Exception($"[{propName}] is null, rawJson is: {json}");
                }

                curToken = curToken[propName];
            }

            return jToken;
        }

        public static string FirstCharToLower(this string input) =>
            input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => string.Concat(input[0].ToString().ToLower(), input.AsSpan(1))
            };


        public static string BuildGraphQLBody(string queryHash, string name = null, string value = null)
        {
            dynamic body = new ExpandoObject();
            dynamic extensions = new ExpandoObject();
            dynamic persistedQuery = new ExpandoObject();
            dynamic variables = new ExpandoObject();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                ((IDictionary<string, object>) variables)[name] = value;
            persistedQuery.sha256Hash = queryHash;
            persistedQuery.version = 1;
            extensions.persistedQuery = persistedQuery;
            body.extensions = extensions;
            body.variables = variables;
            return JsonConvert.SerializeObject(body);
        }
    }

    public static class CommonHelper
    {
        public static string CompressStr(string input, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(input);
            return Convert.ToBase64String(CompressBytes(bytes));
        }

        public static string DecompressStr(string input, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = Convert.FromBase64String(input);
            return encoding.GetString(DecompressBytes(bytes));
        }

        public static byte[] CompressBytes(byte[] bytes)
        {
            using var outputStream = new MemoryStream();
            using (var compressStream = new BrotliStream(outputStream, CompressionLevel.SmallestSize))
            {
                compressStream.Write(bytes, 0, bytes.Length);
            }

            return outputStream.ToArray();
        }

        public static byte[] DecompressBytes(byte[] bytes)
        {
            using var inputStream = new MemoryStream(bytes);
            using var outputStream = new MemoryStream();
            using (var decompressStream = new BrotliStream(inputStream, CompressionMode.Decompress))
            {
                decompressStream.CopyTo(outputStream);
            }

            return outputStream.ToArray();
        }
    }
}

namespace Utility
{
    /// <summary>
    /// Helper methods for working with <see cref="Guid"/>. https://gist.github.com/ChrisMcKee/599264d776878bea8a611493b5e28143
    /// </summary>
    public static class GuidUtility
    {
        /// <summary>
        /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
        /// </summary>
        /// <param name="namespaceId">The ID of the namespace.</param>
        /// <param name="name">The name (within that namespace).</param>
        /// <returns>A UUID derived from the namespace and name.</returns>
        /// <remarks>See <a href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating a deterministic GUID</a>.</remarks>
        public static Guid Create(Guid namespaceId, string name)
        {
            return GuidUtility.Create(namespaceId, name, 5);
        }

        /// <summary>
        /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
        /// </summary>
        /// <param name="namespaceId">The ID of the namespace.</param>
        /// <param name="name">The name (within that namespace).</param>
        /// <param name="version">The version number of the UUID to create; this value must be either
        /// 3 (for MD5 hashing) or 5 (for SHA-1 hashing) or 6 (for SHA-256 hashing).</param>
        /// <returns>A UUID derived from the namespace and name.</returns>
        /// <remarks>See <a href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating a deterministic GUID</a>.</remarks>
        public static Guid Create(Guid namespaceId, string name, int version)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (version != 3 && version != 5 && version != 6)
                throw new ArgumentOutOfRangeException("version",
                    "version must be either 3 (md5) or 5 (sha1), or 6 (sha256).");

            // convert the name to a sequence of octets (as defined by the standard or conventions of its namespace) (step 3)
            // ASSUME: UTF-8 encoding is always appropriate
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);

            // convert the namespace UUID to network order (step 3)
            byte[] namespaceBytes = namespaceId.ToByteArray();
            GuidUtility.SwapByteOrder(namespaceBytes);

            // comput the hash of the name space ID concatenated with the name (step 4)
            byte[] hash;
            using (var incrementalHash = version == 3 ? IncrementalHash.CreateHash(HashAlgorithmName.MD5) :
                   version == 5 ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1) :
                   IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                incrementalHash.AppendData(namespaceBytes);
                incrementalHash.AppendData(nameBytes);
                hash = incrementalHash.GetHashAndReset();
                /*algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                hash = algorithm.Hash;*/ //todo verify correctness;
            }

            // most bytes from the hash are copied straight to the bytes of the new GUID (steps 5-7, 9, 11-12)
            byte[] newGuid = new byte[16];
            Array.Copy(hash, 0, newGuid, 0, 16);

            // set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
            newGuid[6] = (byte) ((newGuid[6] & 0x0F) | (version << 4));

            // set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively (step 10)
            newGuid[8] = (byte) ((newGuid[8] & 0x3F) | 0x80);

            // convert the resulting UUID to local byte order (step 13)
            GuidUtility.SwapByteOrder(newGuid);
            return new Guid(newGuid);
        }

        /// <summary>
        /// The namespace for fully-qualified domain names (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>
        /// The namespace for URLs (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>
        /// The namespace for ISO OIDs (from RFC 4122, Appendix C).
        /// </summary>
        public static readonly Guid IsoOidNamespace = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

        // Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
        internal static void SwapByteOrder(byte[] guid)
        {
            GuidUtility.SwapBytes(guid, 0, 3);
            GuidUtility.SwapBytes(guid, 1, 2);
            GuidUtility.SwapBytes(guid, 4, 5);
            GuidUtility.SwapBytes(guid, 6, 7);
        }

        private static void SwapBytes(byte[] guid, int left, int right)
        {
            (guid[left], guid[right]) = (guid[right], guid[left]);
        }
    }
}