namespace Xamarin.Quotations

open System
open System.Reflection

open Microsoft.FSharp.Quotations

open Mono.Cecil
open Mono.Cecil.Cil
open Mono.Cecil.Rocks

type LoadedAssembly(reflectionAsm : Assembly) =
    let location = reflectionAsm.Location
    member val Reflection = reflectionAsm
    member val Cecil = AssemblyDefinition.ReadAssembly(location)
    member x.Save(loc : string) = x.Cecil.Write(loc)
    member x.Save() = x.Save(location)

module Asm =
    type Type with
        member x.ToTypeDefinition(m : ModuleDefinition) = m.LookupToken(x.MetadataToken) :?> TypeDefinition
    type MethodBase with
        member x.ToMethodDefinition(m : ModuleDefinition) = m.LookupToken(x.MetadataToken) :?> MethodDefinition

    let instrument (func : MethodBase -> Expr option) (asm : LoadedAssembly) =
        let mutable i = 0
        let mutable cecilModule = asm.Cecil.Modules.[i]
        for reflectionType in asm.Reflection.GetTypes() do
            let mutable typeDef = reflectionType.ToTypeDefinition cecilModule
            while typeDef = null do
                i <- (i + 1) % asm.Cecil.Modules.Count
                cecilModule <- asm.Cecil.Modules.[i]
                typeDef <- reflectionType.ToTypeDefinition cecilModule
            let bindingFlags = BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly
            for reflectionMethod in reflectionType.GetMethods(bindingFlags) do
                let cecilMethod = reflectionMethod.ToMethodDefinition cecilModule
                if cecilMethod <> null && cecilMethod.HasBody then
                    match func(reflectionMethod) with
                    | None -> ()
                    | Some(expr) ->
                        printfn "Instrumenting %O to %O" reflectionMethod expr
                        cecilMethod.Body.Instructions.Clear()
                        expr |> Quotation.compile cecilMethod typeDef
