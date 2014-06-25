namespace cstest

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

//view controller is generated from the type provider and embedded into the assembly here
//type myViewController = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard", IsRegistered = true>
type myViewControllerBase = Xamarin.iOSProviders.UIProvider<"../StoryBoards/Main.storyboard", IsRegistered = false, IsAbstract=true>

[<Register"cstestViewController">]
type sub(handle) =
    inherit myViewControllerBase(handle)

    override x.ViewDidLoad() =
        x.MyButton.TouchDown.Add (fun _ -> x.View.BackgroundColor <- UIColor.Purple) 


    // Return true for supported orientations
    override x.ShouldAutorotateToInterfaceOrientation (orientation) =
        orientation <> UIInterfaceOrientation.PortraitUpsideDown

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

//    [<Register("cstestViewController")>]
//    let initialViewController(a) =
//        { new myViewControllerBase(a) with 
//          override x.ViewDidLoad() = x.View.BackgroundColor <- UIColor.Orange}

    //this shows a possibility to access the outlets that are generated from the storyboard via the ViewDidLoadAction
    //do initialViewController.ViewDidLoadAction <-
    //    fun () -> initialViewController.MyButton.TouchDown.Add (fun _ -> initialViewController.View.BackgroundColor <- UIColor.Blue) 

    override val Window = new UIWindow(UIScreen.MainScreen.Bounds) with get,set

    override this.FinishedLaunching (app, options) =
        (this.Window.RootViewController :?> sub).Mytouchup <- fun _ -> this.Window.RootViewController.View.BackgroundColor <- UIColor.Red
        //This shows a possible way that actions can be assigned via a propery setter that is generated
        //initialViewController.Mytouchup <- fun _ -> initialViewController.View.BackgroundColor <- UIColor.Red
        //window.MakeKeyAndVisible ()
        true

module Main =
    UIApplication.Main ([||], null, "AppDelegate")