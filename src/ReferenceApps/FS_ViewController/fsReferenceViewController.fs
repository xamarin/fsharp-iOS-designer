namespace cstest

open System
open System.Drawing

open MonoTouch.Foundation
open MonoTouch.UIKit

[<Register ("cstestViewController")>]
type fsReferenceViewController (ptr: nativeint) =
    inherit UIViewController (ptr)

    let mutable __mytouchup = Unchecked.defaultof<Action<UIButton>>

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true

    member x.OnClickUpAction
        with get() = __mytouchup
        and set v = __mytouchup <- v


    [<Action ("OnClickUp:")>]
    member x.OnClickUp ( sender: UIButton) =
        if __mytouchup <> null then __mytouchup.Invoke sender
