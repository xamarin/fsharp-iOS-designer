namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

type myViewController = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard">

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let mainStoryboard = UIStoryboard.FromName ("Main", null)
    let initialViewController = mainStoryboard.InstantiateInitialViewController () :?> myViewController
    let window = new UIWindow(UIScreen.MainScreen.Bounds, RootViewController= initialViewController)

    override this.FinishedLaunching (app, options) =
        initialViewController.Mytouchup <- fun _ -> initialViewController.View.BackgroundColor <- UIColor.Red
        window.MakeKeyAndVisible ()
        true

module Main =
    UIApplication.Main ([||], null, "AppDelegate")