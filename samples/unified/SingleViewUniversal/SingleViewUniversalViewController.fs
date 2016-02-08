namespace SingleViewUniversal

open System
open UIKit
open Foundation
open Xamarin.TypeProviders

//view controller is generated from the type provider and embedded into the assembly here
type SingleViewUniversalViewController =
    Xamarin.TypeProviders.iOS<AddRegisteration=true,
                              AbstractController=false,
                              AddDefaultConstructor=true,
                              CustomClass="SingleViewUniversalViewController"> 
                                             
type SingleViewUniversalViewController with
    override x.ToString() = "42"
    
    override x.DidReceiveMemoryWarning () = ()
        base.DidReceiveMemoryWarning () 

    override x.ViewDidLoad () = ()
        base.ViewDidLoad () 
   
    //override x.ShouldAutorotateTInterfaceOrientation (toInterfaceOrientation) =
    //    if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
    //       toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
    //    else
    //       true

