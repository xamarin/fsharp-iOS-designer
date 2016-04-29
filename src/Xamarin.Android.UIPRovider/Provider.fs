namespace Xamarin.Android.UIProvider
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Xml.Linq
open System.Reflection
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open FSharp.Quotations
open Android
open Android.Views
open ExtCore

module MoreExt =
    type Text.StringBuilder with
        member x.Yield (_) = x
        [<CustomOperation("append")>]
        member x.append (sb:Text.StringBuilder, str:string) = sb.Append str
        [<CustomOperation("appendLine")>]
        member x.appendLine (sb:Text.StringBuilder, str:string) = sb.AppendLine str
        member x.Run sb = sb.ToString()

[<AutoOpen>]
module Extensions =
    type BindingFlags with
      static member PublicStatic = BindingFlags.Public ||| BindingFlags.Static
      static member All = BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.NonPublic
      
    open Quotations.Patterns
    let tryGetMi e = Option.map (fun (_,mi,_) -> mi) ((|Call|_|) e)
    type ProvidedConstructor with

        member x.Yield (_) = x

        [<CustomOperation("invokeCode")>]
        member x.invokeCode (p:ProvidedConstructor, f) = p.InvokeCode <- f;p
        
        [<CustomOperation("baseConstructorCall")>]
        member x.baseConstructorCall (p:ProvidedConstructor, f) = p.BaseConstructorCall <- f;p
    
module File =
    let log str= Console.WriteLine(str:string)

    //TODO switch watcher to a type and implement IDisposable and make Invalidate an observable
    //This would mean that openWithWatcher would return a watcher that could be used to subscribe to invalidate
    //changes rather than pass in the TP's Invalidate method in. This is more or less the pattern used by the FileSystemProvider.   
    let watch path invalidator =
        let getLastWrite() = File.GetLastWriteTime path 
        let lastWrite = ref (getLastWrite())

        let watcher =
            let path = Path.GetDirectoryName path
            let name = Path.GetFileName path
            new FileSystemWatcher(Filter=name, Path=path, EnableRaisingEvents=true)

        let checkForChanges _ =
            let curr = getLastWrite()
            if !lastWrite <> curr then
                log ("Invalidated " + path)
                lastWrite := curr
                invalidator()
        do
            watcher.Changed.Add checkForChanges
            watcher.Renamed.Add checkForChanges
            watcher.Deleted.Add checkForChanges
        watcher :> IDisposable
        
type Model =
    {ElementName : string; Id : string; Children : Model list }
    member x.MappingName = "android/widget/" + x.ElementName
    
type Scene =
    {FileName : string; Elements : Model list}
 
//type Reflection private () =
//    static let CallMethodInfo = 
//        let flags = BindingFlags.NonPublic ||| BindingFlags.Static
//        typeof<Reflection>.GetMethod("DoUnbox", flags).GetGenericMethodDefinition()

//    static member private DoUnbox<'T>(value: obj) =
//        let value: 'T = unbox value
//        value

//    static member Unbox(value: obj, typeOfValue: Type) =
//        CallMethodInfo.MakeGenericMethod(typeOfValue).Invoke(null, [|value|]) :?> _

type Reflection =
    static member getSlice<'a when 'a :> View> (instance:Expr) (literalField:FieldInfo)=
        match <@@  (null : View).FindViewById<'a>(0) @@> with
        | Patterns.Call(_,mi,_) -> Expr.Call(Expr.Coerce(instance, typeof<View>), mi, [Expr.FieldGet(literalField)])
        | q -> failwithf "Bad quotation %A" q
      
      //<@@ (%%coercedInstance : View).FindViewById<'a>(%%getField) @@>
      //<@@ (%%Expr.Coerce(instance, typeof<View>) : View).FindViewById<'a>(%%Expr.FieldGet(literalField)) @@>

    static member lazyFieldGen<'a when 'a : null     and
                                       'a : equality and
                                       'a :> View> (instance:Expr, controlField:Expr, controlFieldInfo:FieldInfo, literalFieldId:FieldInfo) =
        let controlField = Expr.Cast<'a>(controlField)
        let lf = Reflection.getSlice<'a> instance literalFieldId

        <@@ match %controlField with
            | null ->
                %%Expr.FieldSet(instance, controlFieldInfo, lf)
                %controlField
            | other -> other @@>
            

[<AbstractClass>]
type AndroidDesignerProvider<'T>(config: TypeProviderConfig, ns, name) as this = 
    inherit TypeProviderForNamespaces()
    let basepath = @"/Library/Frameworks/Xamarin.Android.framework/Versions/6.1.99-76/lib/xbuild-frameworks/MonoAndroid/v6.0/"
    let runtimePath = @"/Library/Frameworks/Xamarin.Android.framework/Versions/6.1.99-76/lib/mono/2.1/"
    let assemblyName = "Mono.Android.dll"

    do
      this.RegisterProbingFolder runtimePath
      this.RegisterProbingFolder basepath
    //TODO add folder properly

    let asm = Assembly.GetExecutingAssembly()
    let rootType = ProvidedTypeDefinition(asm, ns, name, None, HideObjectMethods=true, IsErased=false)
    let watchedFiles = ResizeArray<IDisposable>()
    
    let androidTypeMap = 
        let androidAssembly =
            Assembly.LoadFrom(Path.Combine(basepath, assemblyName))

        androidAssembly.ExportedTypes
        |> Seq.choose (fun t -> t.GetCustomAttribute<Android.Runtime.RegisterAttribute>()
                                |> Option.ofObj
                                |> Option.map (fun registerAttrib -> registerAttrib.Name, (registerAttrib, t)))
        |> Map.ofSeq

    let cleanupId (id:string) = id.Replace("@+id/", "")
        
    //CodeDom.Compiler.GeneratedCodeAttribute("Xamarin.Android", "1.0")
    let mkGeneratedCodeAttribute tool version =
        { new CustomAttributeData() with 
            member __.Constructor = typeof<CodeDom.Compiler.GeneratedCodeAttribute>.GetConstructors().[0]
            member __.ConstructorArguments =
                upcast [| CustomAttributeTypedArgument(typeof<string>, tool); CustomAttributeTypedArgument(typeof<string>, version) |]
            member __.NamedArguments = upcast [| |] }
            
    
    let getResourceModel filename =
        let resourceModel = Resources.getModel filename
        //build provided model for resource IDS so they can be used in the
        // provided types to retrieve resource id's properly
        let generatedResource = 
            match resourceModel.Model with
            | Some(model) ->
                let resourceType = ProvidedTypeDefinition("Resource", Some(typeof<obj>), IsErased=false)
                let idLookup = Dictionary<_,_>()
                for KeyValue(key,value) in model do
                    let group = ProvidedTypeDefinition(key, Some(typeof<obj>), IsErased=false)
                    for {Name=item; Id= value} in value do
                        let plf = ProvidedLiteralField(item, typeof<int>, value)
                        if key = "id" then idLookup.[item] <- plf
                        group.AddMember plf
                        
                    resourceType.AddMember group   
                Some(resourceType, idLookup)
            | _ -> None
        generatedResource
    
    let buildTypes typeName (parameterValues: obj []) =
        let xn = XName.op_Implicit
        let designerFile = parameterValues.[0] :?> string

        let foundFiles =
            Directory.EnumerateFiles(config.ResolutionFolder, designerFile, SearchOption.AllDirectories)
    
        let rec procesElements (elements:XElement seq) =
            [
                for element in elements do
                    
                    let attrib = element.Attribute( XName.Get("id", "http://schemas.android.com/apk/res/android")) |> Option.ofObj
                    match attrib with
                    | Some(id) ->
                        let children = procesElements (element.Elements())
                        yield { ElementName = element.Name.LocalName
                                Id = cleanupId id.Value
                                Children = children }
                    | None ->
                        () ]
            
        let scenes = 
            [    for designerFile in foundFiles do

                    let xdoc, watcherDisposer =
                        use sr = new StreamReader(designerFile, true)
                        let xdoc = XDocument.Load(sr)
                        xdoc, File.watch designerFile this.Invalidate
                    
                    let processedElements = procesElements (xdoc.Root.Elements())
                    let sceneModel = {FileName=designerFile;Elements=processedElements}                                        
                    watchedFiles.Add watcherDisposer

                    yield sceneModel ]
                    
        let propertyGetterCode (resourceModel: (ProvidedTypeDefinition * Dictionary<string,ProvidedLiteralField>) option) fieldinfo key (parameters : Expr list) =
            
            let instance = parameters.[0]

            let literalField =
                match resourceModel with
                | Some(ptd, resources) ->
                    match resources.TryGetValue key with
                    | true, v -> v
                    | false, _ -> failwith "Could not find matching resource model"
                | None _ -> failwith "Could not find matching resource model"
            
            let propexpr =
                let field = Expr.FieldGet(instance ,fieldinfo)
                let mi  = match <@ Reflection.lazyFieldGen (<@()@>, <@()@>, null, null) @> with Patterns.Call(_,mi,_) -> mi | q -> failwithf "Could not find method for %A" q
                let gmethodDef = mi.GetGenericMethodDefinition()
                let gmethod = gmethodDef.MakeGenericMethod(fieldinfo.FieldType)
                gmethod.Invoke(null, [|instance; field; fieldinfo; literalField|]) :?> Expr
                
                //let method' = typeof<Reflection>.GetMethod("lazyFieldGen", BindingFlags.PublicStatic).GetGenericMethodDefinition()
                //let gmethod = method'.MakeGenericMethod(fieldinfo.FieldType)
                //gmethod.Invoke(null, [|instance;field;fieldinfo;literalField|]) :?> Expr
                
            propexpr
            
        let generatedCodeAttribute = mkGeneratedCodeAttribute "Xamarin.Android.UIProvider" "0.1"
            
        let createUIMembers resourceModel (model:Model) =
            let registerAttrib, mappedType = androidTypeMap.[model.MappingName]
            
            let field = ProvidedField("_" + model.Id, mappedType)
            field.AddCustomAttribute generatedCodeAttribute
            
            let prop = ProvidedProperty(model.Id, mappedType, GetterCode = (propertyGetterCode resourceModel field model.Id))
            prop.AddCustomAttribute generatedCodeAttribute
            [field :> MemberInfo
             prop  :> _ ]

        let resourceModel =
            match Directory.EnumerateFiles(config.ResolutionFolder, "R.java", SearchOption.AllDirectories) |> Seq.tryHead with
            | Some file -> getResourceModel file
            | _ -> failwithf "Java resource file not found in resolution folder path: %s" config.ResolutionFolder
        

                
        let providedElements =
            let rec loop (model:Model) =
                [yield! createUIMembers resourceModel model
                 for child in model.Children do
                     yield! loop child ]

            [for scene in scenes do
                for model in scene.Elements do
                    yield! loop model ]

        //create a container to store types
        let container = ProvidedTypeDefinition(asm, ns, typeName, Some(typeof<'T>), IsErased=false)
        container.SetAttributes (TypeAttributes.Abstract ||| TypeAttributes.Public)
        container.AddMembers providedElements
        
        resourceModel |> Option.iter (fst >> container.AddMember)

        let addDefaultCtor (t:ProvidedTypeDefinition) =
            let defaultCtor = ProvidedConstructor([], InvokeCode=fun _ -> <@@ () @@>)
            t.AddMember defaultCtor
            
        match typeof<'T> with
        | t when t = typeof<Android.App.Fragment> -> addDefaultCtor container
        | t when t = typeof<Android.App.Activity> -> addDefaultCtor container
        | t when t = typeof<Android.Views.View> ->
            let viewType = typeof<Android.Views.View>
            
            let contextType = typeof<Android.Content.Context>
            let baseCtor = viewType.GetConstructor(BindingFlags.All, Type.DefaultBinder, [|contextType|], [||] )
            let ctor = ProvidedConstructor([ProvidedParameter( "context", contextType)]){
                           baseConstructorCall (fun args -> baseCtor, args)
                           invokeCode (fun _ -> <@@ () @@> ) }
             
            let attrType = typeof<Android.Util.IAttributeSet>
            let baseCtor2 = viewType.GetConstructor(BindingFlags.All, Type.DefaultBinder, [|contextType; attrType|], [||] )                                      
            let ctor2 = ProvidedConstructor([ProvidedParameter( "context", contextType)
                                             ProvidedParameter( "attrs", attrType)]){
                           baseConstructorCall (fun args -> baseCtor2, args)
                           invokeCode (fun _ -> <@@ () @@> ) }

            container.AddMembers [ctor;ctor2]
        | other -> failwithf "Unknown type %A" other
        
        //pump types into the correct assembly
        let assembly = ProvidedAssembly(Path.ChangeExtension(Path.GetTempFileName(), ".dll"))
        assembly.AddTypes [container]
        container

    let axmlFilesParam =
        let param = ProvidedStaticParameter("axml", typeof<string>)
        param.AddXmlDoc "The axml designer file you wish to create types for"
        param
        
    do  rootType.DefineStaticParameters([axmlFilesParam], buildTypes)
        this.AddNamespace(ns, [rootType])
        this.Disposing.Add (fun _ -> for disposer in watchedFiles do disposer.Dispose ())
        
[<TypeProvider>]
type AndroidFragmentUIProvider(config: TypeProviderConfig) =
    inherit AndroidDesignerProvider<Android.App.Fragment>(config, "Xamarin.Android", "FragmentUI")

[<TypeProvider>]
type AndroidViewUIProvider(config: TypeProviderConfig) =
    inherit AndroidDesignerProvider<Android.Views.View>(config, "Xamarin.Android", "ViewUI")
    
[<TypeProvider>]
type AndroidActivityUIProvider(config: TypeProviderConfig) =
    inherit AndroidDesignerProvider<Android.App.Activity>(config, "Xamarin.Android", "ActivityUI")


