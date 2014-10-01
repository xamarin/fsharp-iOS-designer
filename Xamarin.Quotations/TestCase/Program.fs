namespace tt

open System
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.FSharp.Quotations.Patterns
open System.Collections.Generic

open Xamarin.Quotations

[<ReflectedDefinitionAttribute>]
module moduleTest =

    type test() =
        abstract Test : unit -> int
        default x.Test() = 42

    type test2(i : int) =
        inherit test()
        override x.Test() = i

    let returnFifteen() =
        let y = ref 10
        let add() =
            y := !y + 5
    
        add()
        !y

    let myTest =
        let mutable total = 100
        total <- 101
        let test = ref 0
        for i in 1..total do
            test := !test + 50
        !test

    let free() =
        let t = test()
        t.Test() + 12
    let final = free()

    let free2 i =
        let t = test2(i)
        t.Test() + 12
    let final2 = free2 10

    let mutable mark = 0.0
    for i in 1..10 do
        mark <- sin (float i / 5.0)

    let complete = ()

module start =

    let testAssembly = LoadedAssembly(Assembly.GetExecutingAssembly())

    let store = List<string * DateTime * obj>()
    let notify (name, x) = 
      store.Add (name, DateTime.Now, box x)
      x

    let wrapMethInfo t =
        let moduleType = Type.GetType("tt.start")
        let notifyMethodInfo = moduleType.GetMethod("notify")
        let concreteNotify = notifyMethodInfo.MakeGenericMethod [|t|]
        concreteNotify

    let methodIs typeFullName name (mi : MethodInfo) =
        (mi.DeclaringType.FullName = typeFullName && mi.Name = name)

    let notifyExpr name t e = Expr.Call(wrapMethInfo t, [ Expr.Value(name); e ])
    let notifyVar (v : Var) e = notifyExpr v.Name v.Type e
    let notifyProp (p : PropertyInfo) e = notifyExpr p.Name p.PropertyType e
 
    let subs (methd : MethodBase) =
        match Expr.TryGetReflectedDefinition(methd) with
        | None -> None
        | Some(quotation) ->
            let rec visit = function
                | VarSet(var, value) as set -> Expr.VarSet(var, notifyVar var value)
                | Let(var, value, body) -> Expr.Let(var, notifyVar var value, visit body)

                //| DerivedPatterns.SpecificCall <@ (:=) @>(None, [typ], [Var(var) as varExpr; assignment] ) as sc ->
                | Call(None, mi, (Var(var) as varExpr) :: value :: rest) when mi |> methodIs "Microsoft.FSharp.Core.Operators" "op_ColonEquals" ->
                    let typ = mi.GetGenericArguments().[0]
                    let notify = notifyExpr var.Name typ value
                    Expr.Call(mi, [ varExpr; notify ])

                | ShapeVar v -> Expr.Var(v)
                | ShapeLambda (v,expr) -> Expr.Lambda(v, visit expr)
                | ShapeCombination (o, exprs) -> RebuildShapeCombination(o,List.map visit exprs)
            Some (visit quotation)

    testAssembly |> Asm.instrument subs
    testAssembly.Save("instrumented.exe")