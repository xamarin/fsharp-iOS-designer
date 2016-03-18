namespace CocoaApp
open System
open Foundation
open AppKit

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit NSApplicationDelegate ()

    override x.DidFinishLaunching (notification) =
        // Insert code here to initialize your application
        ()
        
    override x.WillTerminate(notification) =
        // Insert code here to tear down your application
        ()
