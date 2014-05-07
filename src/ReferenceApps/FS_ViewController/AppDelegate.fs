namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation

[<Register ("AppDelegate")>]
type AppDelegate() as this =
    inherit UIApplicationDelegate ()

    let vc = new fsReferenceViewController ()
    let window = new UIWindow(UIScreen.MainScreen.Bounds, RootViewController=vc)

    //This method is invoked when the application is ready to run.
//    override this.FinishedLaunching (app, options) =
//        x.Window.MakeKeyAndVisible()
//        true
//
    override x.Window = window

    override x.FinishedLaunching (app, options) =
        vc.OnClickUpAction <- fun _ -> ()
        true

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main (args, null, "AppDelegate")
        0

