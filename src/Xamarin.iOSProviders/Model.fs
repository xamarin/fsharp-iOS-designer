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
               
type ViewController = {
    XmlType: string
    CustomClass : string
    Outlets: Outlet List
    Actions: Action List}
                       
type Scene = {
    ViewController : ViewController }