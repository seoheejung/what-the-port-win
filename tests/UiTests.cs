using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WhatThePort.App;

public static class UiTests {
    static int passed,failed;
    static WhatThePort.App.Panel panel;
    static Window window;
    [STAThread] public static int Main(){
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{
            PlacementTests();
            panel=new WhatThePort.App.Panel(true,true);window=panel.Window;window.Opacity=0;window.ShowActivated=false;window.Show();
            NativePlacementTests();
            var bounds=PanelPlacement.Calculate(PanelPlacement.WorkArea(window),new Size(440,594),null);
            window.Left=bounds.Left;window.Top=bounds.Top;window.Width=bounds.Width;window.Height=bounds.Height;
            double bottom=bounds.Bottom;
            // Exercise actual WPF templates invisibly; every operation uses isolated demo data.
            Layout();Check("list starts with five servers",Rows().Count()==5);
            KeyboardTests();
            CheckNames("list controls have screen reader names");
            Click(Rows().First());Check("row opens server detail",Title()=="menubar port monitor");
            CheckPlacement("detail grows upward with visible footer",bottom);
            Check("detail has memory and CPU charts",All(Body()).OfType<Chart>().Count()==2);
            CheckNames("detail controls have screen reader names");
            Check("window title identifies current screen",window.Title.Contains(Title()));
            Click(ByName("Open local server (O)"));Check("demo blocks external URL",Message().Contains("simulated"));
            Click(ByName("Open project in terminal (T)"));Check("demo blocks terminal launch",Message().Contains("terminal"));
            Click(ByName("Resume linked agent session (A)"));Check("demo blocks agent launch",Message().Contains("session"));
            Click(ByName("Open Vercel preview (V)"));Check("demo blocks preview launch",Message().Contains("preview"));
            Click(ByName("Manage project links and copy local URL"));Check("project links screen opens",Title()=="Project links");
            CheckNames("link fields and buttons have screen reader names");
            CheckPlacement("links preserve bottom and footer",bottom);
            Click(ByName("Save project links"));Check("demo links save returns to detail",Title()=="menubar port monitor"&&Message().Contains("simulated"));
            CheckPlacement("detail return preserves bottom",bottom);
            Click((Button)window.FindName("Back"));Click(ByName("Select servers to stop (C)"));Check("cleanup view opens",Title()=="Clean up");
            CheckNames("cleanup controls have screen reader names");
            CheckPlacement("cleanup preserves bottom and footer",bottom);
            var checks=All(Body()).OfType<CheckBox>().ToList();Check("idle cleanup candidates preselected",checks.Count(c=>c.IsChecked==true)==2);
            var first=checks.First();first.IsChecked=true;Click(first);Check("cleanup selection updates stop count",ByName("Stop selected server process trees").Content.ToString().StartsWith("Stop 3 servers"));
            Click(ByName("Stop selected server process trees"));Check("demo cleanup removes only selection",Title()=="Servers"&&Rows().Count()==2&&Message().Contains("No processes"));
            Click(ByName("Settings (Ctrl+,)"));Check("settings view opens",Title()=="Settings");
            CheckNames("settings controls have screen reader names");
            CheckPlacement("settings fit work area with visible save button",bottom);
            var scroll=(ScrollViewer)window.FindName("Scroll");scroll.ApplyTemplate();var scrollbar=ScrollBarFrom(scroll);Check("compact scrollbar width",scrollbar!=null&&scrollbar.Width==5);
            Check("long settings content scrolls within panel",scroll.ScrollableHeight>0);
            var sample=All(Body()).OfType<TextBox>().First();sample.Text="0";Click(ByName("Save preferences"));Check("invalid preferences stay on settings",Title()=="Settings"&&Message().Contains("thresholds"));
            var notice=(TextBlock)window.FindName("Message");Check("error text exposed as screen reader live region",AutomationProperties.GetLiveSetting(notice)==AutomationLiveSetting.Polite&&UIElementAutomationPeer.CreatePeerForElement(notice).GetName().Contains("thresholds"));
            sample.Text="5";Click(ByName("Save preferences"));Check("valid preferences applied",Title()=="Servers"&&((TextBlock)window.FindName("Status")).Text.Contains("DEMO"));
            CheckPlacement("return to list preserves bottom",bottom);
            Click(ByName("Select servers to stop (C)"));foreach(var check in All(Body()).OfType<CheckBox>().ToList()){if(check.IsChecked!=true){check.IsChecked=true;Click(check);}}
            // Every click re-renders the view, so select remaining current controls by iteration.
            for(int i=0;i<5;i++){var uncheckedBox=All(Body()).OfType<CheckBox>().FirstOrDefault(c=>c.IsChecked!=true);if(uncheckedBox==null)break;uncheckedBox.IsChecked=true;Click(uncheckedBox);}
            Click(ByName("Stop selected server process trees"));Check("empty state after final cleanup",Rows().Count()==0&&All(Body()).OfType<TextBlock>().Any(t=>t.Text=="A little breathing room."));
            var pin=(Button)window.FindName("Pin");Click(pin);Check("pin toggles",pin.Content.ToString()=="◆");
            Check("pin state has a meaningful accessible name",UIElementAutomationPeer.CreatePeerForElement(pin).GetName()=="Unpin panel");
        }catch(Exception e){failed++;Console.WriteLine("FAIL UI exception: "+e);}
        finally{if(panel!=null)panel.Quit();else app.Shutdown();}
        Console.WriteLine("\n"+passed+" UI passed, "+failed+" failed");return failed==0?0:1;
    }
    static void NativePlacementTests(){
        PanelPlacement.Place(window,594,null,true,false);Layout();
        var first=PanelPlacement.PhysicalBounds(window);var primary=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
        Check("first opening uses primary monitor physical work area",first.Left>=primary.Left&&first.Right<=primary.Right+1&&first.Top>=primary.Top&&first.Bottom<=primary.Bottom+1);
        foreach(var screen in System.Windows.Forms.Screen.AllScreens){
            var area=screen.WorkingArea;PanelPlacement.Place(window,716,new Point(area.Left+30,area.Bottom-20),true,false);Layout();Pump();
            var actual=PanelPlacement.PhysicalBounds(window);
            Check("saved placement fits connected monitor "+screen.DeviceName,actual.Left>=area.Left-1&&actual.Right<=area.Right+1&&actual.Top>=area.Top-1&&actual.Bottom<=area.Bottom+1);
        }
        PanelPlacement.Place(window,594,null,true,false);Layout();
        var saved=new Point(first.Left-30,first.Bottom-20);
        PanelPlacement.Place(window,594,saved,true,false);Layout();var moved=PanelPlacement.PhysicalBounds(window);
        window.Hide();window.Show();PanelPlacement.Place(window,594,saved,true,true);Layout();var reopened=PanelPlacement.PhysicalBounds(window);
        Check("saved physical position survives hiding and tray reopening",Near(moved.Left,reopened.Left)&&Near(moved.Bottom,reopened.Bottom));
        PanelPlacement.Place(window,704,saved,false,false);Layout();var detail=PanelPlacement.PhysicalBounds(window);
        Check("native detail placement preserves physical bottom",Near(detail.Bottom,reopened.Bottom));
        PanelPlacement.Place(window,594,new Point(-900000,-900000),true,false);Layout();var work=PanelPlacement.WorkArea(window);
        Check("disconnected monitor position returns to visible work area",window.Left>=work.Left-1&&window.Top>=work.Top-1);
        PanelPlacement.Place(window,594,null,true,false);Layout();
    }
    static void KeyboardTests(){
        window.Activate();Pump();Rows().First().Focus();
        Press(Key.Enter);Check("Enter opens focused server without mouse",Title()=="menubar port monitor");
        var chart=All(Body()).OfType<Chart>().First();chart.Focus();Press(Key.Home);
        Check("chart Home selects first sample",chart.SelectedTime==chart.Samples.First().At);
        Press(Key.Right);Check("chart Right selects next sample",chart.SelectedTime==chart.Samples[1].At);
        var peer=UIElementAutomationPeer.CreatePeerForElement(chart);
        Check("chart screen reader alternative includes value and navigation",peer.GetName().Contains("Sample 2 of")&&peer.GetName().Contains("Memory")&&peer.GetHelpText().Contains("Left and Right"));
        var time=chart.SelectedTime;Rerender();chart=All(Body()).OfType<Chart>().First();
        Check("live refresh preserves chart focus and sample",chart.IsKeyboardFocused&&chart.SelectedTime==time);
        Press(Key.End);Check("chart End selects latest sample",chart.SelectedTime==chart.Samples.Last().At);
        var action=ByName("Open local server (O)");action.Focus();Rerender();
        Check("live refresh preserves footer button focus",ByName("Open local server (O)").IsKeyboardFocused);
        Press(Key.Enter);Check("keyboard activates action with feedback",Message().Contains("simulated"));
        Press(Key.Escape);Check("Escape returns to focused server row",Title()=="Servers"&&Rows().First().IsKeyboardFocused);
        var pin=(Button)window.FindName("Pin");pin.Focus();
        var visited=new HashSet<DependencyObject>();
        for(int i=0;i<30;i++){var focused=Keyboard.FocusedElement as UIElement;if(focused==null)break;visited.Add(focused);focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));}
        Check("Tab traversal reaches server rows and footer",visited.Contains(Rows().First())&&visited.Contains(ByName("Select servers to stop (C)"))&&visited.Contains(ByName("Settings (Ctrl+,)")));
        Press(Key.C);var check=All(Body()).OfType<CheckBox>().First();check.Focus();bool before=check.IsChecked==true;Press(Key.Space);
        Check("Space toggles cleanup selection",All(Body()).OfType<CheckBox>().First().IsChecked!=before);
        Press(Key.Escape);Rows().First().Focus();
    }
    static void CheckNames(string label){
        var controls=All(window).OfType<Control>().Where(c=>c.IsVisible&&(c is Button||c is CheckBox||c is ComboBox||c is TextBox||c is Expander));
        Check(label,controls.All(c=>{var peer=UIElementAutomationPeer.CreatePeerForElement(c);return peer!=null&&!String.IsNullOrWhiteSpace(peer.GetName());}));
    }
    static void Rerender(){typeof(WhatThePort.App.Panel).GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(panel,null);Layout();Pump();}
    static void Pump(){var frame=new System.Windows.Threading.DispatcherFrame();window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);}
    static void Press(Key key){
        var target=Keyboard.FocusedElement as UIElement;if(target==null)throw new Exception("No keyboard focus for "+key);
        var source=PresentationSource.FromVisual(window);
        var preview=new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};target.RaiseEvent(preview);
        if(!preview.Handled){target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.KeyDownEvent});target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.KeyUpEvent});}
        Layout();Pump();
    }
    static void PlacementTests(){
        var work=new Rect(0,0,1920,1040);
        var list=PanelPlacement.Calculate(work,new Size(440,594),null);
        var detail=PanelPlacement.Calculate(work,new Size(440,704),list);
        Check("taller view keeps bottom and moves upward",Near(list.Bottom,detail.Bottom)&&Near(list.Top-detail.Top,110));
        var returned=PanelPlacement.Calculate(work,new Size(440,594),detail);
        Check("shorter view returns to original bounds",returned==list);
        var moved=new Rect(200,250,440,594);
        var movedDetail=PanelPlacement.Calculate(work,new Size(440,704),moved);
        Check("navigation respects manually moved panel",Near(movedDetail.Left,moved.Left)&&Near(movedDetail.Bottom,moved.Bottom));
        var shortWork=new Rect(0,0,1280,640);
        var clipped=PanelPlacement.Calculate(shortWork,new Size(440,716),null);
        Check("short display caps height above taskbar",Near(clipped.Height,616)&&Near(clipped.Top,12)&&Near(clipped.Bottom,628));
        var leftMonitor=new Rect(-1920,40,1920,1000);
        var secondary=PanelPlacement.Calculate(leftMonitor,new Size(440,716),null);
        Check("secondary monitor with negative origin and top taskbar",leftMonitor.Contains(secondary)&&Near(secondary.Right,-12)&&Near(secondary.Bottom,1028));
        var sideTaskbar=new Rect(60,0,1220,720);
        var corrected=PanelPlacement.Calculate(sideTaskbar,new Size(440,716),new Rect(-100,200,440,594));
        Check("side taskbar and offscreen bounds are clamped",sideTaskbar.Contains(corrected)&&Near(corrected.Left,72));
        foreach(double scale in new[]{1.0,1.25,1.5,2.0}){
            var dipWork=PanelPlacement.ToDips(new Rect(0,0,1920,1040),new Matrix(1/scale,0,0,1/scale,0,0));
            var fit=PanelPlacement.Calculate(dipWork,new Size(440,716),null);
            Check("physical work area conversion at "+(scale*100)+"% DPI",Near(fit.Bottom*scale,1040-12*scale)&&fit.Top>=12&&dipWork.Contains(fit));
        }
    }
    static bool Near(double a,double b){return Math.Abs(a-b)<1;}
    static void CheckPlacement(string name,double bottom){
        var work=PanelPlacement.WorkArea(window);
        var footer=(FrameworkElement)window.FindName("Footer");
        var status=(FrameworkElement)window.FindName("Status");
        var footerPoint=footer.TransformToAncestor(window).Transform(new Point(0,footer.ActualHeight));
        var statusPoint=status.TransformToAncestor(window).Transform(new Point(0,status.ActualHeight));
        Check(name,Near(window.Top+window.ActualHeight,bottom)&&window.Top>=work.Top&&
            window.Top+window.ActualHeight<=work.Bottom+1&&footer.ActualHeight>0&&
            footerPoint.Y<=window.ActualHeight&&statusPoint.Y<=window.ActualHeight);
    }
    static void Check(string name,bool condition){if(condition){passed++;Console.WriteLine("PASS "+name);}else{failed++;Console.WriteLine("FAIL "+name);}}
    static StackPanel Body(){return (StackPanel)window.FindName("Body");}
    static string Title(){return ((TextBlock)window.FindName("Title")).Text;}
    static string Message(){return ((TextBlock)window.FindName("Message")).Text;}
    static IEnumerable<Button> Rows(){return Body().Children.OfType<Button>().Where(b=>b.Tag is string&&((string)b.Tag).StartsWith("server-"));}
    static Button ByName(string name){return All(window).OfType<Button>().First(b=>AutomationProperties.GetName(b)==name);}
    static IEnumerable<DependencyObject> All(DependencyObject node){yield return node;foreach(var next in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())foreach(var child in All(next))yield return child;}
    static void Click(System.Windows.Controls.Primitives.ButtonBase b){b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Layout();}
    static void Layout(){window.Measure(new Size(window.Width,window.Height));window.Arrange(new Rect(0,0,window.Width,window.Height));window.UpdateLayout();}
    static System.Windows.Controls.Primitives.ScrollBar ScrollBarFrom(ScrollViewer scroll){return scroll.Template.FindName("PART_VerticalScrollBar",scroll) as System.Windows.Controls.Primitives.ScrollBar;}
}
