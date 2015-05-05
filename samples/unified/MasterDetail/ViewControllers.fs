namespace fsharp_masterdetail

open System
open UIKit
open Foundation
open Xamarin.iOSProviders

//view controllers are generated from the type provider and embedded into the assembly here
type Container = UIProvider

type DataSource (controller:UITableViewController) =
    inherit UITableViewSource()
        
    let cellId = new NSString ("Cell")
    member val Items = ResizeArray<obj> () with get,set

    // Customize the number of sections in the table view.
    override x.NumberOfSections (tableView) = nint 1

    override x.RowsInSection (tableview, section) =
        nint x.Items.Count

    // Customize the appearance of table view cells.
    override x.GetCell (tableView, indexPath) =         
        let cell = tableView.DequeueReusableCell (cellId, indexPath)
        cell.TextLabel.Text <- x.Items.[indexPath.Row].ToString ()
        cell

    override x.CanEditRow (tableView, indexPath) =
        // Return false if you do not want the specified item to be editable.
        true

    override x.CommitEditingStyle (tableView, editingStyle, indexPath) =
        match editingStyle with
        | UITableViewCellEditingStyle.Delete ->
            // Delete the row from the data source.
            x.Items.RemoveAt indexPath.Row
            controller.TableView.DeleteRows ([|indexPath|], UITableViewRowAnimation.Fade)
        | UITableViewCellEditingStyle.Insert -> ()
            // Create a new instance of the appropriate class, insert it into the array, and add a new row to the table view.
        | _ -> ()

// Override to support rearranging the table view.
//    override x.MoveRow (tableView, sourceIndexPath, destinationIndexPath) = ()

// Override to support conditional rearranging of the table view.
//    override x.CanMoveRow (tableView, indexPath) =     
//        // Return false if you do not want the item to be re-orderable.
//        true
  
[<Register (Container.DetailViewControllerBase.CustomClass)>]
type DetailViewController (handle) =
    inherit Container.DetailViewControllerBase (handle)
    let mutable detailItem = null

    member x.SetDetailItem (newDetailItem) =
        if detailItem <> newDetailItem then
            detailItem <- newDetailItem
            // Update the view
            x.ConfigureView ()

    member x.ConfigureView () =
        // Update the user interface for the detail item
        if x.IsViewLoaded && detailItem <> null then
            x.DetailDescriptionLabel.Text <- detailItem.ToString ()
        
    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()
        // Perform any additional setup after loading the view, typically from a nib.
        x.ConfigureView ()

[<Register (Container.MasterViewControllerBase.CustomClass)>]
type MasterDetailController (handle) =
    inherit Container.MasterViewControllerBase (handle, Title = NSBundle.MainBundle.LocalizedString ("Master", "Master"))
    let mutable dataSource = Unchecked.defaultof<DataSource>

    member x.AddNewItem =
        EventHandler (fun _sender _args -> 
            dataSource.Items.Insert (0, DateTime.Now)
            use indexPath = NSIndexPath.FromRowSection (nint 0, nint 0)
            x.TableView.InsertRows ([|indexPath|], UITableViewRowAnimation.Automatic))

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()

        // Perform any additional setup after loading the view, typically from a nib.
        x.NavigationItem.LeftBarButtonItem <- x.EditButtonItem

        let addButton = new UIBarButtonItem (UIBarButtonSystemItem.Add, x.AddNewItem)
        x.NavigationItem.SetRightBarButtonItem (addButton, false)
        dataSource <- new DataSource(x)
        x.TableView.Source <- dataSource

    override x.PrepareForSegue (segue, sender) = 
        if segue.Identifier = "showDetail" then
            let indexPath = x.TableView.IndexPathForSelectedRow
            let item = dataSource.Items.[indexPath.Row]
            (segue.DestinationViewController :?> DetailViewController).SetDetailItem item