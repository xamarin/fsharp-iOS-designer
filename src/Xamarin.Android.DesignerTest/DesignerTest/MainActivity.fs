namespace DesignerTest

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Xamarin.Android.UIProvider.Runtime

//type container1 = Xamarin.Android.FragmentUI<"Test.axml"> 
type container2 = Xamarin.Android.ViewUI<"Test.axml"> 
//type container3 = Xamarin.Android.ActivityUI<"Test.axml"> 

type MyView =
    inherit container2
    //new() as x = { inherit container2()} then x.Initialise()
    new(context) as x = 
        { inherit container2(context) } then x.Initialise()
    new(context:Context, attr:Android.Util.IAttributeSet) as x =
        { inherit container2(context, attr) } then x.Initialise()
    
    member x.Initialise() =
       let space1 = x.space1
       let tableLayout1 = x.tableLayout1
       tableLayout1.SetBackgroundColor Android.Graphics.Color.Tomato

[<Activity (Label = "DesignerTest", MainLauncher=true, Icon="@mipmap/icon")>]
type MainActivity () =
    inherit Activity ()

    let mutable count:int = 1

    override this.OnCreate (bundle) =

        base.OnCreate (bundle)

        // Set our view from the "main" layout resource
        this.SetContentView (Resource_Layout.Main)

        // Get our button from the layout resource, and attach an event to it
        let button = this.FindViewById<Button>(Resource_Id.myButton)
        button.Click.Add (fun args -> 
            button.Text <- sprintf "%d clicks!" count
            count <- count + 1
            this.SetContentView(Resource_Layout.Test)
        )