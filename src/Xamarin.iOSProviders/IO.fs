﻿namespace Xamarin.iOSProviders
open System
open System.Collections.Generic
open System.IO
open System.Net
open ProviderImplementation.ProvidedTypes

module IO =
    let log str= Console.WriteLine(str:string)

    //TODO switch watcher to a type and implement IDisposable and make Invalidate an observable
    //This would mean that openWithWatcher would return a watcher that could be used to subscribe to invalidate
    //changes rather than pass in the TP's Invalidate method in. This is more or less the pattern used by the FileSystemProvider.   
    let watch (uri:Uri) invalidator =

        let getLastWrite() = File.GetLastWriteTime uri.OriginalString 
        let lastWrite = ref (getLastWrite())
        
        let watcher = 
            let path = Path.GetDirectoryName uri.OriginalString
            let name = Path.GetFileName uri.OriginalString
            new FileSystemWatcher(Filter = name, Path = path, EnableRaisingEvents = true)

        let checkForChanges _ =
            let curr = getLastWrite()
        
            if !lastWrite <> curr then
                log ("Invalidated " + uri.OriginalString)
                lastWrite := curr
                invalidator()
        do
            watcher.Changed.Add checkForChanges
            watcher.Renamed.Add checkForChanges
            watcher.Deleted.Add checkForChanges
        watcher :> IDisposable
             
    /// Opens the Uri and sets up a filesystem watcher that calls the invalidate function whenever the file changes
    let openWithWatcher (uri:Uri) invalidator =
        let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
        let file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream
        file, watch uri invalidator