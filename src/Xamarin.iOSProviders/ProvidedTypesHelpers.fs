namespace Xamarin.UIProviders.DesignTime

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

module ProvidedTypes =
    module BindingFlags =
        let AllInstance = BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.NonPublic

    type CustomAttributeDataExt =
        static member Make(ctorInfo, ?args, ?namedArgs) = 
            { new CustomAttributeData() with 
                member __.Constructor =  ctorInfo
                member __.ConstructorArguments = defaultArg args [||] :> IList<_>
                member __.NamedArguments = defaultArg namedArgs [||] :> IList<_> }

    type Type with
        member x.GetConstructor(typ) =
            x.GetConstructor(BindingFlags.AllInstance, Type.DefaultBinder, [|typ|], [||] )

        member x.TryGetConstructor(typ:Type) =
            match x.GetConstructor(typ) with
            | null -> None
            | v -> Some v

        member x.GetUnitConstructor() =
            x.GetConstructor([||])

        member x.TryGetUnitConstructor() =
            match x.GetUnitConstructor() with
            | null -> None
            | v -> Some v

        member x.GetVirtualMethods() = 
            x.GetMethods (BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly) 
            |> Seq.filter (fun m -> m.IsVirtual) 

    type ProvidedTypes() =
        static member ProvidedPropertyWithField(fieldName, propertyName, typ, ?parameters: ProvidedParameter list) =
            
            let field = ProvidedField( fieldName, typ)
            field.SetFieldAttributes FieldAttributes.Private
            

            let property = ProvidedProperty(propertyName, typ, defaultArg parameters [])
            property.GetterCode <- fun args -> Expr.FieldGet(args.[0], field)
            property.SetterCode <- fun args -> Expr.FieldSet(args.[0], field, args.[1])

            field,property
            
    let mkProvidedLiteralField name (value: 'a) =
        ProvidedLiteralField(name, typeof<'a>, value)
        
    let mkProvidedStaticParameter name (value: 'a) =
        ProvidedStaticParameter(name, typeof<'a>, value)

    module Expr =
        /// This helper makes working with Expr.Let a little easier and safer
        let LetVar(varName, expr:Expr, f) =  
            let var = Var(varName, expr.Type)
            Expr.Let(var, expr, f (Expr.Var var))

        //creates an empty expression in the form of a unit or ()
        let emptyInvoke = fun _ -> <@@ () @@>

    module String =

        let private test_null =
            function
            | null -> raise (ArgumentNullException "arg")
            | _ -> ()

        let capitalize (s : string) =
            test_null s
            if s.Length = 0 then ""
            else (s.[0..0]).ToUpperInvariant () + s.[1 .. s.Length - 1]

        let uncapitalize (s : string) =
            test_null s
            if s.Length = 0 then ""
            else (s.[0..0]).ToLowerInvariant() + s.[1 .. s.Length - 1]
