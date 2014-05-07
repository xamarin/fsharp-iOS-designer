namespace Xamarin.iOSProviders
open System
open System.Diagnostics

/// Represents a stream of IObserver events. 
type ObservableSource<'T>() =

    let protect funct =
        let mutable ok = false 
        try 
            funct()
            ok <- true 
        finally
            Debug.Assert(ok, "IObserver method threw an exception.")

    let mutable key = 0

    // Use a Map, not a Dictionary, because callers might unsubscribe in the OnNext 
    // method, so thread-safe snapshots of subscribers to iterate over are needed. 
    let mutable subscriptions = Map.empty : Map<int, IObserver<'T>>

    let next(obs) = 
        subscriptions |> Seq.iter (fun (KeyValue(_, value)) -> 
            protect (fun () -> value.OnNext(obs)))

    let completed() = 
        subscriptions |> Seq.iter (fun (KeyValue(_, value)) -> 
            protect (fun () -> value.OnCompleted()))

    let error(err) = 
        subscriptions |> Seq.iter (fun (KeyValue(_, value)) -> 
            protect (fun () -> value.OnError(err)))

    let thisLock = new obj()

    let obs = 
        { new IObservable<'T> with 
            member this.Subscribe(obs) =
                let key1 =
                    lock thisLock (fun () ->
                        let key1 = key
                        key <- key + 1
                        subscriptions <- subscriptions.Add(key1, obs)
                        key1)
                { new IDisposable with  
                    member this.Dispose() = 
                        lock thisLock (fun () -> 
                            subscriptions <- subscriptions.Remove(key1)) } }

    let mutable finished = false 

    // The source ought to call these methods in serialized fashion (from 
    // any thread, but serialized and non-reentrant). 
    member this.Next(obs) =
        Debug.Assert(not finished, "IObserver is already finished")
        next obs

    member this.Completed() =
        Debug.Assert(not finished, "IObserver is already finished")
        finished <- true
        completed()

    member this.Error(err) =
        Debug.Assert(not finished, "IObserver is already finished")
        finished <- true
        error err

    // The IObservable object returned is thread-safe; you can subscribe  
    // and unsubscribe (Dispose) concurrently. 
    member this.AsObservable = obs