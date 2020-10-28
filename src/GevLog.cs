using System.Collections.Generic;

namespace Gev
{
    public class GevLog
    {
        public class Record

        {
            public Record() { }
            public Record(int index, string id, string levelName, int levelNumber, string date, string source, string description)
            {
                Index = index;
                Id = id;
                LevelName = levelName;
                LevelNumber = levelNumber;
                Date = date;
                Source = source;
                Description = description;
            }

            public int Index { get; set; }
            public string Id { get; set; }
            public string LevelName { get; set; }
            public int LevelNumber { get; set; }
            public string Date { get; set; }
            public string Source { get; set; }
            public string Description { get; set; }
        }

        internal class LogQuery
        {
            /// <summary>Path to a .evtx file.</summary>
            public string LogPath { get; set; }

            /// <summary>Name of the file to output results to.</summary>
            public string OutputFile { get; set; }

            /// <summary>List of event providers to filter.</summary>
            public List<string> SourceFilter = new List<string>();

            /// <summary>List of event IDs to filter.</summary>
            public List<int> IdFilter = new List<int>();

            /// <summary>List of event levels to filter.</summary>
            public List<int> LevelFilter = new List<int>();

            /// <summary>How far back, in days, from the newest event to filter.</summary>
            public int DateOffsetFilter { get; set; }

            /// <summary>How many days to search back from the offset.</summary>
            public int DateRangeFilter { get; set; }

            /// <summary>Direction for sorting events by TimeCreated.  1 = Ascending, 2 = Descending</summary>
            public int Direction { get; set; }

            /// <summary>Format for output file.  Either 'xml', 'html', or 'text'.</summary>
            public string Format { get; set; } = "text";

            /// <summary> Set by the --debug flag.  Outputs debugging info to the console. </summary>
            public bool DebugMode { get; set; } = false;

            /// <summary> Set by the --query flag.  Grabs oldest and newest event dates and record counts. </summary>
            public bool QuerySet { get; set; } = false;

            /* UNIMPLEMENTED
             * public DateTime newestEvent { get; set; }
             * public DateTime oldestEvent { get; set; }
             */
        }

    }
}