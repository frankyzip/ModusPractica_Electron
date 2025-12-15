ModusPractica - Intelligent Practice Planner
System Requirements

Windows 10 or Windows 11
.NET 8 Desktop Runtime (required)
4GB RAM (recommended)
100MB free disk space

Installation Instructions

Check if you have .NET 8 Desktop Runtime installed

If you're not sure, continue with step 2.


Download the .NET 8 Desktop Runtime

Visit: https://dotnet.microsoft.com/download/dotnet/8.0
Download the ".NET Desktop Runtime" installer (not just the ASP.NET Core Runtime)
Choose the version matching your system (x64 for most modern computers)


Install the .NET 8 Desktop Runtime

Run the downloaded installer
Follow the on-screen instructions


Run ModusPractica

Extract all files from the ModusPractica folder to a location of your choice
Double-click on ModusPractica.exe to start the application



Data Storage
ModusPractica stores your data in the following location:
%AppData%\ModusPractica\
This translates to (replace [Username] with your Windows username):
C:\Users\[Username]\AppData\Roaming\ModusPractica\
Important subdirectories:

MusicPieces: Contains your saved music pieces
History: Contains your practice history
Scheduled: Contains your scheduled practice sessions

Note: These directories are created automatically when you first use the application.
Troubleshooting
If the application doesn't start:

Verify .NET Installation

Open Command Prompt (search for "cmd" in the Start menu)
Type: dotnet --info
Verify that ".NET Desktop Runtime" version 8.x.x is listed


Check Windows Event Viewer

Press Win+R, type "eventvwr.msc" and press Enter
Look for errors under "Windows Logs" > "Application"


Check Application Data

Ensure that your user account has write permissions to the %AppData% folder
Try running the application as administrator: right-click ModusPractica.exe and select "Run as administrator"




About ModusPractica
ModusPractica is an intelligent practice management application designed for musicians of all levels. It leverages cutting-edge machine learning techniques based on Hermann Ebbinghaus's scientific research on memory and learning to optimize your practice routine.
By applying spaced repetition algorithms and Gebrian's research on overlearning, ModusPractica helps you achieve better results with less practice time, allowing you to learn music more efficiently and retain it longer.