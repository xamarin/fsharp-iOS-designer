namespace Xamarin.UIProviders.DesignTime

type MaybeBuilder() =
    member this.Bind(x, f) =
        match x with
        | None -> None
        | Some a -> f a

    member this.Return(x) =
        Some x

[<AutoOpen>]
module extensions =
    let maybe = new MaybeBuilder()