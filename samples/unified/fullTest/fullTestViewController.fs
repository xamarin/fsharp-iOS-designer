namespace cstest

open System
open UIKit
open Foundation
open Xamarin

//view controller is generated from the type provider and embedded into the assembly here
type VCContainer = UIProvider

[<Register (VCContainer.cstestViewControllerBase.CustomClass) >]
type MyViewController (handle) =
    inherit VCContainer.cstestViewControllerBase (handle)

    //Overrides are implemented on the derived type   
    override x.ViewDidLoad() =
        //Access to the outlets are available
        x.myButton.TouchDown.Add (fun _ -> x.View.BackgroundColor <- UIColor.Purple)
        
        //Access to actions are available 
        x.Mytouchup <- Some(fun _ -> x.View.BackgroundColor <- UIColor.Yellow)

    override x.ShouldAutorotateToInterfaceOrientation (orientation) = 
        orientation <> UIInterfaceOrientation.PortraitUpsideDown

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()
        
    override val Window = new UIWindow(UIScreen.MainScreen.Bounds) with get,set

module Main =
    UIApplication.Main ([||], null, "AppDelegate")