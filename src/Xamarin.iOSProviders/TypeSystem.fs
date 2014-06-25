﻿namespace Xamarin.iOSProviders
open System
open System.Drawing
open MonoTouch
open MonoTouch.UIKit
open MonoTouch.Foundation
open MonoTouch.Design

type StaticHelpers() =
    static member InstantiateInitialViewController<'a when 'a :> NSObject>( storyboardName) =
        let mainStoryboard = UIStoryboard.FromName (storyboardName, null)
        let sb = mainStoryboard.InstantiateInitialViewController ()
        let theType = sb.GetType()
        sb :?> 'a
    

module TypeSystem =
    let typeMap =

        //TODO replace this with a call to xml to objc type and then look in the monotouch assembly for a type with a register attribute that matches the objc name
        Map.ofList
            [("activityIndicatorView", typeof<UIActivityIndicatorView>)
             ("adBannerView", typeof<MonoTouch.iAd.ADBannerView>)
             ("attributedString", typeof<NSMutableAttributedString>)
             ("autoresizingMask", typeof<UIViewAutoresizing>)
             ("barButtonItem", typeof<UIBarButtonItem>)
             ("button", typeof<UIButton>)
             ("color", typeof<UIColor>)
             ("collectionReusableView", typeof<UICollectionReusableView>)
             ("collectionView", typeof<UICollectionView>)
             ("collectionViewCell", typeof<UICollectionViewCell>)
             ("collectionViewController", typeof<UICollectionViewController>)
             ("collectionViewLayout", typeof<UICollectionViewLayout>)
             ("collectionViewFlowLayout", typeof<UICollectionViewFlowLayout>)
             ("constraint", typeof<NSLayoutConstraint>)
             ("containerView", typeof<UIView>)
             ("control", typeof<UIControl>)
             ("customObject", typeof<NSObject>)
             ("dataDetectorType", typeof<UIDataDetectorType>)
             ("date", typeof<MonoTouch.Foundation.NSDate>)
             ("datePicker", typeof<UIDatePicker>)
             ("dependencies", typeof<Dependencies>)
             ("deployment", typeof<Deployment>)
             ("development", typeof<Development>)
             ("extendedEdge", typeof<UIRectEdge>)
             ("font", typeof<UIFont>)
             ("fontDescription", typeof<UIFont>)
             ("glkView", typeof<GLKit.GLKView>)
             ("glkViewController", typeof<GLKit.GLKViewController>)
             ("imageView", typeof<UIImageView>)
             ("inset", typeof<UIEdgeInsets>)
             ("integer", typeof<int>)
             ("label", typeof<UILabel>)
             ("locale", typeof<MonoTouch.Foundation.NSLocale>)
             ("mapView", typeof<MapKit.MKMapView>)
             ("mutableData", typeof<MonoTouch.Foundation.NSData>)
             ("mutableString", typeof<string>)
             ("navigationBar", typeof<UINavigationBar>)
             ("navigationController", typeof<UINavigationController>)
             ("navigationItem", typeof<UINavigationItem>)
             ("offsetWrapper", typeof<UIOffset>)
             ("pageControl", typeof<UIPageControl>)
             ("panGestureRecognizer", typeof<UIPanGestureRecognizer>)
             ("paragraphStyle", typeof<NSMutableParagraphStyle>)
             ("pickerView", typeof<UIPickerView>)
             ("pinchGestureRecognizer", typeof<UIPinchGestureRecognizer>)
             ("plugIn", typeof<Plugin>)
             ("pageViewController", typeof<UIPageViewController>)
             ("progressView", typeof<UIProgressView>)
             ("point", typeof<PointF>)
             ("pongPressGestureRecognizer", typeof<UILongPressGestureRecognizer>) // the class name is an Xcode typo that we emulate
             ("real", typeof<float>)
             ("rect", typeof<RectangleF>)
             ("rotationGestureRecognizer", typeof<UIRotationGestureRecognizer>)
             ("scrollView", typeof<UIScrollView>)
             ("searchBar", typeof<UISearchBar>)
             ("searchDisplayController", typeof<UISearchDisplayController>)
             ("segmentedControl", typeof<UISegmentedControl>)
             ("size", typeof<SizeF>)
             ("slider", typeof<UISlider>)
             ("splitViewController", typeof<UISplitViewController>)
             ("splitViewDetailSimulatedSizeMetrics", typeof<SimulatedSplitViewDetailSizeMetrics>)
             ("splitViewMasterSimulatedSizeMetrics", typeof<SimulatedSplitViewMasterSizeMetrics>)
             ("state", typeof<Void>) //not needed client side//
             ("stepper", typeof<UIStepper>)
             ("string", typeof<string>)
             ("swipeGestureRecognizer", typeof<UISwipeGestureRecognizer>)
             ("switch", typeof<UISwitch>)
             ("tabBar", typeof<UITabBar>)
             ("tabBarController", typeof<UITabBarController>)
             ("tabBarItem", typeof<UITabBarItem>)
             ("tableView", typeof<UITableView>)
             ("tableViewCell", typeof<UITableViewCell>)
             ("tableViewController", typeof<UITableViewController>)
             ("tableViewSection", typeof<ProxiedTableViewSection>)
             ("tapGestureRecognizer", typeof<UITapGestureRecognizer>)
             ("textAttributes", typeof<UITextAttributes>)
             ("textField", typeof<UITextField>)
             ("toolbarItems", typeof<ToolbarItems>)
             ("textView", typeof<UITextView>)
             ("timeZone", typeof<MonoTouch.Foundation.NSTimeZone>)
             ("toolbar", typeof<UIToolbar>)
             ("view", typeof<UIView>)
             ("viewController", typeof<UIViewController>)
             ("viewControllerLayoutGuide", typeof<IUILayoutSupport>)
             ("window", typeof<UIWindow>)
             ("webView", typeof<UIWebView>) ]
