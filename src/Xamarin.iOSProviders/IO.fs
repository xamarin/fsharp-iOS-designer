namespace Xamarin.UIProviders.DesignTime
open System
open System.Collections.Generic
open System.IO
open System.Net
open ProviderImplementation.ProvidedTypes

module File =
    let log str= Console.WriteLine(str:string)

    //TODO switch watcher to a type and implement IDisposable and make Invalidate an observable
    //This would mean that openWithWatcher would return a watcher that could be used to subscribe to invalidate
    //changes rather than pass in the TP's Invalidate method in. This is more or less the pattern used by the FileSystemProvider.   
    let watch path invalidator =

        let getLastWrite() = File.GetLastWriteTime path 
        let lastWrite = ref (getLastWrite())
        
        let watcher = 
            let path = Path.GetDirectoryName path
            let name = Path.GetFileName path
            new FileSystemWatcher(Filter = name, Path = path, EnableRaisingEvents = true)

        let checkForChanges _ =
            let curr = getLastWrite()
        
            if !lastWrite <> curr then
                log ("Invalidated " + path)
                lastWrite := curr
                invalidator()
        do
            watcher.Changed.Add checkForChanges
            watcher.Renamed.Add checkForChanges
            watcher.Deleted.Add checkForChanges
        watcher :> IDisposable