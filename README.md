## Gev (Get Event)

This program is used to read events from an archived .evtx file, and return them in a few different ways.  Currently, it only outputs a JSON string that contains all events found based on search criteria.

This uses a list of EventLogProviders found in event_providers.txt to build an XPath query using the Providers (sources), EventLevels, and EventIds.

As far as I know, System.Diagnostics.Eventing.Reader.EventLogReader uses XPath 1.0, which does not support regex or any sort of cool (i.e. convenient) pattern matching.  To get around this, I compare user input against the list in the text file.  This allows me to, for instance, generate the following XPath from `--source ntfs`:
```
Provider[@Name='Microsoft-Windows-Ntfs' or @Name='Microsoft-Windows-Ntfs-UBPM' or @Name='Ntfs']
```

Otherwise, you would have to have the user specifically input "Microsoft-Windows-Ntfs".

This is used in tandem with my other project to build a web-based event log parser -> https://github.com/grimhv/gev_web

A live version of the software can be seen running here -> https://gev.honeypox.dev

Usage:
```
--help ......... Help
--path ......... Path to archived event viewer log
                 the only argument that is required
--query ........ Queries the dates in an event log
                 this won't output anything but the first and last date
                 of an event in a log rendering all the below arguments invalid
--debug ........ Displays various debugging messages to the console
--id ........... Id, comma separated (max 5)
                 e.g. "-id 5,15,1337"
--source ....... Sources, comma separated (max 5)
                 e.g. "-source vss,chkdsk,ntfs"
--level ........ Sets the eventlevel.  Comma separated (max 5):
                 1 = critical
                 2 = error
                 3 = warning
                 4 = information
                 5 = verbose
--max .......... Sets the maximum number of events to output
--direction .... Sets how to sort the output by date.  1 = Ascending, 2 = Descending
Example:         "gev --path .\application.evtx --source chkdsk,wininit --level 1,2,3"
                 this will search the application.evtx log for all chkdsk and wininit events
                 that have an event level of "critical", "error", or "warning"
```

Sample output:
```
PS> ./gev.exe --path ../uploads/app_demo.evtx --level 3 --max 2 --direction 2
[
  {
    "Index": 0,
    "ID": "1008",
    "Level": "3",
    "Date": "2/25/2020 11:17:06 AM",
    "Source": "Microsoft-Windows-Perflib",
    "Description": "The Open procedure for service \u0022BITS\u0022 in DLL \u0022C:\\Windows\\System32\\bitsperf.dll\u0022 failed with error code The system cannot find the file specified.. Performance data for this service will not be available."
  },
  {
    "Index": 1,
    "ID": "2003",
    "Level": "3",
    "Date": "2/25/2020 1:14:55 AM",
    "Source": "Microsoft-Windows-Perflib",
    "Description": "The configuration information of the performance library \u0022perf-MSSQL$SQLEXPRESS-sqlctr12.2.5000.0.dll\u0022 for the \u0022MSSQL$SQLEXPRESS\u0022 service does not match the trusted performance library information stored in the registry. The functions in this library will not be treated as trusted."
  }
]
```
