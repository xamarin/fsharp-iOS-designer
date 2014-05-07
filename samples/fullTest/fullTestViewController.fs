namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

type fstestViewController = Xamarin.iOSProviders.UIProvider<"Main.storyboard">
type multiController = Xamarin.iOSProviders.UIProvider<"multitest.storyboard">

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let mutable window = new UIWindow (UIScreen.MainScreen.Bounds)
    let vc = new fstestViewController()

    //override val Window = new UIWindow (UIScreen.MainScreen.Bounds) with get,set
        //with get() = window
        //and set v = window <- v

    override this.FinishedLaunching (app, options) =
        
        //vc.OnClickUp <-
        //    fun _ -> printfn "Hello"
        vc.View.BackgroundColor <- UIColor.Red
        window.RootViewController <- vc
        window.MakeKeyAndVisible ()
        true


module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main (args, null, "AppDelegate")
        0

