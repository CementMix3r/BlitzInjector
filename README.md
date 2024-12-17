# BlitzInjector
World of Tanks Blitz dll injector, it is the injector for the mod loader :)

# How to Use
- firstly, get the dll (its not released yet), then either click "browse me" to browse for the dll, or you can drag it into the window to load it,
- (in development mod) put zipped mods into the mods folder (click the open mods folder first), pls do not extract them, the dll will do it for you
- click inject, and itll try to open the process for you, if its already open, itll just inject into it, (if it fails to open the process for you, just open with steam or wgc)

# Other
- make sure to exclude it from your anti virus in windows settings, else windows will try to get rid of it (its a false positive like all injectors)
- to do this, go to windows security->virus & thread protection->virus & thread protection settings\manage settings->Exclusions\add or remove exclusion. just add the injector itself (.exe) as an exclusion, so that windows wont be constantly trying to get rid of it :)

# Building it yourself
- because im lazy and didnt upload the sln, youll want to open visual studio, make a WPF app (.NET Framework) in c#, then open the folder location, and just put these in there, if you make any of your own changes, and want me to update it on the github for others, just make a request or something
- you might wanna install costura (optional), and also you need System.Text.Json (for the config stuff)
- i have no idea how to use github guys dont be mean ):
