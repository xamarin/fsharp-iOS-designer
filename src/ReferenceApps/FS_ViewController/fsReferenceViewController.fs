namespace cstest

open System
open System.Drawing

open MonoTouch.Foundation
open MonoTouch.UIKit

[<Register ("cstestViewController")>]
type fsReferenceViewController () =
    inherit UIViewController ()

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

//    override x.ViewDidLoad () =
//        base.ViewDidLoad ()
//        // Perform any additional setup after loading the view, typically from a nib.
//        x.View.BackgroundColor <- UIColor.Red

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true