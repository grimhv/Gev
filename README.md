## Gev (Get Event)

This program is used to read events from an archived .evtx file, and return them in a few different ways.  Currently, it can output raw text, HTML, XML, and a JSON string that contains all events found based on search criteria to either the console window or a specified file.

This uses a list of EventLogProviders found in event_providers.txt to build an XPath query using the Providers (sources), EventLevels, and EventIds.

As far as I know, https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogreader uses XPath 1.0, which does not support regex or any sort of cool (i.e. convenient) pattern matching.  To get around this, I compare user input against the list from the text file.  This allows me to, for instance, generate the following XPath from `--source ntfs`:
```
Provider[@Name='Microsoft-Windows-Ntfs' or @Name='Microsoft-Windows-Ntfs-UBPM' or @Name='Ntfs']
```

Otherwise, you would have to have the user specifically input "Microsoft-Windows-Ntfs".

This is used in tandem with my other project to build a web-based event log parser -> https://github.com/grimhv/gev_web

A live version of the software can be seen running here -> https://gev.honeypox.dev

Usage:
```
PS> ./gev.exe --help
gev: (g)et (ev)ent.  Written by Anthony Grimaldi, September 2019.

Usage:
--help ......... Help
--path ......... Path to archived event viewer log
                 the only argument that is required
--debug ........ Displays various debugging messages to the console
--id ........... Id, comma separated (max 5)
                 e.g. "-id 5,15,1337"
--source ....... Sources, comma separated (max 5)
                 e.g. '-source "vss, chkdsk, ntfs"'
--level ........ Sets the eventlevel.  Comma separated (max 5):
                 1 = critical
                 2 = error
                 3 = warning
                 4 = information
                 5 = verbose
--max .......... Sets the maximum number of events to output
--direction .... Sets how to sort the output by date.  1 = Ascending, 2 = Descending
--out-file ..... Sets the file gev outputs to
Example:         "gev --path ".\application.evtx" --source "chkdsk, wininit" --level 1,2,3"
                 this will search the application.evtx log for all chkdsk and wininit events
                 that have an event level of "critical", "error", or "warning"
```

### Sample output:

Text:
```
PS> ./gev.exe --path "app_demo.evtx" --level "1, 2" --source "User Profile, perflib" --max 2 --direction 2 --format "text"
---------------------------------------------------------------------------------
Id                 : 1552
LevelDisplayName   : Error
Level              : 2
TimeCreated        : 12/17/2019 11:32:04 AM
ProviderName       : Microsoft-Windows-User Profiles Service
FormatDescription  : User hive is loaded by another process (Registry Lock) Process name: C:\Windows\System32\svchost.exe, PID: 2632, ProfSvc PID: 1228.
Records            : 2
---------------------------------------------------------------------------------

---------------------------------------------------------------------------------
Id                 : 1017
LevelDisplayName   : Error
Level              : 2
TimeCreated        : 12/4/2019 10:37:19 AM
ProviderName       : Microsoft-Windows-Perflib
FormatDescription  : Disabled performance counter data collection from the "ASP.NET_64_2.0.50727" service because the performance counter library for that service has generated one or more errors. The errors that forced this action have been written to the application event log. Correct the errors before enabling the performance counters for this service.
Records            : 2
---------------------------------------------------------------------------------
```

XML:
```
PS> ./gev.exe --path "app_demo.evtx" --level "1, 2" --source "User Profile, perflib" --max 2 --direction 2 --format "xml"
<?xml version="1.0" encoding="utf-16"?>
<ArrayOfRecord xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Record>
    <Index>0</Index>
    <Id>1552</Id>
    <LevelName>Error</LevelName>
    <LevelNumber>2</LevelNumber>
    <Date>12/17/2019 11:32:04 AM</Date>
    <Source>Microsoft-Windows-User Profiles Service</Source>
    <Description>User hive is loaded by another process (Registry Lock) Process name: C:\Windows\System32\svchost.exe, PID: 2632, ProfSvc PID: 1228.</Description>
  </Record>
  <Record>
    <Index>1</Index>
    <Id>1017</Id>
    <LevelName>Error</LevelName>
    <LevelNumber>2</LevelNumber>
    <Date>12/4/2019 10:37:19 AM</Date>
    <Source>Microsoft-Windows-Perflib</Source>
    <Description>Disabled performance counter data collection from the "ASP.NET_64_2.0.50727" service because the performance counter library for that service has generated one or more errors. The errors that forced this action have been written to the application event log. Correct the errors before enabling the performance counters for this service.</Description>
  </Record>
</ArrayOfRecord>
```

JSON:
```
PS> ./gev.exe --path "app_demo.evtx" --level "1, 2" --source "User Profile, perflib" --max 2 --direction 2 --format "json"
[
  {
    "Index": 0,
    "Id": "1552",
    "LevelName": "Error",
    "LevelNumber": 2,
    "Date": "12/17/2019 11:32:04 AM",
    "Source": "Microsoft-Windows-User Profiles Service",
    "Description": "User hive is loaded by another process (Registry Lock) Process name: C:\\Windows\\System32\\svchost.exe, PID: 2632, ProfSvc PID: 1228."
  },
  {
    "Index": 1,
    "Id": "1017",
    "LevelName": "Error",
    "LevelNumber": 2,
    "Date": "12/4/2019 10:37:19 AM",
    "Source": "Microsoft-Windows-Perflib",
    "Description": "Disabled performance counter data collection from the \u0022ASP.NET_64_2.0.50727\u0022 service because the performance counter library for that service has generated one or more errors. The errors that forced this action have been written to the application event log. Correct the errors before enabling the performance counters for this service."
  }
]
```

