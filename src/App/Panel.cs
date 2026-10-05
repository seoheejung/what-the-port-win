using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WhatThePort.App {
    public sealed class Panel {
        public Window Window;
        readonly StackPanel body,footer;
        readonly TextBlock title,status,message;
        readonly Border messageBox;
        readonly Button back,pin;
        readonly Store store;
        readonly Scanner scanner=new Scanner();
        readonly DispatcherTimer timer=new DispatcherTimer();
        readonly Dictionary<string,DateTime> alerted=new Dictionary<string,DateTime>();
        readonly HashSet<string> selected=new HashSet<string>();
        readonly bool demo,snapshotMode;
        Settings settings;
        Snapshot current=new Snapshot();
        string view="list",detailKey;
        bool scanning,acting,pinned,closing,processesExpanded,hotkeyRegistered,capturing;
        double preferredHeight=594;
        int rowIndex;
        DateTime snoozeUntil=DateTime.MinValue,cleanupAsked=DateTime.MinValue;
        System.Windows.Forms.NotifyIcon tray;
        System.Drawing.Icon normalIcon,warningIcon;
        IntPtr handle;
        FontFamily mono;
        public Panel(bool isDemo,bool snapshot,string storageRoot=null) {
            demo=isDemo; snapshotMode=snapshot;
            store=new Store(storageRoot ?? (demo ? Path.Combine(Path.GetTempPath(),"WhatThePort-demo") : null));
            settings=demo?new Settings():store.LoadSettings();
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Shell.xaml")) Window=(Window)XamlReader.Load(stream);
            body=Find<StackPanel>("Body"); footer=Find<StackPanel>("Footer"); title=Find<TextBlock>("Title"); status=Find<TextBlock>("Status"); message=Find<TextBlock>("Message"); messageBox=Find<Border>("MessageBox"); back=Find<Button>("Back"); pin=Find<Button>("Pin");
            var fonts=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts")+Path.DirectorySeparatorChar);
            Window.FontFamily=new FontFamily(fonts,"./#Geist"); mono=new FontFamily(fonts,"./#Geist Mono");
            Find<Button>("Hide").Click+=delegate{Window.Hide();}; back.Click+=delegate{Navigate("list");};
            pin.Click+=delegate{pinned=!pinned; pin.Content=pinned?"◆":"◇"; pin.ToolTip=pinned?"Unpin panel":"Keep panel open";AutomationProperties.SetName(pin,(string)pin.ToolTip);};
            title.ToolTip="Drag to move. Position is saved. Ctrl+Shift+arrow keys also move the panel.";title.Cursor=Cursors.SizeAll;
            Find<Grid>("Header").MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.OriginalSource==title||e.OriginalSource==sender)Window.DragMove();};
            Window.PreviewKeyDown+=OnKey;
            Window.Deactivated+=delegate { if(!pinned&&!snapshotMode&&!acting&&view!="settings"&&view!="links")Window.Hide(); };
            Window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!closing){e.Cancel=true;Window.Hide();}};
            Window.SourceInitialized+=delegate {
                handle=new WindowInteropHelper(Window).Handle; HwndSource.FromHwnd(handle).AddHook(Hook);
                if(!demo){hotkeyRegistered=Native.RegisterHotKey(handle,1,0x4003,0x50);if(!hotkeyRegistered)Notice("Ctrl+Alt+P is already in use. Open the panel from its tray icon.");}
            };
            timer.Tick+=async delegate { await Refresh(); };
            timer.Interval=TimeSpan.FromSeconds(settings.ScanSeconds);
            if(demo)current=Demo.Create();
            if(!snapshotMode)CreateTray();
            Render();
            if(!String.IsNullOrEmpty(store.LastError))Notice(store.LastError);
        }
        T Find<T>(string name) where T:FrameworkElement{return (T)Window.FindName(name);}
        static Brush B(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
        static TextBlock Text(string value,double size,string color){return new TextBlock{Text=value,FontSize=size,Foreground=B(color),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};}
        TextBlock Mono(string value,double size,string color){var t=Text(value,size,color);t.FontFamily=mono;return t;}
        Button Button(string label,Action action,string tip){var b=new Button{Content=label,ToolTip=tip,Tag="action-"+(tip??label)};AutomationProperties.SetName(b,tip??label);b.Click+=delegate{Try(action);};return b;}
        void Try(Action action){try{action();}catch(Exception e){Notice(e.Message);}}
        void Notice(string value){
            message.Text=value;messageBox.Visibility=String.IsNullOrEmpty(value)?Visibility.Collapsed:Visibility.Visible;
            if(!String.IsNullOrEmpty(value)){var peer=UIElementAutomationPeer.CreatePeerForElement(message);if(peer!=null)peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);}
        }
        void Rule(){body.Children.Add(new Border{Height=1,Background=B("#30343D"),Margin=new Thickness(-16,13,-16,9)});}
        void Caption(string label){body.Children.Add(new TextBlock{Text=label,FontSize=11,Foreground=B("#A4A6AF"),Margin=new Thickness(4,15,0,8)});}
        static void Cell(Grid grid,UIElement child,int column){Grid.SetColumn(child,column);grid.Children.Add(child);}
        Grid Columns(params double[] widths){var g=new Grid();foreach(var width in widths)g.ColumnDefinitions.Add(new ColumnDefinition{Width=width<0?new GridLength(1,GridUnitType.Star):new GridLength(width)});return g;}
        string State(Server s){if(s.Protected)return "Protected process";if(!String.IsNullOrEmpty(s.Warning))return s.Warning;if((DateTime.UtcNow-s.LastActive).TotalMinutes>=1&&s.Connections==0&&s.Cpu<2)return "Idle "+Format.Duration(DateTime.UtcNow-s.LastActive)+" · no connections";return (s.Agent==null?"":s.Agent+" · ")+"up "+Format.Duration(DateTime.UtcNow-s.Started);}
        public void Show(){Show(false);}
        void Show(bool fromTray){if(snapshotMode)return;if(view=="list"||view=="detail"||view=="cleanup")Render();Place(true,fromTray);Window.Show();Place(true,fromTray);Window.Activate();Window.Focus();}
        void Place(bool opening,bool fromTray=false){
            Point? saved=settings.PanelLeft.HasValue?(Point?)new Point(settings.PanelLeft.Value,settings.PanelBottom.Value):null;
            PanelPlacement.Place(Window,preferredHeight,saved,opening,fromTray);
        }
        void RememberPosition(){
            var bounds=PanelPlacement.PhysicalBounds(Window);settings.PanelLeft=(int)Math.Round(bounds.Left);settings.PanelBottom=(int)Math.Round(bounds.Bottom);
            if(!demo)Try(delegate{store.SaveSettings(settings);});
        }
        public async void Start(){timer.Start();await Refresh();}
        public async Task Refresh(){
            if(demo||scanning||acting)return;scanning=true;
            try {
                Settings config=settings.Copy();var next=await Task.Run(()=>{lock(scanner)return scanner.Scan(config);});current=next;
                selected.RemoveWhere(k=>!current.Servers.Any(s=>s.Key==k&&!s.Protected));
                if(Window.IsVisible&&(view=="list"||view=="detail"||view=="cleanup"))Render();
                UpdateTray(); CheckAlerts();
                if(settings.Cleanup=="Automatic"&&view!="settings"&&view!="cleanup") {
                    var targets=current.Servers.Where(s=>Policy.CleanupCandidate(s,settings,DateTime.UtcNow)).ToList();
                    if(targets.Count>0)await StopServers(targets,true);
                }
            }catch(Exception e){current.Error=e.Message;Notice("Scan unavailable. "+e.Message);status.Text="SCAN PAUSED · retrying automatically";}
            finally{scanning=false;}
        }
        void Navigate(string target){view=target;Notice(null);Find<ScrollViewer>("Scroll").ScrollToTop();Render();if(Window.IsVisible){
            if(view=="list"){var row=body.Children.OfType<Button>().FirstOrDefault(b=>(b.Tag as string)=="server-"+detailKey);if(row!=null){row.Focus();return;}}
            if(view=="settings"||view=="links"){var input=Descendants(body).OfType<TextBox>().FirstOrDefault();if(input!=null){input.Focus();return;}}
            back.Focus();
        }}
        void Render(){
            // Preserve keyboard focus and scroll position through live refreshes.
            var focusedElement=Keyboard.FocusedElement as DependencyObject;
            while(focusedElement!=null&&!((focusedElement as FrameworkElement)!=null&&((FrameworkElement)focusedElement).Tag is string))focusedElement=VisualTreeHelper.GetParent(focusedElement);
            string focused=focusedElement==null?null:((FrameworkElement)focusedElement).Tag as string;
            var focusedChart=focusedElement as Chart;DateTime? chartTime=focusedChart==null?null:focusedChart.SelectedTime;
            double offset=Find<ScrollViewer>("Scroll").VerticalOffset;
            body.Children.Clear();footer.Children.Clear();
            back.Content=view=="list"?(object)DotGrid():"‹";back.IsEnabled=true;back.FontSize=25;back.Foreground=B("#A4A6AF");
            AutomationProperties.SetName(back,view=="list"?"Servers home":"Back to servers");back.ToolTip=view=="list"?"Servers home":"Back to servers (Esc)";
            status.Text=demo?"DEMO · SAMPLE DATA · NO SYSTEM ACTIONS":"LOCAL ONLY   ·   CTRL + ALT + P   ·   "+settings.ScanSeconds+"s";
            if(view=="detail")Detail();else if(view=="cleanup")List(true);else if(view=="settings")SettingsView();else if(view=="links")LinksView();else List(false);
            if(capturing||!Window.IsVisible)Window.Height=preferredHeight;else Place(false);
            Window.Title=title.Text+" · What the Port";
            Find<ScrollViewer>("Scroll").ScrollToVerticalOffset(offset);
            if(focused!=null) {Window.UpdateLayout();foreach(var element in Descendants(Window).OfType<FrameworkElement>())if((element.Tag as string)==focused){if(element is Chart&&chartTime.HasValue)((Chart)element).SelectTime(chartTime.Value);element.Focus();element.BringIntoView();break;}}
        }
        static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var node in Descendants(child))yield return node;}}
        void Summary(bool cleanup){
            var targets=current.Servers.Where(s=>selected.Contains(s.Key)).ToList();
            long value=cleanup?targets.Sum(s=>s.Memory):current.ServerMemory;
            var heading=Columns(-1,175);heading.Margin=new Thickness(3,6,3,12);
            var amount=new StackPanel{Orientation=Orientation.Horizontal};string[] parts=Format.Total(value).Split(' ');
            amount.Children.Add(Mono(parts[0],30,"#F5F5F7"));amount.Children.Add(new TextBlock{Text=" "+parts[1],FontFamily=mono,FontSize=12,Foreground=B("#9396A2"),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,6)});
            Cell(heading,amount,0);var cpu=Text(cleanup?"freed by stopping "+targets.Count:"CPU (servers)  "+Format.Percent(current.Cpu),11,"#A4A6AF");cpu.HorizontalAlignment=HorizontalAlignment.Right;Cell(heading,cpu,1);body.Children.Add(heading);
            var bar=new Grid{Height=7,Margin=new Thickness(3,0,3,11),ClipToBounds=true};double total=Math.Max(1,current.TotalMemory);double available=Math.Max(0,current.FreeMemory);double used=Math.Min(total-available,current.ServerMemory);double scale=current.ServerMemory>0?Math.Max(0,used)/current.ServerMemory:1;
            foreach(var s in current.Servers)Segment(bar,s.Memory*scale,Format.PortColor(s.Port));
            Segment(bar,Math.Max(0,total-available-used),"#454852");Segment(bar,available,"#2A2E37");body.Children.Add(bar);
            var legend=Text("■ Servers "+Format.Total(current.ServerMemory)+"   ▪ Other "+Format.Total(Math.Max(0,current.TotalMemory-current.FreeMemory-current.ServerMemory))+"   ▪ Free "+Format.Total(current.FreeMemory),10,"#A4A6AF");legend.Margin=new Thickness(3,0,0,0);legend.ToolTip="Resident working set; shared pages may appear in more than one process. System RAM: "+Format.Total(current.TotalMemory);body.Children.Add(legend);
        }
        static void Segment(Grid bar,double value,string color){if(value<=0)return;bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(value,GridUnitType.Star)});var segment=new Border{Background=B(color),CornerRadius=new CornerRadius(1),Margin=new Thickness(0,0,1,0)};Cell(bar,segment,bar.ColumnDefinitions.Count-1);}
        void List(bool cleanup){
            title.Text=cleanup?"Clean up":"Servers";preferredHeight=cleanup?614:594;Summary(cleanup);Rule();
            if(current.Servers.Count==0){
                var empty=new StackPanel{Margin=new Thickness(20,42,20,45),HorizontalAlignment=HorizontalAlignment.Center};
                var glyph=Mono(":_",38,"#7DDBE0");glyph.HorizontalAlignment=HorizontalAlignment.Center;empty.Children.Add(glyph);
                empty.Children.Add(new TextBlock{Text="A little breathing room.",FontSize=18,FontWeight=FontWeights.Medium,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,18,0,10)});
                empty.Children.Add(new TextBlock{Text="No development servers found.\nStart a local server and it will appear here.",FontSize=12,Foreground=B("#A4A6AF"),TextAlignment=TextAlignment.Center,LineHeight=20});body.Children.Add(empty);
                var more=Button("Show all user listeners",delegate{settings.AllListeners=true;Task ignored=Refresh();},"Include custom executables and other local apps");more.Margin=new Thickness(15,0,15,12);body.Children.Add(more);
            }
            for(int i=0;i<current.Servers.Count;i++){Server s=current.Servers[i];int index=i;body.Children.Add(ServerRow(s,cleanup,index));}
            if(cleanup){
                var note=Text("Stopping ends the selected process trees immediately.",10,"#A4A6AF");note.Margin=new Thickness(4,10,0,0);body.Children.Add(note);
                var buttons=Columns(80,-1);var cancel=Button("Cancel",delegate{Navigate("list");},"Cancel cleanup");cancel.Margin=new Thickness(0,0,8,0);Cell(buttons,cancel,0);
                var targets=current.Servers.Where(s=>selected.Contains(s.Key)&&!s.Protected).ToList();
                var stop=Button("Stop "+targets.Count+" server"+(targets.Count==1?"":"s")+" · free "+Format.Bytes(targets.Sum(s=>s.Memory)),async delegate{await StopServers(targets,false);},"Stop selected server process trees");stop.Background=B("#FF453A");stop.IsEnabled=targets.Count>0&&!acting&&current.Error==null;Cell(buttons,stop,1);footer.Children.Add(buttons);
            }else{
                var buttons=Columns(-1,130,36);int count=current.Servers.Count(s=>Policy.CleanupCandidate(s,settings,DateTime.UtcNow));
                var clean=Button("Clean up"+(count>0?"  "+count:""),EnterCleanup,"Select servers to stop (C)");clean.HorizontalAlignment=HorizontalAlignment.Left;Cell(buttons,clean,0);
                var live=Text(current.Servers.Count+" servers · monitoring",10,"#A4A6AF");live.HorizontalAlignment=HorizontalAlignment.Right;live.Margin=new Thickness(0,0,10,0);Cell(buttons,live,1);
                var gear=Button("\uE713",delegate{Navigate("settings");},"Settings (Ctrl+,)");gear.FontFamily=new FontFamily("Segoe MDL2 Assets");gear.Background=Brushes.Transparent;gear.FontSize=16;gear.Padding=new Thickness(0);Cell(buttons,gear,2);footer.Children.Add(buttons);
            }
        }
        UIElement ServerRow(Server s,bool cleanup,int index){
            var row=Columns(cleanup?24:0,61,-1,43,73);row.Height=65;row.Margin=new Thickness(0,0,0,1);
            if(cleanup){var check=new CheckBox{IsChecked=selected.Contains(s.Key),IsEnabled=!s.Protected,ToolTip=s.Protected?"Protected process":"Select :"+s.Port};check.Tag="select-"+s.Key;AutomationProperties.SetName(check,"Select server "+s.Port);check.Click+=delegate{if(check.IsChecked==true)selected.Add(s.Key);else selected.Remove(s.Key);Render();};Cell(row,check,0);}
            var port=Mono(":"+s.Port,13,Format.PortColor(s.Port));port.FontWeight=FontWeights.Medium;port.Margin=new Thickness(0,0,3,0);Cell(row,port,1);
            var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0)};var name=Text(s.Name,13,"#F5F5F7");name.FontWeight=FontWeights.Medium;name.ToolTip=s.Folder??s.Name;labels.Children.Add(name);var meta=Text(State(s),10,s.Warning==null?"#A4A6AF":"#FFB224");meta.Margin=new Thickness(0,4,0,0);meta.ToolTip=s.Branch==null?State(s):s.Branch+" · "+State(s);labels.Children.Add(meta);Cell(row,labels,2);
            var chart=new Chart{Samples=s.History,Spark=true,Height=24,VerticalAlignment=VerticalAlignment.Center,Stroke=(Color)ColorConverter.ConvertFromString(s.Warning==null?"#B7BAC4":"#FFB224")};Cell(row,chart,3);
            var memory=Mono(Format.Bytes(s.Memory),11,s.Warning==null?"#DADCE4":"#FFB224");memory.HorizontalAlignment=HorizontalAlignment.Right;memory.Margin=new Thickness(0,0,2,0);Cell(row,memory,4);
            if(cleanup)return row;
            var button=Button("",delegate{rowIndex=index;detailKey=s.Key;Navigate("detail");},s.Name+" · port "+s.Port+" · "+Format.Bytes(s.Memory)+" · "+State(s));button.Content=row;button.Padding=new Thickness(3,0,3,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Background=Brushes.Transparent;button.Tag="server-"+s.Key;
            button.GotKeyboardFocus+=delegate{rowIndex=index;};
            return button;
        }
        Server CurrentServer(){return current.Servers.FirstOrDefault(s=>s.Key==detailKey);}
        void Detail(){
            Server s=CurrentServer();if(s==null){view="list";List(false);Notice("That server has stopped.");return;}
            title.Text=s.Name;preferredHeight=704;
            var top=Columns(-1,124,34);top.Margin=new Thickness(3,1,3,7);Cell(top,Mono(":"+s.Port,29,Format.PortColor(s.Port)),0);Cell(top,Text("Running for "+Format.Duration(DateTime.UtcNow-s.Started),11,"#A4A6AF"),1);
            var stop=Button("■",delegate{ConfirmStop(s);},s.Protected?"Protected server":"Stop server");stop.Background=B("#3B2528");stop.Foreground=B("#FF6961");stop.Padding=new Thickness(0);stop.IsEnabled=!s.Protected&&!acting&&current.Error==null;Cell(top,stop,2);body.Children.Add(top);Rule();
            var link=demo?null:store.LinkFor(s);
            Property("Session",link!=null&&!String.IsNullOrEmpty(link.Session)?link.Agent+" · "+link.Session:s.Agent??"Not linked",delegate{Navigate("links");});
            Property("Branch",s.Branch??"No Git branch",null);
            Property("Folder",s.Folder??"Unavailable for this process",delegate{RunAction("terminal");});
            Property("Ports",String.Join(", ",s.Ports)+"   ·   "+s.Connections+" connections",null);
            Rule();
            MetricTitle("Memory",Format.Bytes(s.Memory),s.Warning==null?"#F5F5F7":"#FFB224");
            body.Children.Add(new Chart{Tag="chart-memory",Samples=s.History,Threshold=settings.MemoryGB*Format.GB,Height=121,Stroke=(Color)ColorConverter.ConvertFromString(s.Warning==null?"#DADCE4":"#FFB224")});
            MetricTitle("CPU",Format.Percent(s.Cpu),"#F5F5F7");body.Children.Add(new Chart{Tag="chart-cpu",Samples=s.History,Cpu=true,Height=115,Stroke=(Color)ColorConverter.ConvertFromString(Format.PortColor(s.Port))});
            Rule();
            var processes=new Expander{Header="Processes   ·   "+s.Processes.Count+"   ·   "+Format.Bytes(s.Memory),Foreground=B("#B7BAC4"),FontSize=12,Margin=new Thickness(3,0,3,8),IsExpanded=processesExpanded};
            processes.Tag="processes";AutomationProperties.SetName(processes,(string)processes.Header);
            processes.Expanded+=delegate{processesExpanded=true;};processes.Collapsed+=delegate{processesExpanded=false;};
            var nodes=new StackPanel();foreach(var p in s.Processes){var item=Text(new string(' ',p.Depth*2)+p.Name+"   #"+p.Pid+"   "+Format.Bytes(p.Memory),11,"#A4A6AF");item.Margin=new Thickness(12,6,0,2);nodes.Children.Add(item);}processes.Content=nodes;body.Children.Add(processes);
            var actions=Columns(-1,40,40,40);var open=Button("Open localhost:"+s.Port,delegate{RunAction("open");},"Open local server (O)");open.Background=B("#F5F5F7");open.Foreground=B("#111318");open.Margin=new Thickness(0,0,6,0);Cell(actions,open,0);
            string[] keys={"terminal","session","preview"},labels={">_","A","↗"},tips={"Open project in terminal (T)","Resume linked agent session (A)","Open Vercel preview (V)"};
            for(int i=0;i<3;i++){string action=keys[i];var b=Button(labels[i],delegate{RunAction(action);},tips[i]);b.Padding=new Thickness(0);b.Margin=new Thickness(2,0,0,0);Cell(actions,b,i+1);}footer.Children.Add(actions);
            var links=Button("Link session & preview   ·   Copy URL",delegate{Navigate("links");},"Manage project links and copy local URL");links.Background=Brushes.Transparent;links.Foreground=B("#A4A6AF");links.FontSize=10;links.MinHeight=25;links.Padding=new Thickness(0,5,0,0);footer.Children.Add(links);
        }
        void MetricTitle(string label,string value,string color){var row=Columns(64,-1,45);row.Margin=new Thickness(3,4,3,2);Cell(row,Text(label,12,"#A4A6AF"),0);Cell(row,Mono(value,12,color),1);Cell(row,Mono("10 min",10,"#A4A6AF"),2);body.Children.Add(row);}
        void Property(string key,string value,Action action){var row=Columns(72,-1);row.Margin=new Thickness(3,4,3,4);Cell(row,Text(key,11,"#A4A6AF"),0);if(action==null){var t=Text(value,12,"#DADCE4");t.ToolTip=value;Cell(row,t,1);}else{var b=Button(value,action,value);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Background=Brushes.Transparent;b.MinHeight=24;b.Padding=new Thickness(0);var t=Text(value,12,"#DADCE4");b.Content=t;Cell(row,b,1);}body.Children.Add(row);}
        void EnterCleanup(){selected.Clear();foreach(var s in current.Servers.Where(s=>Policy.CleanupCandidate(s,settings,DateTime.UtcNow)))selected.Add(s.Key);Navigate("cleanup");}
        void ConfirmStop(Server s){
            acting=true;bool confirmed=false;
            try { confirmed=MessageBox.Show(Window,"Stop :"+s.Port+" and its "+s.Processes.Count+" process(es)?\n\nThis ends the process tree immediately.","Stop server",MessageBoxButton.OKCancel,MessageBoxImage.Warning)==MessageBoxResult.OK; }
            finally{acting=false;}
            if(confirmed){Task ignored=StopServers(new List<Server>{s},false);}
        }
        async Task StopServers(List<Server> targets,bool automatic){
            if(acting)return;
            if(demo){foreach(var s in targets)current.Servers.Remove(s);Navigate("list");Notice("Demo: selected servers removed. No processes were stopped.");return;}
            acting=true;
            try {
                Settings config=settings.Copy();
                var result=await Task.Run(()=>{lock(scanner){
                    // Re-scan immediately before every stop; no cached selection can authorize a new PID.
                    Snapshot fresh=scanner.Scan(config);var errors=new List<string>();
                    foreach(var target in targets){var live=fresh.Servers.FirstOrDefault(s=>s.Key==target.Key);if(live==null){errors.Add(":"+target.Port+" already stopped.");continue;}if(automatic&&!Policy.CleanupCandidate(live,config,DateTime.UtcNow))continue;errors.AddRange(ProcessControl.Stop(live));}
                    return Tuple.Create(scanner.Scan(config),errors);
                }});
                current=result.Item1;if(!automatic)Navigate("list");else Render();
                string text=result.Item2.Count==0?"Stopped "+targets.Count+" server(s).":String.Join("\n",result.Item2);
                if(automatic)Notify("Automatic clean up",text);else Notice(text);
                UpdateTray();
            }catch(Exception e){Notice("Could not stop servers. "+e.Message);}finally{acting=false;}
        }
        void RunAction(string action){
            var s=CurrentServer();if(s==null)return;
            if(demo){Notice("Demo: "+action+" action simulated.");return;}
            var link=store.LinkFor(s);
            if(action=="open")Launchers.OpenLocal(s);
            else if(action=="terminal")Launchers.Terminal(s.Folder,settings.Terminal,null);
            else if(action=="session"){if(link==null||String.IsNullOrEmpty(link.Session)){Navigate("links");Notice("Register this server's exact session ID to resume it.");}else Launchers.Terminal(s.Folder,settings.Terminal,link);}
            else if(action=="preview"){if(link==null||String.IsNullOrEmpty(link.Preview)){Navigate("links");Notice("Register the Vercel preview URL for this project.");}else Launchers.OpenPreview(link);}
        }
        TextBox Input(string label,string value){Caption(label);var input=new TextBox{Tag="input-"+label,Text=value??"",Height=34,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(9,2,9,2),Margin=new Thickness(3,0,3,0)};AutomationProperties.SetName(input,label);body.Children.Add(input);return input;}
        ComboBox Select(string label,string value,params string[] choices){Caption(label);var combo=new ComboBox{Tag="select-"+label,ItemsSource=choices,SelectedItem=value,Margin=new Thickness(3,0,3,0)};AutomationProperties.SetName(combo,label);body.Children.Add(combo);return combo;}
        CheckBox Check(string label,bool value){var check=new CheckBox{Tag="check-"+label,Content=label,IsChecked=value,Margin=new Thickness(3,15,0,0)};AutomationProperties.SetName(check,label);body.Children.Add(check);return check;}
        void SettingsView(){
            title.Text="Settings";preferredHeight=716;
            var intro=Text("A quieter laptop, on your terms.",16,"#F5F5F7");intro.Margin=new Thickness(3,8,0,3);body.Children.Add(intro);
            var scan=Input("SAMPLE INTERVAL · seconds (1–30)",settings.ScanSeconds.ToString());
            var memory=Input("MEMORY ALERT · GB",settings.MemoryGB.ToString(CultureInfo.InvariantCulture));
            var growth=Input("GROWTH ALERT · MB / 10 min (0 = off)",settings.GrowthMB.ToString());
            var cpu=Input("CPU ALERT · % of this computer",settings.CpuPercent.ToString());
            var notify=Check("Desktop notifications",settings.Notifications);
            var cleanup=Select("AUTOMATIC CLEAN UP",settings.Cleanup,"Off","Ask","Automatic");
            var idle=Input("IDLE THRESHOLD · hours (0.05–720)",settings.IdleHours.ToString(CultureInfo.InvariantCulture));
            var explanation=new TextBlock{Text="Only continuously observed idle servers are eligible. Protected and alerting servers are never stopped automatically.",FontSize=11,Foreground=B("#A4A6AF"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(3,9,3,0)};body.Children.Add(explanation);
            var terminal=Select("PREFERRED TERMINAL",settings.Terminal,"PowerShell","Windows Terminal");
            var all=Check("Show all user TCP listeners",settings.AllListeners);
            var snooze=Button("Snooze alerts for 1 hour",delegate{snoozeUntil=DateTime.UtcNow.AddHours(1);Notice("Alerts snoozed for one hour.");},"Pause resource notifications");snooze.Margin=new Thickness(3,18,3,4);body.Children.Add(snooze);
            var position=Button("Reset panel position",delegate{settings.PanelLeft=null;settings.PanelBottom=null;if(!demo)store.SaveSettings(settings);Place(true);Notice("Position reset to the primary monitor. Drag the title to save a new position.");},"Reset panel position to primary monitor");position.Margin=new Thickness(3,8,3,4);body.Children.Add(position);
            var positionHelp=new TextBlock{Text="Drag the title to move the panel. Position is saved automatically. Keyboard: Ctrl+Shift+arrow keys.",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=B("#A4A6AF"),Margin=new Thickness(3,4,3,0)};body.Children.Add(positionHelp);
            var help=new TextBlock{Text="Windows 10 / 11 · x64\nWhat the Port for Windows 1.0\nBased on WhatThePort by Tomjohn Design · MIT",FontSize=10,Foreground=B("#A4A6AF"),Margin=new Thickness(3,18,3,8),LineHeight=18};body.Children.Add(help);
            var buttons=Columns(90,-1);var cancel=Button("Cancel",delegate{Navigate("list");},"Discard changes");cancel.Margin=new Thickness(0,0,8,0);Cell(buttons,cancel,0);
            var save=Button("Save preferences",delegate{
                var next=new Settings{ScanSeconds=Int32.Parse(scan.Text,CultureInfo.InvariantCulture),MemoryGB=Double.Parse(memory.Text,CultureInfo.InvariantCulture),GrowthMB=Int32.Parse(growth.Text,CultureInfo.InvariantCulture),CpuPercent=Int32.Parse(cpu.Text,CultureInfo.InvariantCulture),IdleHours=Double.Parse(idle.Text,CultureInfo.InvariantCulture),Notifications=notify.IsChecked==true,Cleanup=(string)cleanup.SelectedItem,Terminal=(string)terminal.SelectedItem,AllListeners=all.IsChecked==true};
                next.PanelLeft=settings.PanelLeft;next.PanelBottom=settings.PanelBottom;next.Validate();if(!demo)store.SaveSettings(next);settings=next;timer.Interval=TimeSpan.FromSeconds(settings.ScanSeconds);Navigate("list");Notice(demo?"Demo preferences applied for this session.":"Preferences saved.");
            },"Save preferences");save.Background=B("#F5F5F7");save.Foreground=B("#111318");Cell(buttons,save,1);footer.Children.Add(buttons);
        }
        void LinksView(){
            var s=CurrentServer();if(s==null){Navigate("list");return;}title.Text="Project links";preferredHeight=650;var link=demo?null:store.LinkFor(s);
            var intro=new TextBlock{Text="Pick up where you left off.",FontSize=18,Margin=new Thickness(3,10,0,0)};body.Children.Add(intro);
            var sub=new TextBlock{Text="Link the exact agent session and preview for :"+s.Port+". Your conversation history is never read.",Foreground=B("#A4A6AF"),TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(3,9,3,0),LineHeight=19};body.Children.Add(sub);
            var folder=Input("PROJECT FOLDER",s.Folder);var agent=Select("AGENT",link==null?"Codex":link.Agent??"Codex","Codex","Claude Code");
            var session=Input("SESSION ID",link==null?"":link.Session);var preview=Input("VERCEL PREVIEW · https://…vercel.app",link==null?"":link.Preview);
            var note=new TextBlock{Text="Resume runs in your preferred terminal. Codex or Claude Code must already be installed and signed in.",Foreground=B("#A4A6AF"),TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(3,16,3,0),LineHeight=18};body.Children.Add(note);
            var copy=Button("Copy "+s.Url,delegate{if(demo)Notice("Demo: URL copy simulated.");else{Clipboard.SetText(s.Url);Notice("Local URL copied.");}},"Copy local server URL");copy.Margin=new Thickness(3,22,3,0);body.Children.Add(copy);
            var buttons=Columns(90,-1);var cancel=Button("Cancel",delegate{Navigate("detail");},"Back to server");cancel.Margin=new Thickness(0,0,8,0);Cell(buttons,cancel,0);
            var save=Button("Save links",delegate{if(demo){Navigate("detail");Notice("Demo: project links simulated.");return;}var item=new ProjectLink{Folder=folder.Text.Trim(),Port=s.Port,Agent=(string)agent.SelectedItem,Session=session.Text.Trim(),Preview=preview.Text.Trim()};item.Validate();if(!String.IsNullOrEmpty(s.Folder)&&!String.Equals(s.Folder.TrimEnd('\\'),item.Folder.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The folder must match this server's detected project folder.");store.SaveLink(item);Navigate("detail");Notice("Project links saved.");},"Save project links");save.Background=B("#F5F5F7");save.Foreground=B("#111318");Cell(buttons,save,1);footer.Children.Add(buttons);
        }
        void OnKey(object sender,KeyEventArgs e){
            if(Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&(e.Key==Key.Left||e.Key==Key.Right||e.Key==Key.Up||e.Key==Key.Down)){
                Window.Left+=(e.Key==Key.Left?-20:e.Key==Key.Right?20:0);Window.Top+=(e.Key==Key.Up?-20:e.Key==Key.Down?20:0);Place(false);RememberPosition();e.Handled=true;return;
            }
            var combo=Keyboard.FocusedElement as ComboBox;if(e.Key==Key.Escape&&combo!=null&&combo.IsDropDownOpen)return;
            if(e.Key==Key.Escape){if(view=="list")Window.Hide();else Navigate("list");e.Handled=true;return;}
            if(e.Key==Key.OemComma&&Keyboard.Modifiers==ModifierKeys.Control){Navigate("settings");e.Handled=true;return;}
            if(Keyboard.FocusedElement is TextBox||Keyboard.FocusedElement is ComboBox||Keyboard.Modifiers!=ModifierKeys.None)return;
            if(view=="list"&&(e.Key==Key.Down||e.Key==Key.Up)){rowIndex=Math.Max(0,Math.Min(current.Servers.Count-1,rowIndex+(e.Key==Key.Down?1:-1)));if(current.Servers.Count>0){string tag="server-"+current.Servers[rowIndex].Key;foreach(var b in body.Children.OfType<Button>())if((b.Tag as string)==tag)b.Focus();}e.Handled=true;}
            else if(view=="list"&&e.Key==Key.C){EnterCleanup();e.Handled=true;}
            else if(view=="detail"){string action=e.Key==Key.O?"open":e.Key==Key.T?"terminal":e.Key==Key.A?"session":e.Key==Key.V?"preview":null;if(action!=null){Try(delegate{RunAction(action);});e.Handled=true;}}
        }
        IntPtr Hook(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam,ref bool handled){
            if(msg==0x0312||msg==0x8001){if(Window.IsVisible)Window.Hide();else Show();handled=true;}
            // Re-evaluate after Windows has applied taskbar, display or DPI changes.
            if(!snapshotMode&&(msg==0x001A||msg==0x007E||msg==0x02E0||msg==0x0232))
                Window.Dispatcher.BeginInvoke(new Action(delegate{if(Window.IsVisible&&!closing){Place(false);if(msg==0x0232)RememberPosition();}}));
            return IntPtr.Zero;
        }
        void CreateTray(){
            normalIcon=Icon(false);warningIcon=Icon(true);tray=new System.Windows.Forms.NotifyIcon{Icon=normalIcon,Text="What the Port · monitoring",Visible=true};
            tray.MouseClick+=delegate(object sender,System.Windows.Forms.MouseEventArgs e){if(e.Button==System.Windows.Forms.MouseButtons.Left){if(Window.IsVisible)Window.Hide();else Show(true);}};
            var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Open What the Port",null,delegate{Show();});menu.Items.Add("Clean up…",null,delegate{EnterCleanup();Show();});menu.Items.Add("Settings…",null,delegate{Navigate("settings");Show();});menu.Items.Add("Snooze alerts for 1 hour",null,delegate{snoozeUntil=DateTime.UtcNow.AddHours(1);});menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());menu.Items.Add("Quit",null,delegate{Quit();});tray.ContextMenuStrip=menu;
            tray.BalloonTipClicked+=delegate{Show();};
        }
        static UIElement DotGrid(){var canvas=new Canvas{Width=19,Height=19};for(int y=0;y<4;y++)for(int x=0;x<4;x++){var dot=new System.Windows.Shapes.Ellipse{Width=3,Height=3,Fill=B("#FFB224")};Canvas.SetLeft(dot,x*5);Canvas.SetTop(dot,y*5);canvas.Children.Add(dot);}return canvas;}
        static System.Drawing.Icon Icon(bool warning){using(var bitmap=new System.Drawing.Bitmap(32,32)){using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var brush=new System.Drawing.SolidBrush(warning?System.Drawing.Color.FromArgb(255,178,36):System.Drawing.Color.FromArgb(125,219,224))){for(int y=0;y<4;y++)for(int x=0;x<4;x++)g.FillEllipse(brush,3+x*7,3+y*7,4,4);}}IntPtr h=bitmap.GetHicon();try{using(var icon=System.Drawing.Icon.FromHandle(h))return (System.Drawing.Icon)icon.Clone();}finally{Native.DestroyIcon(h);}}}
        void UpdateTray(){if(tray==null)return;tray.Text="What the Port · "+current.Servers.Count+" servers · "+Format.Total(current.ServerMemory);tray.Icon=current.Servers.Any(s=>s.Warning!=null)?warningIcon:normalIcon;}
        void Notify(string heading,string text){if(tray!=null&&settings.Notifications&&DateTime.UtcNow>=snoozeUntil){tray.BalloonTipTitle=heading;tray.BalloonTipText=text;tray.ShowBalloonTip(6000);}}
        void CheckAlerts(){
            DateTime now=DateTime.UtcNow;
            foreach(var s in current.Servers.Where(s=>s.Warning!=null)){DateTime last;if(!alerted.TryGetValue(s.Key,out last)||(now-last).TotalMinutes>=15){Notify(":"+s.Port+" · "+s.Name,s.Warning);alerted[s.Key]=now;}}
            foreach(var key in alerted.Keys.Where(k=>!current.Servers.Any(s=>s.Key==k)).ToList())alerted.Remove(key);
            int count=current.Servers.Count(s=>Policy.CleanupCandidate(s,settings,now));if(settings.Cleanup=="Ask"&&count>0&&(now-cleanupAsked).TotalHours>=1){Notify("A little breathing room?",count+" idle server(s) are ready to clean up. Open Clean up to review.");cleanupAsked=now;}
        }
        public void Quit(){closing=true;timer.Stop();if(handle!=IntPtr.Zero&&!demo)Native.UnregisterHotKey(handle,1);if(tray!=null){tray.Visible=false;tray.Dispose();}if(normalIcon!=null)normalIcon.Dispose();if(warningIcon!=null)warningIcon.Dispose();Window.Close();Application.Current.Shutdown();}
        public async Task<bool> LiveSmoke(string report){
            settings=new Settings{Notifications=false,Cleanup="Off"};
            new WindowInteropHelper(Window).EnsureHandle();
            await Refresh();await Task.Delay(300);await Refresh();
            bool ok=current.Error==null&&current.TotalMemory>0&&tray!=null&&tray.Visible&&handle!=IntPtr.Zero;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            File.WriteAllText(report,"result="+(ok?"PASS":"FAIL")+"\ntray="+(tray!=null&&tray.Visible)+"\nhotkey="+hotkeyRegistered+"\nwindowHandle="+(handle!=IntPtr.Zero)+"\nservers="+current.Servers.Count+"\ntotalMemory="+current.TotalMemory+"\nerror="+(current.Error??"none")+"\n");
            Quit();return ok;
        }
        public void CaptureAll(string directory){
            Directory.CreateDirectory(directory);capturing=true;pinned=true;Window.Left=30;Window.Top=30;Window.Show();
            foreach(string page in new[]{"list","detail","cleanup","settings","links","empty"}){
                current=Demo.Create();detailKey=current.Servers[0].Key;selected.Clear();if(page=="cleanup")selected.Add(current.Servers[4].Key);if(page=="empty")current.Servers.Clear();view=page=="empty"?"list":page;Render();
                Window.UpdateLayout();Find<ScrollViewer>("Scroll").ScrollToTop();Window.UpdateLayout();
                var bitmap=new RenderTargetBitmap((int)(Window.ActualWidth*1.5),(int)(Window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(Window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,page+".png")))encoder.Save(file);
            }
            Quit();
        }
    }
}
