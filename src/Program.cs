using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Serialization;

namespace Honeypox.Gev
{
    internal static class Globals
    {
        /// <summary> Maximum number of events to output. </summary>
        public static int MaxEvents { get; set; } = 1000;

        /// <summary> Total records found </summary>
        public static int TotalRecords { get; set; } = 0;

    }

    internal class Gev
    {
        private static void Main(string[] args)
        {
            // Start the System.Diagnostics.Stopwatch to see how long the program takes to run
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Instantiate a new object that holds query information
            GevLog.LogQuery log = new GevLog.LogQuery() { LogPath = "" };

            // Holds a list of all the records we have found
            var eventLogRecords = new List<GevLog.Record>();

            // If user has not supplied any arguments, we'll display the help text
            if (args.Length == 0)
            {
                DisplayHelp();
            }
            else
            {
                // parse arguments
                log = ParseArguments(args, log);
            }

            // DEBUG MODE: this is just to ensure that all the parameters are correct
            if (log.DebugMode == true)
            {
                DisplayArguments(log);
            }

            // Since we have parsed arguments, check to see if they supplied a path
            if (log.LogPath.Length < 1)
            {
                if (log.QuerySet)
                {
                    AbortGev("Query flag was found, but no path was given.  Please set the path before the query flag.");
                }
                else
                {
                    AbortGev("No path to archived log found.");
                }
            }

            // If we have any Levels or IDs we'll need a prefix and suffix, otherwise the queryString should just be '*'
            string queryString = XPathBuilder.BuildXPathQuery(log);

            // Get our records that match our XPath query
            EventRecords(LogRecordCollection(log.LogPath, log.Direction, queryString), eventLogRecords);

            // If there were no records found, we'll alert the user
            if (Globals.TotalRecords < 1)
                Console.WriteLine("No records found matching search criteria.");
            else
            {
                OutputRecords(eventLogRecords, log);

                // Metrics
                stopWatch.Stop();
                Console.WriteLine("Program run in " + stopWatch.ElapsedMilliseconds.ToString() +
                    "ms with " + Globals.TotalRecords + " records found.");
            }
        }

        /// <summary>
        /// Parses and formats an EventLogRecord.
        /// </summary>
        /// <param name="record">Record to be parsed.</param>
        private static List<GevLog.Record> EventRecords(IEnumerable<EventLogRecord> record, List<GevLog.Record> eventLogRecords)
        {
            string resultId;
            string resultLevelDisplayName;
            string resultLevel;
            string resultTimeCreated;
            string resultProviderName;
            string resultFormatDescription;

            /* Iterate through each event log record found and determine:
             *                resultId = event ID
             *  resultLevelDisplayName = level, i.e. "warning", "error", etc
             *       resultTimeCreated = event date
             *      resultProviderName = event source
             * resultFormatDescription = event description
             *
             * If any of the properties are 'null', it will throw an EventLogNotFoundException
             * In that case, we'll catch it and just pretend the property is some generic identifier
             * such as "0", "null", "unknown", etc depending on the property
             */
            foreach (EventLogRecord i in record)
            {
                try
                {
                    resultId = i.Id.ToString();
                }
                catch (EventLogNotFoundException)
                {
                    resultId = "0";
                }
                try
                {
                    resultLevel = i.Level.ToString();
                }
                catch (EventLogNotFoundException)
                {
                    resultLevel = "0";
                }
                try
                {
                    // Sometimes the LevelDisplyName node is missing, so we'll build the string from the Level node
                    if (string.IsNullOrEmpty(i.LevelDisplayName))
                    {
                        switch (resultLevel)
                        {
                            case "1":
                                resultLevelDisplayName = "Critical";
                                break;

                            case "2":
                                resultLevelDisplayName = "Error";
                                break;

                            case "3":
                                resultLevelDisplayName = "Warning";
                                break;

                            case "4":
                                resultLevelDisplayName = "Information";
                                break;

                            case "5":
                                resultLevelDisplayName = "Verbose";
                                break;

                            default:
                                resultLevelDisplayName = "Other";
                                break;
                        }
                    }
                    else
                    {
                        resultLevelDisplayName = i.LevelDisplayName;
                    }
                }
                catch (EventLogNotFoundException)
                {
                    resultLevelDisplayName = "Other";
                }
                try
                {
                    resultTimeCreated = i.TimeCreated.ToString();
                }
                catch (EventLogNotFoundException)
                {
                    resultTimeCreated = "Unknown";
                }
                try
                {
                    resultProviderName = i.ProviderName;
                }
                catch (EventLogNotFoundException)
                {
                    resultProviderName = "Provider not found";
                }
                try
                {
                    // If FormatDecription is empty or missing, the Event Viewer will show this message
                    // so I added it here for consistency
                    if (string.IsNullOrEmpty(i.FormatDescription()))
                        resultFormatDescription = string.Format("The description for Event ID {0} from source {1} cannot be found. " +
                            "Either the component that raises this event is not installed on your local computer or " +
                            "the installation is corrupt.  You can install or repair the component on the local computer." +
                            "\n\nIf the event originated on another computer, the display information had to be saved with " +
                            "the event.", resultId, resultProviderName);
                    else
                        resultFormatDescription = i.FormatDescription().ToString();
                }
                catch (EventLogNotFoundException)
                {
                    resultFormatDescription = "No message found.";
                }

                // Add our parsed EventLogRecord to our EventLogRecords object
                eventLogRecords.Add(new GevLog.Record(
                    (Globals.TotalRecords - 1), // i = (count - 1) since index starts at 0 but count starts at 1
                    resultId,
                    resultLevelDisplayName,
                    Convert.ToInt32(resultLevel),
                    resultTimeCreated,
                    resultProviderName,
                    resultFormatDescription));
            }
            return eventLogRecords;
        }

        /// <summary>
        /// Queries a .evtx file
        /// </summary>
        /// <param name="filename">Path to .evtx</param>
        /// <param name="xPathQueryString">XPath query</param>
        /// <param name="direction">Time ascending (1) or descending (2)</param>
        /// <returns>Event log records matching XPath query, one at a time</returns>
        private static IEnumerable<EventLogRecord> LogRecordCollection(string filename, int direction, string xPathQuery)
        {
            // Create an EventLogQuery given the file and XPath query
            EventLogQuery eventLogQuery = new EventLogQuery(filename, PathType.FilePath, xPathQuery);

            // Check for ascending/descending
            if (direction == 1)
            {
                eventLogQuery.ReverseDirection = false;
            }
            else if (direction == 2)
            {
                eventLogQuery.ReverseDirection = true;
            }

            // Read through events and return records that aren't null
            using (EventLogReader eventLogReader = new EventLogReader(eventLogQuery))
            {
                EventLogRecord eventLogRecord;

                while ((eventLogRecord = (EventLogRecord)eventLogReader.ReadEvent()) != null)
                {
                    // We keep track of the total records so we can stop when we've reached the requested maximum
                    if (Globals.TotalRecords < Globals.MaxEvents)
                    {
                        Globals.TotalRecords++;
                        yield return eventLogRecord;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Outputs event log records
        /// </summary>
        /// <param name="record">Event log record to output</param>
        /// <param name="outFile">File to output it to</param>
        /// <param name="format">Type of output -> 1 = XML - 2 = HTML - 3 = TEXT</param>
        /// <returns>Number of records found so far</returns>
        private static void OutputRecords(List<GevLog.Record> eventLogRecords, GevLog.LogQuery log)
        {
            string outString = "";

            /* JsonSerializer and XmlSerializer can turn the whole object into a JSON string
             * so we don't need to iterate through each record for "xml" or "json", but
             * we will for "text" and "html"
             */

            /*==================== JSON ====================*/
            if (log.Format == "json")
            {
                
                outString = JsonSerializer.Serialize(eventLogRecords, new JsonSerializerOptions()
                {
                    WriteIndented = true
                });
            }

            /*==================== XML ====================*/
            else if (log.Format == "xml")
            {
                using (var stringWriter = new StringWriter())
                {
                    var xmlSerializer = new XmlSerializer(eventLogRecords.GetType());
                    xmlSerializer.Serialize(stringWriter, eventLogRecords);
                    outString = stringWriter.ToString();
                }
            }
            else
            {
                foreach (GevLog.Record record in eventLogRecords)
                {
                    /*==================== Text ====================*/
                    if (log.Format == "text")
                    {
                        outString +=
                        $"---------------------------------------------------------------------------------\n" +
                        $"Id                 : {record.Id}\n" +
                        $"LevelDisplayName   : {record.LevelDisplayName}\n" +
                        $"Level              : {record.Level}\n" +
                        $"TimeCreated        : {record.TimeCreated}\n" +
                        $"ProviderName       : {record.ProviderName}\n" +
                        $"FormatDescription  : {record.FormatDescription}\n" +
                        $"Records            : {Globals.TotalRecords}\n" +
                        $"---------------------------------------------------------------------------------" +
                        "\n\n";
                    }

                    /*==================== HTML ====================*/
                    else if (log.Format == "html")
                    {
                        // This is specifically used by the gev_web application using a customized CSS style sheet.
                        if (Globals.TotalRecords < 1)
                        {
                            // Formulate output and adds header
                            outString += $"<table class=\"tg\" style=\"width:100%\">" +
                                $"    <colgroup>" +
                                $"        <col style=\"width:15%\">\n" +
                                $"        <col style=\"width:15%\">\n" +
                                $"        <col style=\"width:30%\">\n" +
                                $"        <col style=\"width:10%\">\n" +
                                $"    </colgroup>\n" +
                                $"    <tr>\n" +
                                $"        <th class=\"tg-h\">Level</th>\n" +
                                $"        <th class=\"tg-h\">Date</th>\n" +
                                $"        <th class=\"tg-h\">Source</th>\n" +
                                $"        <th class=\"tg-h\">EventID</th>\n" +
                                $"    </tr>\n" +
                                $"    <tr>\n" +
                                $"        <td class=\"tg-1\">{record.LevelDisplayName}</td>\n" +
                                $"        <td class=\"tg-1\">{record.TimeCreated}</td>\n" +
                                $"        <td class=\"tg-1\">{record.ProviderName}</td>\n" +
                                $"        <td class=\"tg-1\">{record.Id}</td>\n" +
                                $"    </tr>\n" +
                                $"    <tr>\n" +
                                $"        <td  class=\"tg-2\" colspan=\"4\"><pre>{record.FormatDescription}</pre></td>\n" +
                                $"    </tr>\n" +
                                $"</table>\n" +
                                $"<br>\n";
                        }
                        else
                        {
                            // Formulates output without header
                            outString += $"<table class=\"tg\" style=\"width:100%\">" +
                                $"    <colgroup>" +
                                $"        <col style=\"width:15%\">\n" +
                                $"        <col style=\"width:15%\">\n" +
                                $"        <col style=\"width:30%\">\n" +
                                $"        <col style=\"width:10%\">\n" +
                                $"    </colgroup>\n" +
                                $"    <tr>\n" +
                                $"        <td class=\"tg-1\">{record.LevelDisplayName}</td>\n" +
                                $"        <td class=\"tg-1\">{record.TimeCreated}</td>\n" +
                                $"        <td class=\"tg-1\">{record.ProviderName}</td>\n" +
                                $"        <td class=\"tg-1\">{record.Id}</td>\n" +
                                $"    </tr>\n" +
                                $"    <tr>\n" +
                                $"        <td  class=\"tg-2\" colspan=\"4\"><pre>{record.FormatDescription}</pre></td>\n" +
                                $"    </tr>\n" +
                                $"</table>\n" +
                                $"<br>\n";
                        }
                    }

                    /* Something bad did indeed happen */
                    else
                    {
                        AbortGev("Unable to determine format.");
                    }
                }
            }
            // Either output the records to a file, or to the console
            if (!string.IsNullOrEmpty(log.OutputFile))
            {
                // Write output to a file
                File.AppendAllText(log.OutputFile, outString);
            }
            else
            {
                // Write output to screen
                Console.Write($"{outString}\n");
            }
        }

        /// <summary>
        /// Displays help either with --help or if they used an incorrect argument.
        /// </summary>
        private static void DisplayHelp()
        {
            Console.WriteLine("gev: (g)et (ev)ent.  Written by Anthony Grimaldi, September 2019.\n\nUsage:");
            Console.Write("--help ......... Help\n" +
                          "--path ......... Path to archived event viewer log\n" +
                          "                     the only argument that is required\n" +
                          "--debug ........ Displays various debugging messages to the console\n" +
                          "--id ........... Id, comma separated (max 5)\n" +
                          "                     e.g. '-id \"5, 15, 1337\"'\n" +
                          "--source ....... Sources, comma separated (max 5)\n" +
                          "                     e.g. '-source \"vss, chkdsk, ntfs\"'\n" +
                          "--level ........ Sets the eventlevel.  Comma separated (max 5):\n" +
                          "                     1 = critical\n" +
                          "                     2 = error\n" +
                          "                     3 = warning\n" +
                          "                     4 = information\n" +
                          "                     5 = verbose\n" +
                          "--max .......... Sets the maximum number of events to output\n" +
                          "--direction .... Sets how to sort the output by date.  1 = Ascending, 2 = Descending\n" +
                          "--out-file ..... Sets the file gev outputs to\n" +
                          /* DISABLED "--query ........ Queries the dates in an event log\n" +
                           *          "                     this won't output anything but the first and last date\n" +
                           *          "                     of an event in a log rendering all the below arguments invalid\n" +
                           */
                          /* DISABLED "--exclude ...... exclude ids or sources, comma separated (max 10)\n" +
                           *          "                     e.g. \"-x vss,15,chkdsk,1337\"\n" +
                           */
                          // DISABLED "--offset ....... offset\n" +
                          // DISABLED "--date ......... date range\n" +
                          "\n" +
                          "Example:         \"gev --path \".\\application.evtx\" --source \"chkdsk, wininit\" --level 1,2,3\"\n" +
                          "                     this will search the application.evtx log for all chkdsk and wininit events\n" +
                          "                     that have an event level of \"critical\", \"error\", or \"warning\"\n");

            Environment.Exit(1);
        }

        /// <summary>
        /// Parses command-line arguments.
        /// </summary>
        /// <param name="arguments">args[] from main()</param>
        /// <param name="log">LogQuery class which stores user filters parsed from arguments.</param>
        /// <returns>The LogQuery object.</returns>
        private static GevLog.LogQuery ParseArguments(string[] arguments, GevLog.LogQuery log)
        {
            // Check for help
            if (arguments.Contains("--help"))
            {
                DisplayHelp(); // This also aborts
            }

            // Dictionary which contains list of acceptable arguments as keys, and whether the argument requires secondary parameters
            // as the values
            // For instance, in: `--path .\hello.evtx`, ".\hello.evtx" is a secondary parameter
            var argsDict = new Dictionary<string, bool>() {
                {       "--path", true   },
                {      "--debug", false  },
                {      "--query", false  },
                {         "--id", true   },
                {     "--source", true   },
                {      "--level", true   },
                {     "--format", true   },
                {   "--out-file", true   },
                {  "--direction", true   },
                {        "--max", true   }
            };

            for (int i = 0; i < arguments.Length; i++)
            {
                string key = arguments[i].ToLower();
                if (argsDict.ContainsKey(key))
                {
                    string secondaryParameter = "";

                    // if the Value for the Key is "true", it means we need to look for secondary parameters
                    if (argsDict[key])
                    {
                        secondaryParameter = arguments[i + 1];

                        i++; // In the event that a secondary parameter is needed and found, we need to increment
                             // `i` again so the next iteration will find the next key, and not the previous
                             // key's parameter
                    }

                    switch (key)
                    {
                        case "--":
                            return log; // Stop parsing arguments

                        ///////////////////// Path //////////////////////
                        case "--path":
                            string pathArgument = secondaryParameter;

                            if (File.Exists(pathArgument))
                            {
                                // Looks good, assign value
                                log.LogPath = pathArgument;
                            }
                            else
                            {
                                if (File.Exists($"{pathArgument}.evtx"))
                                {
                                    // Append .evtx
                                    log.LogPath = ($"{pathArgument}.evtx");
                                }
                                else
                                {
                                    // It's not valid
                                    AbortGev($"Invalid path: \"{pathArgument}\"");
                                }
                            }
                            break;

                        //////////////////// Debug /////////////////////
                        case "--debug":
                            log.DebugMode = true;
                            break;

                        //////////////////// Query /////////////////////
                        case "--query":
                            log.QuerySet = true;
                            return log; // Stop parsing arguments

                        ///////////////////// IDs //////////////////////
                        case "--id":
                            string idArgument = secondaryParameter;
                            if (!string.IsNullOrEmpty(idArgument))
                            {
                                if (idArgument.Split(',').Length > 5)
                                {
                                    // Too many IDs (max 5), abort
                                    AbortGev("Too many IDs. Max 5.");
                                }
                                else
                                {
                                    // Iterate through each
                                    foreach (var id in idArgument.Split(','))
                                    {
                                        if (int.TryParse(id, out int idSubValue))
                                        {
                                            if (idSubValue < 1 || idSubValue > 65535)
                                            {
                                                // It is an int, but it's outside 1-65535
                                                AbortGev($"Invalid ID: \"{idSubValue}\" Please input a value between 1 and 65535.");
                                            }
                                            else
                                            {
                                                // All is good, add to list
                                                log.IdFilter.Add(idSubValue);
                                            }
                                        }
                                        else
                                        {
                                            // It isn't an int, abort
                                            AbortGev($"Invalid ID. \"{id}\" is not an integer.");
                                        }
                                    }
                                }
                                // Check to see we ended up with any ids
                                if (log.IdFilter.Count < 1)
                                {
                                    // Something broke (we had an --id flag but there was a problem parsing IDs)
                                    AbortGev("There was a problem parsing IDs. Valid IDs are separated by commas without spaces.");
                                }
                            }
                            break;

                        /////////////////// Sources ////////////////////
                        case "--source":
                            string sourceArgument = secondaryParameter;

                            if (!string.IsNullOrEmpty(sourceArgument))
                            {
                                if (sourceArgument.Split(',').Length > 5)
                                {
                                    // Too many sources (max 5), abort
                                    AbortGev("Too many sources. Max 5.");
                                }

                                // Iterate through each and add to the source list
                                foreach (var source in sourceArgument.Split(','))
                                {
                                    log.SourceFilter.Add(source);
                                }

                                // Check to see we ended up with any ids
                                if (log.SourceFilter.Count < 1)
                                {
                                    // Something broke (we had a --source flag but there was a problem parsing sources)
                                    AbortGev("There was a problem parsing sources. Valid sources are separated by commas without spaces.");
                                }
                            }
                            break;

                        //////////////////// Level /////////////////////
                        case "--level":
                            string levelArgument = secondaryParameter;

                            if (!String.IsNullOrEmpty(levelArgument))
                            {
                                if (levelArgument.Split(',').Length > 5)
                                {
                                    // Too many levels (max 5), abort
                                    AbortGev("Too many levels.  Max 5.");
                                }
                                else
                                {
                                    // Iterate through each
                                    foreach (var level in levelArgument.Split(','))
                                    {
                                        if (int.TryParse(level, out int levelSubValue))
                                        {
                                            if (levelSubValue < 1 || levelSubValue > 5)
                                            {
                                                // It is an int, but it's outside 1-5
                                                AbortGev($"Invalid event level: \"{levelSubValue}\" Please input a value between 1 and 5.");
                                            }
                                            else
                                            {
                                                // All is good, add to list
                                                log.LevelFilter.Add(levelSubValue);
                                            }
                                        }
                                        else
                                        {
                                            // It isn't an int, abort
                                            AbortGev($"Invalid level. \"{level}\" is not an integer.");
                                        }
                                    }
                                }

                                // Check to see we ended up with any levels
                                if (log.LevelFilter.Count < 1)
                                {
                                    // Something broke (we had an --level flag but there was a problem parsing levels)
                                    AbortGev("There was a problem parsing IDs.  Valid IDs are separated by commas without spaces.");
                                }
                            }
                            break;

                        /////////////////// Format /////////////////////
                        case "--format":
                            string formatArgument = secondaryParameter;

                            if (!string.IsNullOrEmpty(formatArgument))
                            {
                                // Check for case-insensitive strings
                                if (formatArgument.Equals("xml", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    log.Format = "xml"; // xml
                                }
                                else if (formatArgument.Equals("html", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    log.Format = "html"; // html
                                }
                                else if (formatArgument.Equals("text", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    log.Format = "text"; // text
                                }
                                else if (formatArgument.Equals("json", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    log.Format = "json"; // json
                                }
                                else
                                {
                                    // They didn't supply 'xml', 'html', 'text', or 'json'
                                    AbortGev("There was a problem parsing format.  Valid formats are 'xml', 'html', 'text', or 'json'.");
                                }
                            }
                            break;

                        ///////////////// Output File //////////////////
                        case "--out-file":
                            string outfileArgument = secondaryParameter;

                            if (!string.IsNullOrEmpty(outfileArgument))
                            {
                                // Parse format:
                                switch (log.Format)
                                {
                                    case "text":  // Defaults to .txt
                                        if (outfileArgument.EndsWith(".txt"))
                                        {
                                            log.OutputFile = outfileArgument;
                                        }
                                        else
                                        {
                                            log.OutputFile = (outfileArgument + ".txt");
                                        }
                                        break;

                                    case "xml": // xml
                                        if (outfileArgument.EndsWith(".xml"))
                                        {
                                            log.OutputFile = outfileArgument;
                                        }
                                        else
                                        {
                                            log.OutputFile = (outfileArgument + ".xml");
                                        }
                                        break;

                                    case "html": // html
                                        if (outfileArgument.EndsWith(".html"))
                                        {
                                            log.OutputFile = outfileArgument;
                                        }
                                        else
                                        {
                                            log.OutputFile = (outfileArgument + ".html");
                                        }
                                        break;

                                    case "json": // json
                                        if (outfileArgument.EndsWith(".json"))
                                        {
                                            log.OutputFile = outfileArgument;
                                        }
                                        else
                                        {
                                            log.OutputFile = (outfileArgument + ".json");
                                        }
                                        break;

                                    default: // Something broke
                                        AbortGev("There was an error when parsing output file.");
                                        break;
                                }

                                // If the file exists, back it up
                                if (File.Exists(log.OutputFile))
                                {
                                    // Grab file name (base + extension)
                                    string fileBase = Path.GetFileNameWithoutExtension(log.OutputFile);
                                    string fileExt = Path.GetExtension(log.OutputFile);

                                    // Random number
                                    string randomNumber;
                                    Random random = new Random();

                                    // Keep trying to rename the file until we get one that doesn't exist.
                                    do
                                    {
                                        // Create string to prepend
                                        string prependString = String.Empty;

                                        // Create random number between 1-10000
                                        randomNumber = random.Next(10000).ToString();

                                        // If the nmber is < 10000, prepend '0's to the string to make it 5 characters long.
                                        if (randomNumber.Length < 5)
                                        {
                                            for (int x = 0; x + randomNumber.Length < 5; x++)
                                            {
                                                prependString += "0";
                                            }
                                        }
                                        log.OutputFile = (fileBase + "-" + prependString + randomNumber + fileExt);
                                    } while (File.Exists(log.OutputFile));

                                    // Create it
                                    File.Create(log.OutputFile).Close();
                                }
                                else
                                {
                                    File.Create(log.OutputFile).Close();
                                }
                            }
                            break;

                        ////////////////// Direction ///////////////////
                        case "--direction":
                            string directionArgument = secondaryParameter;

                            if (!String.IsNullOrEmpty(directionArgument))
                            {
                                if (int.TryParse(directionArgument, out int direction))
                                {
                                    if (direction < 1 || direction > 2)
                                    {
                                        // It is an int, but outside 1-2
                                        AbortGev($"Invalid direction: \"{direction}\" Please input either 1 or 2.");
                                    }
                                    else
                                    {
                                        // Everything looks good, we've gotten a valid direction.
                                        log.Direction = direction;
                                    }
                                }
                                else
                                {
                                    // It's not an integer
                                    AbortGev($"Invalid direction. \"{directionArgument}\" is not an integer.");
                                }
                            }
                            break;

                        //////////////// Maximum Events ////////////////
                        case "--max":
                            string maxEventsArgument = secondaryParameter;

                            if (!String.IsNullOrEmpty(maxEventsArgument))
                            {
                                if (int.TryParse(maxEventsArgument, out int maxEvents))
                                {
                                    if (maxEvents < 1)
                                    {
                                        // Doesn't make sense
                                        AbortGev($"Invalid maximum events: \"{maxEvents}\" Please input a number greater than 0.");
                                    }
                                    else
                                    {
                                        // Everything looks good, we've gotten a valid maximum
                                        Globals.MaxEvents = maxEvents;
                                    }
                                }
                            }
                            break;
                    }
                }
                else
                {
                    // Unknown argument
                    AbortGev($"Unknown argument {arguments[i]}");
                }
            }

            // All done
            return log;
        }

        /// <summary>
        /// Kills the program with a message
        /// </summary>
        /// <param name="abortMessage">Error</param>
        private static void AbortGev(string abortMessage)
        {
            // Change the font color to red and let them know why we're aborting
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nError: {0} Aborting.", abortMessage);
            Console.ForegroundColor = ConsoleColor.White;
            Environment.Exit(1);
        }

        /// <summary> Display the debugging information about command-line arguments </summary>
        private static void DisplayArguments(GevLog.LogQuery log)
        {
            Console.WriteLine($"\nParsed arguments:\n");
            Console.WriteLine($"Path = \"{log.LogPath}\"");
            Console.WriteLine($"Outfile = \"{log.OutputFile}\"");
            Console.WriteLine($"Date = \"{log.DateRangeFilter}\"");
            Console.WriteLine($"Offset = \"{log.DateOffsetFilter}\"");
            Console.Write("Levels = \"");
            for (int i = 0; i < log.LevelFilter.Count; i++)
            {
                Console.Write(log.LevelFilter[i]);
                if (i < (log.LevelFilter.Count - 1)) Console.Write(", ");
            }
            Console.Write("\"\n");
            Console.Write("IDs = \"");
            for (int i = 0; i < log.IdFilter.Count; i++)
            {
                Console.Write(log.IdFilter[i]);
                if (i < (log.IdFilter.Count - 1)) Console.Write(", ");
            }
            Console.Write("\"\n");
            Console.WriteLine($"IDCount = \"{log.IdFilter.Count}\"");
            Console.Write("Sources = \"");
            for (int i = 0; i < log.SourceFilter.Count; i++)
            {
                Console.Write(log.SourceFilter[i]);
                if (i < (log.SourceFilter.Count - 1))
                {
                    Console.Write(", ");
                }
            }
            Console.WriteLine($"\nSource count: {log.SourceFilter.Count}");
            Console.Write("\n");
        }
    }
}