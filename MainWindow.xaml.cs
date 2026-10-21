using StdUnit.Tags;
using StdUnit.Tags.R3;
using R3;
using System.Windows;
using System.Windows.Media;
using usbtest.Tags;

namespace usbtest;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private IDisposable _disposables;
    private ITagsProjectCtrl? _ctrl;

    public MainWindow()
    {
        InitializeComponent();


        var app = App.Current as App ?? throw new InvalidCastException("App.Current is not of type App");
        this._ctrl = app.Ctrl;
        this._disposables = app.ObservableProject
            .Select(proj => Observable.Create<Unit>(observer => {
                return proj is null ?
                    Disposable.Empty :
                    SubscribeTags(proj!.Tags);
            })) 
            .Switch()
            .Subscribe();
    }

    private IDisposable SubscribeTags(ITagGrp tags)
    {
        var entry = tags.SelectGrp(TagsPathDefines.EntryName);
        var req = entry.SelectTag(TagsPathDefines.心跳请求);
        var ack = entry.SelectTag(TagsPathDefines.心跳响应);
        var interval = entry.SelectTag(TagsPathDefines.扫描周期);

        var d = new CompositeDisposable();
        req.Watch()
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(evt =>
            {
                var newvalue = evt.NewValue;
                this.txtReq.Text = newvalue?.ToString();
                this.txtReq.Foreground= newvalue is true ? Brushes.Green : Brushes.Black;
            })
            .AddTo(d);

        ack.Watch()
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(evt =>
            {
                var newvalue = evt.NewValue;
                this.txtAck.Text = newvalue?.ToString();
                this.txtAck.Foreground = newvalue is true ? Brushes.Green : Brushes.Black;
            })
            .AddTo(d);

        interval.Watch()
            .ObserveOnDispatcher(this.Dispatcher)
            .Subscribe(evt =>
            {
                var newvalue = evt.NewValue;
                this.txtInterval.Text = newvalue?.ToString();
            })
            .AddTo(d);
        return d;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private async void btnToggle_Click(object sender, RoutedEventArgs e)
    {
        var proj = this._ctrl?.Project;
        if(proj is null)
        {
            return;
        }
        var enqueue = proj.WriteIntent(
            entry: TagsPathDefines.EntryName, 
            intent: (grp, ct) => {
                var tag = proj.Tags.SelectTag($"{TagsPathDefines.EntryName}/{TagsPathDefines.测试1}");
                var old = tag.GetTagValue<byte>();
                tag.Value = (byte)(((int)old + 1) % 256);
                return ValueTask.CompletedTask;
            }, 
            out var task
        );
        await task;
    }
}