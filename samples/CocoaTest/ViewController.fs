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
        // Access the action, apply
        x.OnButton <- Some(fun _ -> x.View.Window.BackgroundColor <- NSColor.Red)
        //access button outlet
        x.myButton.Title <- "test"
        //access label outlet
        x.myLabel.PlaceholderString <- "test"

    override x.RepresentedObject
        // Update the view, if already loaded.
        with get() = base.RepresentedObject
        and set(v) = base.RepresentedObject <- v
