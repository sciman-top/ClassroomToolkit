using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ClassroomToolkit.App.Ink;

public enum PowerPointSlideshowEventType
{
    Begin,
    NextBuild,
    NextSlide,
    End
}

public sealed class PowerPointSlideshowEventBridge : IDisposable
{
    private object? _application;
    private IConnectionPoint? _connectionPoint;
    private int _cookie;
    private PowerPointEventSink? _sink;

    public event Action<PowerPointSlideshowEventType>? EventReceived;
    public bool IsAttached => _connectionPoint != null && _cookie != 0;

    public bool TryAttach(object? application)
    {
        if (application == null)
        {
            Detach();
            return false;
        }

        if (ReferenceEquals(_application, application) && _connectionPoint != null && _cookie != 0)
        {
            return true;
        }

        Detach();

        if (application is not IConnectionPointContainer container)
        {
            return false;
        }

        var iid = typeof(PowerPointEApplicationEvents).GUID;
        container.FindConnectionPoint(ref iid, out _connectionPoint);
        if (_connectionPoint == null)
        {
            return false;
        }

        _sink = new PowerPointEventSink(this);
        _connectionPoint.Advise(_sink, out _cookie);
        if (_cookie == 0)
        {
            Detach();
            return false;
        }

        _application = application;
        return true;
    }

    public void Detach()
    {
        if (_connectionPoint != null && _cookie != 0)
        {
            try
            {
                _connectionPoint.Unadvise(_cookie);
            }
            catch
            {
            }
        }

        _cookie = 0;
        _connectionPoint = null;
        _sink = null;
        _application = null;
    }

    public void Dispose()
    {
        Detach();
    }

    private void OnEvent(PowerPointSlideshowEventType eventType)
    {
        EventReceived?.Invoke(eventType);
    }

    [ComImport]
    [Guid("914934C2-5A91-11CF-8700-00AA0060263B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface PowerPointEApplicationEvents
    {
        [DispId(2011)]
        void SlideShowBegin([MarshalAs(UnmanagedType.IDispatch)] object wn);

        [DispId(2012)]
        void SlideShowNextBuild([MarshalAs(UnmanagedType.IDispatch)] object wn);

        [DispId(2013)]
        void SlideShowNextSlide([MarshalAs(UnmanagedType.IDispatch)] object wn);

        [DispId(2014)]
        void SlideShowEnd([MarshalAs(UnmanagedType.IDispatch)] object pres);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class PowerPointEventSink : PowerPointEApplicationEvents
    {
        private readonly PowerPointSlideshowEventBridge _owner;

        public PowerPointEventSink(PowerPointSlideshowEventBridge owner)
        {
            _owner = owner;
        }

        public void SlideShowBegin(object wn)
        {
            _owner.OnEvent(PowerPointSlideshowEventType.Begin);
        }

        public void SlideShowNextBuild(object wn)
        {
            _owner.OnEvent(PowerPointSlideshowEventType.NextBuild);
        }

        public void SlideShowNextSlide(object wn)
        {
            _owner.OnEvent(PowerPointSlideshowEventType.NextSlide);
        }

        public void SlideShowEnd(object pres)
        {
            _owner.OnEvent(PowerPointSlideshowEventType.End);
        }
    }
}
