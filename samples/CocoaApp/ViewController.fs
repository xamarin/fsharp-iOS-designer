namespace CocoaApp
open System
open Foundation
open AppKit

type Container = Xamarin.UIProvider 

[<Register(Container.ViewControllerBase.CustomClass)>] 
type myViewController(handle) =
    inherit Container.ViewControllerBase(handle)
    
    let onButton button =
        printfn "Button pressed"
            
    override x.ViewDidLoad () =
        base.ViewDidLoad ()
        // Do any additional setup after loading the view.
        x.OnButton <- Some(fun _ -> x.View.Window.BackgroundColor <- AppKit.NSColor.Red)

    override x.RepresentedObject
        // Update the view, if already loaded.
        with get() = base.RepresentedObject
        and set(v) = base.RepresentedObject <- v
