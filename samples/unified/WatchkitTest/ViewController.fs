namespace Xamarin.iOSProviders.UIProvider

open System
open System.Drawing

open Foundation
open UIKit
open Xamarin.iOSProviders

//view controller is generated from the type provider and embedded into the assembly here
type VCContainer = UIProvider<AbstractController=false>
type VCContainer with
  member x.Test () = 172
//  override x.ToString() = ""

type VCContainer.ViewController with
  member x.ToString()  = "172"

[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
    inherit UIViewController (handle)

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()
        // Perform any additional setup after loading the view, typically from a nib.

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true

