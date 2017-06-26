namespace SingleViewUniversal

open System
open UIKit
open Foundation

//view controller is generated from the type provider and embedded into the assembly here
type VCContainer = Xamarin.UIProvider

[<Register (VCContainer.SingleViewUniversalViewControllerBase.CustomClass) >]
type MyViewController (handle) =
    inherit VCContainer.SingleViewUniversalViewControllerBase (handle) 

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()
        x.myButton.TouchUpInside.Add(fun _ -> x.View.BackgroundColor <- UIColor.Blue)

        // Perform any additional setup after loading the view, typically from a nib.

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true

