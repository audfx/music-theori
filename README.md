[![dev & player chat](https://discordapp.com/api/guilds/594032849804591114/widget.png?style=shield)](https://discord.gg/pQkF5HM)

# music:theori
A rhythm game development platform targeting desktop systems.

While :theori will in the future include a standard game client for hosting game modes developed with it, the bulk of development work is currently on the to-be-included game modes themselves as standalone products first, [NeuroSonic](https://github.com/audfx/neurosonic) being the primary development force and drive for this project. Other official game modes are planned as well as a way for players to create their own game modes and publish them for other players to enjoy with all the multiplayer functionality :theori will provide. For this reason, the `Clients/theori-core3.0` project will remain relatively barren.

# Running
:theori isn't intended to be run standalone currently, and even it it was you'd need a development environment set up for the time. :theori makes use of .NET development tools which aren't in full releases yet. Follow the **Building** guide below for information on seting up that development environment.

# Building
:theori targets .NET Standard 2.1 / .NET Core 3.0 so you'll need an up-to-date dev environment for those; the code occasionally makes use of modern features which aren't supported on .NET Framework versions or older .NET Core and Standard versions.

## Windows
On Windows as of August 2019 you'll need to download [Visual Studio 2019 Preview](https://visualstudio.microsoft.com/vs/preview/) which can be installed alongside current Visual Studio installations.
When installing, make sure you have the .NET Core cross-platform development workload and .NET Core 3.0 SDK individual components checked.

With a properly setup Visual Studio Preview installed, simply open the solution and build or run the desired projects.

## Non-Windows
I'm currently only a Windows-familiar developer myself so all I can say is do your best to set up a .NET Core 3.0 compatible development environment and go to town. Feel free to create a pull request with valid OS setup instructions for non-Windows builds!

Maybe JetBrains Rider? Does that support the preview stuff that Visual Studio does? Are there releases of .NET Core 3.0 outside of Visual Studio?
