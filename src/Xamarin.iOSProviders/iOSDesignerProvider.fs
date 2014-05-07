namespace Xamarin.iOSProviders

open System
open System.IO
open System.Reflection
open System.Linq
open System.Xml.Linq
open System.Collections.Generic
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations      
open MonoTouch.Foundation
open MonoTouch.UIKit
open Microsoft.FSharp.Compatibility.OCaml
open Swensen.Unquote
open Swensen.Unquote.Extensions
open Swensen.Unquote.Operators

module Option =
    let fromBoolAndOut (success,value) =
        if success then Some(value) else None

module Sanitise =
    let makeFieldName (name:string) = 
        "__" +
        (name |> String.uncapitalize |> String.trimEnd [|':'|])
    
    let makePropertyName (name:string) = 
        name |> String.capitalize |> String.trimEnd [|':'|]

    let makeMethodName (name:string) = 
        (name |> String.capitalize |> String.trimEnd [|':'|])  + "Selector"

module Expr =

    /// This helper makes working with Expr.Let a little easier and safer
    let LetVar(varName, expr:Expr, f) =  
        let var = Var(varName, expr.Type)
        Expr.Let(var, expr, f (Expr.Var var))
    //creates an empty expression in the form of a unit or ()
    let emptyInvoke = fun _ -> <@@ () @@>

module BindingFlags =
    let publicInstance = BindingFlags.Public ||| BindingFlags.Instance

[<AutoOpenAttribute>]
module TypeExt =
    type Type with
        member x.GetConstructor(tt) =
            x.GetConstructor([|tt|])
        member x.GetUnitConstructor() =
            x.GetConstructor([||])         
          
type CustomAttributeDataExt =
    static member Make(ctorInfo, ?args, ?namedArgs) = 
        #if FX_NO_CUSTOMATTRIBUTEDATA
        { new IProvidedCustomAttributeData with 
        #else
        { new CustomAttributeData() with 
        #endif
            member __.Constructor =  ctorInfo
            member __.ConstructorArguments = defaultArg args [||] :> IList<_>
            member __.NamedArguments = defaultArg namedArgs [||] :> IList<_> }
         
module Attributes =
    let MakeActionAttributeData(argument:string) = 
        CustomAttributeDataExt.Make(typeof<ActionAttribute>.GetConstructor(typeof<string>),
                                    [| CustomAttributeTypedArgument(typeof<ActionAttribute>, argument) |])
            
    let MakeRegisterAttributeData(argument:string) = 
        CustomAttributeDataExt.Make(typeof<RegisterAttribute>.GetConstructor(typeof<string>),
                                    [| CustomAttributeTypedArgument(typeof<string>, argument) |])

    let MakeOutletAttributeData() = 
        CustomAttributeDataExt.Make(typeof<OutletAttribute>.GetUnitConstructor())

[<TypeProvider>] 
type iOSDesignerProvider(config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    //TODO figure out how to find this properly, must be a env var or something.
    let assemblybase = "/Developer/MonoTouch/usr/lib/mono/2.1/"

    do this.RegisterProbingFolder assemblybase    

    let ns = "Xamarin.iOSProviders"
    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, "UIProvider", None, HideObjectMethods = true, IsErased = false)
    let watchedFile = ref  Unchecked.defaultof<_>
    let buildTypes typeName (parameterValues: obj []) =
        let designerFile =
            let filename = parameterValues.[0] :?> string
            if Path.IsPathRooted filename then Uri(filename)
            else Uri(Path.Combine [|config.ResolutionFolder; filename |])

        let stream, watcherDisposer = IO.openWithWatcher designerFile this.Invalidate
        watchedFile := watcherDisposer
        let xdoc = XDocument.Load(stream)
        stream.Dispose()

        //TODO try to use MonoTouch.Design parsing, extract the models action/outlets etc
        //let parsed = MonoTouch.Design.ClientParser.Instance.Parse(xdoc.Root)

        //TODO: support multiple view controllers
        let viewControllerElement = xdoc.Descendants(Xml.xn "viewController").Single()

        let actions = 
            viewControllerElement.Descendants(Xml.xn "action") 
            |> Seq.map IosAction.Parse

        let outlets =
            viewControllerElement.Descendants(Xml.xn "outlet") 
            |> Seq.map Outlet.Parse |> Seq.toArray

        let viewController = ViewController.Parse(viewControllerElement)

        // Generate the required type
        let viewControllerType = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<UIViewController>), IsErased = false )
        let ctorInfo = typeof<UIViewController>.GetConstructor(typeof<IntPtr>)
        let ctor = ProvidedConstructor([ProvidedParameter("handle", typeof<IntPtr>)], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> ctorInfo, args)
        viewControllerType.AddMember(ctor)

        let emptyctorInfo = typeof<UIViewController>.GetUnitConstructor()
        let emptyctor = ProvidedConstructor([], InvokeCode=Expr.emptyInvoke, BaseConstructorCall = fun args -> emptyctorInfo, args)
        viewControllerType.AddMember(emptyctor)

                                                                             
        let register = Attributes.MakeRegisterAttributeData viewController.customClass
        viewControllerType.AddCustomAttribute(register)

        //actions mutable assignment style----------------------------
        //TODO add option for ObservableSource<NSObject>
        for action in actions do
            //create a backing field for the action
            let actionField = ProvidedField(Sanitise.makeFieldName action.selector, typeof<Action<NSObject>>)
            actionField.SetFieldAttributes FieldAttributes.Private

            //create a property for the action
            let actionProperty =
                ProvidedProperty(Sanitise.makePropertyName action.selector, typeof<Action<NSObject>>,
                                 GetterCode = (fun args -> Expr.FieldGet(args.[0], actionField)),
                                 SetterCode = fun args -> Expr.FieldSet(args.[0], actionField, args.[1]))
            
            let actionBinding =
                ProvidedMethod(methodName=Sanitise.makeMethodName action.selector, 
                               parameters=[ProvidedParameter("sender", typeof<NSObject>)], 
                               returnType=typeof<Void>, 
                               InvokeCode = fun args -> let instance = Expr.Cast<Action<NSObject>>(Expr.FieldGet(args.[0], actionField))
                                                        <@@ if %instance <> null then (%instance).Invoke(%%args.[1]) @@>)

            actionBinding.AddCustomAttribute(Attributes.MakeActionAttributeData(action.selector))
            actionBinding.SetMethodAttrs MethodAttributes.Private

            viewControllerType.AddMember actionField
            viewControllerType.AddMember actionProperty
            viewControllerType.AddMember actionBinding
        //end actions-----------------------------------------

        let makeReleaseOutletsExpr (instance: Expr) (outlets:(Expr -> Expr) array)=
            match outlets with
            | [|single|] -> single instance
            | lots -> lots 
                      |> Array.map (fun o -> o instance) 
                      |> Array.reduce (fun one two -> Expr.Sequential(one, two))

        //outlets-----------------------------------------
        let providedOutlets = 
            outlets
            |> Array.map (fun outlet ->
                let backingField = ProvidedField(Sanitise.makeFieldName outlet.Name + "Outlet", outlet.Type)

                let property = ProvidedProperty(Sanitise.makePropertyName outlet.Name + "Outlet", outlet.Type)

                property.GetterCode <- fun args -> Expr.FieldGet(args.[0], backingField)
                property.SetterCode <- fun args -> Expr.FieldSet(args.[0], backingField, args.[1])
                property.AddCustomAttribute <| Attributes.MakeOutletAttributeData()

                ///takes an instance returns a disposal expresion
                let disposal(instance) =
                    let get = Expr.FieldGet(instance, backingField)
                    let field = Expr.Coerce(get, typeof<obj>)
                    <@@ if %%field <>  null then
                           ((%%field:obj) :?> IDisposable).Dispose() @@>

                //This is Expr equivelent of the above
                //let operators = Type.GetType("Microsoft.FSharp.Core.Operators, FSharp.Core")
                //let intrinsicFunctions = Type.GetType("Microsoft.FSharp.Core.LanguagePrimitives+IntrinsicFunctions, FSharp.Core")
                //let inequality = operators.GetMethod("op_Inequality")
                //let genineqtyped = ProvidedTypeBuilder.MakeGenericMethod(inequality, [typeof<obj>;typeof<obj>])
                //
                //let unboxGenericMethod = intrinsicFunctions.GetMethod("UnboxGeneric")
                //let unboxGenericMethodTyped = ProvidedTypeBuilder.MakeGenericMethod(unboxGenericMethod, [typeof<IDisposable>])
                //
                //let disposeMethod = typeof<IDisposable>.GetMethod("Dispose")
                //
                //
                //let coerceToObj = Expr.Coerce(get, typeof<obj>)
                //let guard = Expr.Call(genineqtyped, [coerceToObj; Expr.Value(null) ])
                //let trueblock = Expr.Call(Expr.Call(unboxGenericMethodTyped, [Expr.Coerce(get, typeof<obj>)]), disposeMethod, [])
                //
                //Expr.IfThenElse(guard, trueblock, <@@ () @@>)

                //Add the property and backing fields to the view controller
                viewControllerType.AddMember backingField
                viewControllerType.AddMember property

                disposal)       





        let releaseOutletsMethod = ProvidedMethod("ReleaseDesignerOutlets", [], typeof<Void>, 
                                                  InvokeCode = fun args -> if Array.isEmpty providedOutlets then
                                                                                <@@ () @@>
                                                                           else
                                                                                let code = makeReleaseOutletsExpr args.[0] providedOutlets
                                                                                let decompiled = code.Decompile()
                                                                                code)
        viewControllerType.AddMember releaseOutletsMethod
        //outlets-----------------------------------------

        //pump types into the correct assembly
        let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        assembly.AddTypes [viewControllerType]

        viewControllerType

    do  rootType.DefineStaticParameters([ProvidedStaticParameter("DesignerFile", typeof<string>)], buildTypes) 
    do this.AddNamespace(ns, [rootType])

    interface IDisposable with
        member x.Dispose() =
            if !watchedFile <> null then (!watchedFile).Dispose()

[<assembly:TypeProviderAssembly()>] 
do()