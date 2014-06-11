namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

//view controller is generated from the type provider and embedded inot the assembly here
type myViewController = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard">

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let initialViewController = myViewController.CreateInitialViewController()
    //this shows a possibility to access the outlets that are generated from the storyboard via the ViewDidLoadAction
    do initialViewController.ViewDidLoadAction <-
        fun _ -> initialViewController.View.ContentMode <- UIViewContentMode.ScaleToFill
                 initialViewController.MyButton.TouchDown.Add (fun _ -> initialViewController.View.BackgroundColor <- UIColor.Blue) 

    let window = new UIWindow(UIScreen.MainScreen.Bounds, RootViewController= initialViewController)

    override this.FinishedLaunching (app, options) =
        //This shows a possible way that actions can be assigned via a propery setter that is generated
        initialViewController.Mytouchup <- fun _ -> initialViewController.View.BackgroundColor <- UIColor.Red
        window.MakeKeyAndVisible ()
        true

module Main =
    UIApplication.Main ([||], null, "AppDelegate")