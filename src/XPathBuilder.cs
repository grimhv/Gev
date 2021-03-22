using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Honeypox.Gev
{
    class XPathBuilder
    {
        /// <summary>
        /// Returns an array of Providers stored in a local file.
        /// </summary>
        /// <param name="filePath">Path to the plaintext file with list of Windows Providers.</param>
        private static string[] GetProvidersFromFile(string filePath)
        {
            return File.ReadAllLines(filePath);
        }

        /// <summary>
        /// Returns a list of Providers based on user input.
        /// </summary>
        /// <param name="sourceList"></param>
        public static List<string> ParseProviders(List<string> sourceList)
        {
            // get an array of providers from our text file
            string[] listOfProviders = GetProvidersFromFile(@".\event_providers.txt");
            List<string> providerXPath = new List<string>();

            /* Use LINQ to select providers that contain case insensitive substring of the source strings
             * inputted by the user
             * 
             * For instance, if user input is `--source ntfs`, then this should create a list with:
             *     Microsoft-Windows-Ntfs
             *     Microsoft-Windows-Ntfs-UBPM
             *     Ntfs
             *     Ntfs_NtfsLog
             * 
             * We also add the exact text the user inputted just in case it doesn't exist inside
             * event_providers.txt
             */
            sourceList.ForEach(source =>
            {
                providerXPath.Add(source);

                listOfProviders.Where(x => x.IndexOf(
                    source.Trim(), 0, StringComparison.OrdinalIgnoreCase) >= 0).ToList().ForEach(y =>
                        providerXPath.Add(y)
                    );
            });

            return providerXPath;
        }

        /// <summary>
        /// Prepares a list to be converted into an XPath query.
        /// </summary>
        /// <param name="list">List to be aggregated into an XPath query.</param>
        /// <param name="prefix">Opening characters.</param>
        /// <param name="element">Element name.</param>
        /// <param name="suffix">Closing characters.</param>
        private static string PrepareXPathStringFromList(List<int> list, string prefix, string element, string suffix)
        {
            // Converts the List<int> to a List<string>, then renders the XPath query
            return RenderXPathFromList(list.ConvertAll(x => x.ToString()), prefix, element, suffix);
        }

        /// <summary>
        /// Prepares a list to be converted into an XPath query.
        /// </summary>
        /// <param name="list">List to be aggregated into an XPath query.</param>
        /// <param name="prefix">Opening characters.</param>
        /// <param name="element">Element name.</param>
        /// <param name="suffix">Closing characters.</param>
        /// <param name="quote">Whether to put single quotes around the element's value.</param>
        private static string PrepareXPathStringFromList(List<string> list, string prefix, string element, string suffix, bool quote = false)
        {
            // Renders the XPath query
            return RenderXPathFromList(list, prefix, element, suffix, quote);
        }

        /// <summary>
        /// Creates an XPath query based on parameters.
        /// </summary>
        /// <param name="list">List to be aggregated into an XPath query.</param>
        /// <param name="prefix">Opening characters.</param>
        /// <param name="element">Element name.</param>
        /// <param name="suffix">Closing characters.</param>
        /// <param name="quote">Whether to put single quotes around the element's value.</param>
        /// <returns>An XPath string.</returns>
        private static string RenderXPathFromList(List<string> list, string prefix, string element, string suffix, bool quote = false)
        {
            string delimiter = " or ";
            string equal = "=";
            if (quote)
            {
                delimiter = $"'{delimiter}";
                equal = $"{equal}'";
                element = $"@{element}";
                suffix = $"'{suffix}";
            }

            return $"{prefix}{element}{equal}{list.Aggregate((a, b) => a + delimiter + element + equal + b)}{suffix}";
        }

        /// <summary>
        /// Returns a single XPath query string from the Events, Levels, and Sources found in a LogQuery object.
        /// </summary>
        /// <param name="log">The LogQuery object to be parsed.</param>
        /// <returns></returns>
        public static string BuildXPathQuery(GevLog.LogQuery log)
        {
            string queryStringPrefix = "";
            string queryStringSuffix = "";
            string queryStringSources = "";
            string queryStringIds = "";
            string queryStringLevels = "";

            if (log.LevelFilter.Count > 0 || log.IdFilter.Count > 0 || log.SourceFilter.Count > 0)
            {
                queryStringPrefix = "*[System[";
                queryStringSuffix = "]]";
            }
            else
            {
                return "*";
            }

            if (log.SourceFilter.Count > 0)
            {
                queryStringSources = PrepareXPathStringFromList(ParseProviders(log.SourceFilter), "Provider[", "Name", "]", true);
            }

            if (log.IdFilter.Count > 0)
            {
                queryStringIds = PrepareXPathStringFromList(log.IdFilter, "(", "EventID", ")");
            }

            if (log.LevelFilter.Count > 0)
            {
                queryStringLevels = PrepareXPathStringFromList(log.LevelFilter, "(", "Level", ")");
            }

            List<string> queryStringList = new List<string>();
            
            if (queryStringSources.Length > 0)
            {
                queryStringList.Add(queryStringSources);
            }
            if (queryStringIds.Length > 0)
            {
                queryStringList.Add(queryStringIds);
            }
            if (queryStringLevels.Length > 0)
            {
                queryStringList.Add(queryStringLevels);
            }

            // return the final string, when all is said and done it will look something like:
            // *[System[Provider[@Name='Microsoft-Windows-Ntfs' or @Name='Microsoft-Windows-Ntfs-UBPM' or @Name='Ntfs'] and (EventID=1 or EventID=2 or EventID=3) and (Level=1 or Level=2)]]
            return queryStringPrefix + queryStringList.Aggregate((a, b) => a + " and " + b) + queryStringSuffix;
        }
    }
}