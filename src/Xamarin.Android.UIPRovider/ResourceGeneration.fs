namespace Xamarin.Android.UIProvider
open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Text.RegularExpressions


module Resources  =
    type Id = {Name:string; Id:int}
    type ResourceModel = Dictionary<string, Id list> 
    type State =
        {HasMainResource:bool; CurrentType: string option; Model : ResourceModel option}
        static member Empty = {HasMainResource=false;CurrentType=None; Model=None}
    let (|GetName|) (name:string) =
        match name with
        | "anim" -> "Animation"
        | "attr" -> "Attribute"
        | "bool" -> "Boolean"
        | "dimen" -> "Dimension"
        | other -> string (Char.ToUpperInvariant (name.[0])) + name.[1..]

    let (|CompiledMatch|_|) pattern input =
        if input = null then None else
        let m = Regex.Match(input, pattern, RegexOptions.Compiled)
        if m.Success then
            Some [for x in m.Groups do if x.Success then yield x]
        else None
            
    let (|Resource|_|) = (|CompiledMatch|_|) "^public final class R {"
    
    let (|Type|_|) = function
        | CompiledMatch "^    public static final class ([^ ]+) {$" group ->
            match group with
            | [_line;typ] when not (String.IsNullOrEmpty typ.Value) -> Some(typ.Value)
            | _ -> None
        | _ -> None
    
    let (|Id|_|) = function
        | CompiledMatch @"^        public static final int ([^ =]+)\s*=\s*([^;]+);$" group ->
            match group with
            | [_line; name; id] when
                not (String.IsNullOrEmpty name.Value) &&
                not (String.IsNullOrEmpty id.Value) -> Some(name.Value, id.Value)
            | _ -> None
        | _ -> None

    let private foldModel state line =
        match line with
        | Resource _group -> {state with HasMainResource=true}
        | Type typ when state.HasMainResource -> {state with CurrentType = Some(typ)}
        | Id (name, value) ->
            match state with
            | { HasMainResource = true; CurrentType = Some(typ) } ->
               let model = match state.Model with Some(m) -> m | _ -> ResourceModel()
               match model.TryGetValue typ with
               | true, v ->
                   model.Remove(typ) |> ignore
                   model.Add(typ, {Name=name; Id=int value} :: v)
               | false, _ ->  model.Add(typ, [{Name=name;Id=int value}])
               {state with Model = Some(model)}
            | _ -> state
        | _ -> state
        
    let getModel filename =
        File.ReadLines(filename)
        |> Seq.fold foldModel State.Empty

        

        
    