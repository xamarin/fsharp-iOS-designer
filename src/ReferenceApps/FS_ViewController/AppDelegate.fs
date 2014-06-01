namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation

[<Register ("AppDelegate")>]
type AppDelegate() =
    inherit UIApplicationDelegate ()

    let mainStoryboard = UIStoryboard.FromName ("MainStoryboard", null)
    let initialViewController = mainStoryboard.InstantiateInitialViewController () :?> fsReferenceViewController
    let window = new UIWindow(UIScreen.MainScreen.Bounds, RootViewController= initialViewController)

    override x.FinishedLaunching(app, options) =
        initialViewController.OnClickUpAction <- (fun _ -> initialViewController.View.BackgroundColor <- UIColor.Red )
        window.MakeKeyAndVisible ()
        true

module Main =
    UIApplication.Main ([||], null, "AppDelegate")