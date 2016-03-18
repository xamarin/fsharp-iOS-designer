namespace CocoaApp
open System
open Foundation
open AppKit

type Container = Xamarin.UIProvider 

[<Register(Container.ViewControllerBase.CustomClass)>] 
type myViewController(handle) =
    inherit Container.ViewControllerBase(handle) 
    
        override x.ViewDidLoad () =
            base.ViewDidLoad ()
            // Do any additional setup after loading the view.
            //x.OnButton <- Some(fun _ -> x.View.Layer.BackgroundColor <- new CoreGraphics.CGColor("purple"))

        override x.RepresentedObject
            // Update the view, if already loaded.
            with get() = base.RepresentedObject
            and set(v) = base.RepresentedObject <- v
