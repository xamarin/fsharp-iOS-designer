namespace Xamarin.Android.UIProvider.Runtime
open Microsoft.FSharp.Core.CompilerServices

//type WrappedView = 
//    inherit Android.Views.View
//    new() = { inherit Android.Views.View(null) }
//    new(context) = { inherit Android.Views.View(context) }
//    new(context:Android.Content.Context, attrs) = { inherit Android.Views.View(context, attrs) }
//    new(context, attrs, defStyle) = { inherit Android.Views.View(context, attrs, defStyle) }
    
[<assembly:TypeProviderAssembly("Xamarin.Android.UIProvider")>]
()