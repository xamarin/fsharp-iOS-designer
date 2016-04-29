
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace cs_test
{
	//[Register ("com.companyname.cs_test.View23")]
    public class View23 : View
    {
        public View23 (Context context) :
            base (context)
        {
            Initialize ();
        }

        public View23 (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        //public View23 (Context context, IAttributeSet attrs, int defStyle) :
        //    base (context, attrs, defStyle)
        //{
        //    Initialize ();
        //}

        void Initialize ()
        {
			
        }
    }
}

