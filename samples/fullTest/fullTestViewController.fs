namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

type myViewController = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard">

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let vc = new myViewController()
    let window = new UIWindow (UIScreen.MainScreen.Bounds, RootViewController=vc)

    override x.Window = window
    override this.FinishedLaunching (app, options) =
        
        //action applied as a lambda, will be available as an IObservable soon too.
        vc.Mytouchup <- fun sender -> vc.View.BackgroundColor <- UIColor.Red
            
        window.MakeKeyAndVisible ()
        true


module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main (args, null, "AppDelegate")
        0