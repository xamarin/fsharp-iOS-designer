namespace singleview_tvOS

open System
open Foundation
open UIKit

type container = Xamarin.UIProvider 

[<Register(container.myViewControllerBase.CustomClass)>]
type ViewController(handle : IntPtr) =  
    inherit container.myViewControllerBase(handle)

    override x.DidReceiveMemoryWarning() = 
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning()
        
    // Release any cached data, images, etc that aren't in use.
    override x.ViewDidLoad() =
        x.DoNotPressAction <- Some (fun sender -> x.View.BackgroundColor <- UIColor.Purple
                                                  x.myLabel.Text <- "Bang!" )
        base.ViewDidLoad()
        
        
// Perform any additional setup after loading the view, typically from a nib.

