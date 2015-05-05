namespace SingleViewUniversal

open System
open UIKit
open Foundation

// The UIApplicationDelegate for the application. This class is responsible for launching the
// User Interface of the application, as well as listening (and optionally responding) to
// application events from iOS.
[<Register ("AppDelegate")>]
type AppDelegate () = 
    inherit UIApplicationDelegate ()
    
    override val Window = null with get,set

    // This method is invoked when the application is about to move from active to inactive state.
    // OpenGL applications should use this method to pause.
    override x.OnResignActivation (application) = ()

    // This method should be used to release shared resources and it should store the application state.
    // If your application supports background exection this method is called instead of WillTerminate
    // when the user quits.
    override x.DidEnterBackground (application) = ()
                
    // This method is called as part of the transiton from background to active state.
    override x.WillEnterForeground (application) = ()

    // This method is called when the application is about to terminate. Save data, if needed.
    override x.WillTerminate (application) = ()

module Main = 
    [<EntryPoint>]
    let main args = 
        UIApplication.Main (args, null, "AppDelegate")
        0