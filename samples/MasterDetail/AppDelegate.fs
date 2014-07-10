namespace MasterDetail

open System
open System.Collections.Generic
open MonoTouch.UIKit
open MonoTouch.Foundation
open Xamarin.iOSProviders

//view controller is generated from the type provider and embedded into the assembly here
type VCContainer = UIProvider<"MainStoryboard.storyboard">

[<AllowNullLiteral>]
type DataSource(controller:UITableViewController) =
    inherit UITableViewSource()
        
    let CellIdentifier = new NSString ("Cell")
    member val Objects = List<obj>() with get,set

    // Customize the number of sections in the table view.
    override x.NumberOfSections (tableView) = 1

    override x.RowsInSection (tableview, section) =
        x.Objects.Count

    // Customize the appearance of table view cells.
    override x.GetCell (tableView, indexPath) =         
        let cell = tableView.DequeueReusableCell (CellIdentifier, indexPath)
        cell.TextLabel.Text <- x.Objects.[indexPath.Row].ToString ()
        cell

    override x.CanEditRow (tableView, indexPath) =
        // Return false if you do not want the specified item to be editable.
        true

    override x.CommitEditingStyle (tableView, editingStyle, indexPath) =
        match editingStyle with
        | UITableViewCellEditingStyle.Delete ->
            // Delete the row from the data source.
            x.Objects.RemoveAt (indexPath.Row)
            controller.TableView.DeleteRows ([|indexPath|], UITableViewRowAnimation.Fade)
        | UITableViewCellEditingStyle.Insert -> ()
            // Create a new instance of the appropriate class, insert it into the array, and add a new row to the table view.
        | _ -> ()

    // Override to support rearranging the table view.
    override x.MoveRow (tableView, sourceIndexPath, destinationIndexPath) = ()

     // Override to support conditional rearranging of the table view.
    override x.CanMoveRow (tableView, indexPath) =     
        // Return false if you do not want the item to be re-orderable.
        true
  
[<Register(VCContainer.MasterViewControllerBase.CustomClass)>]
type MasterDetailController(iptr) as this =
    inherit VCContainer.MasterViewControllerBase(iptr, Title = NSBundle.MainBundle.LocalizedString ("Master", "Master"))
    let mutable dataSource : DataSource = null

    let addNewItem =
        (fun sender args ->
            dataSource.Objects.Insert (0, DateTime.Now)
            use indexPath = NSIndexPath.FromRowSection (0, 0)
            this.TableView.InsertRows ([|indexPath|], UITableViewRowAnimation.Automatic))

    override this.DidReceiveMemoryWarning() =
        base.DidReceiveMemoryWarning()

    override this.ViewDidLoad() =
        base.ViewDidLoad()
        this.NavigationItem.SetLeftBarButtonItem (this.EditButtonItem, false)
        let addButton = new UIBarButtonItem (UIBarButtonSystemItem.Add, EventHandler(addNewItem))
        this.NavigationItem.SetRightBarButtonItem (addButton, false)
        dataSource <- new DataSource(this)
        this.TableView.Source <- dataSource

    override this.PrepareForSegue(segue, sender) = 
        if segue.Identifier = "showDetail" then
            let indexPath = this.TableView.IndexPathForSelectedRow
            let item = dataSource.Objects.[indexPath.Row]
            dataSource <- new DataSource(this)
            this.TableView.Source <- dataSource

[<Register(VCContainer.DetailViewControllerBase.CustomClass)>]
type DetailViewController(handle) =
    inherit VCContainer.DetailViewControllerBase(handle)
    let mutable detailItem = null

    member this.SetDetailItem (newDetailItem) =
        if detailItem <> newDetailItem then
            detailItem <- newDetailItem
            // Update the view
            this.ConfigureView ()

    member this.ConfigureView () =
        // Update the user interface for the detail item
        if this.IsViewLoaded && detailItem <> null then
            this.DetailDescriptionLabel.Text <- detailItem.ToString ()
        
    override this.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
        
        // Perform any additional setup after loading the view, typically from a nib.
        this.ConfigureView ()

[<Register("AppDelegate")>]
type AppDelegate() = 
    inherit UIApplicationDelegate()
    
    override val Window = new UIWindow(UIScreen.MainScreen.Bounds) with get,set
    
    // This method should be used to release shared resources and it should store the application state.
    // If your application supports background exection this method is called instead of WillTerminate
    // when the user quits.
    override this.DidEnterBackground (application) = ()
                
    // This method is called as part of the transiton from background to active state.
    override this.WillEnterForeground (application) = ()

    // This method is called when the application is about to terminate. Save data, if needed.
    override this.WillTerminate (application) = ()

module Main = 
    [<EntryPoint>]
    let main args = 
        UIApplication.Main(args, null, "AppDelegate")
        0