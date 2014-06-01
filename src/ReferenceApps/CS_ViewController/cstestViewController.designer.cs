// WARNING
//
// This file has been generated automatically by Xamarin Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.CodeDom.Compiler;

namespace cstest
{
	[Register ("cstestViewController")]
	partial class cstestViewController
	{
		[Outlet]
		[GeneratedCode ("iOS Designer", "1.0")]
		UIButton myButton { get; set; }

		[Outlet]
		[GeneratedCode ("iOS Designer", "1.0")]
		UISegmentedControl mySegment { get; set; }

		[Outlet]
		[GeneratedCode ("iOS Designer", "1.0")]
		UITextField myText { get; set; }

		[Action ("touchup:")]
		[GeneratedCode ("iOS Designer", "1.0")]
		partial void touchup (UIButton sender);

		void ReleaseDesignerOutlets ()
		{
			if (myButton != null) {
				myButton.Dispose ();
				myButton = null;
			}
			if (mySegment != null) {
				mySegment.Dispose ();
				mySegment = null;
			}
			if (myText != null) {
				myText.Dispose ();
				myText = null;
			}
		}
	}
}
