namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

type StaticHelpers() =
    static member InstantiateInitialViewController<'a when 'a :> NSObject>( storyboardName) =
        let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
        let sb = mainStoryboard.InstantiateInitialViewController ()
        let theType = sb.GetType()
        sb :?> 'a

//view controller is generated from the type provider and embedded into the assembly here
type myViewController = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard", Register = true>

//[<Register("cstestViewController")>]
//type sub =
//    inherit myViewController
//    new ()  = { inherit myViewController() } 
//    new (handle:nativeint) = { inherit myViewController(handle) } 
//
//    override x.ViewDidLoad() =
//        x.MyButton.TouchDown.Add (fun _ -> x.View.BackgroundColor <- UIColor.Green) 

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()
        
    let initialViewController = myViewController.CreateInitialViewController()

    let ivc = StaticHelpers.InstantiateInitialViewController("Main") :?> myViewController

    //this shows a possibility to access the outlets that are generated from the storyboard via the ViewDidLoadAction
    do initialViewController.ViewDidLoadAction <-
        fun () -> initialViewController.MyButton.TouchDown.Add (fun _ -> initialViewController.View.BackgroundColor <- UIColor.Blue) 

    let window = new UIWindow(UIScreen.MainScreen.Bounds, RootViewController= initialViewController)

    override this.FinishedLaunching (app, options) =
        //This shows a possible way that actions can be assigned via a propery setter that is generated
        initialViewController.Mytouchup <- fun _ -> initialViewController.View.BackgroundColor <- UIColor.Red
        window.MakeKeyAndVisible ()
        true

module Main =
    UIApplication.Main ([||], null, "AppDelegate")