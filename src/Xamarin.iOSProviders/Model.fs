namespace Xamarin.UIProviders.DesignTime
open System
open System.IO
open System.Xml
open System.Xml.Linq

type Outlet = {
    Property:string
    ElementName: string }
    
type Action = {
    Selector:string
    ElementName: string }

type View = {
    XmlType: string
    CustomClass : string
    Outlets: Outlet list
    SubViews : View list}
               
type ViewController = {
    XmlType: string
    CustomClass : string
    Outlets: Outlet list
    Actions: Action list
    View : View option}

type Scene = {
    ViewController : ViewController }