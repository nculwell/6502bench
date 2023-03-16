
using Newtonsoft.Json.Linq;
using SDL2;
using static SDL2.SDL;
using static SDL2.SDL.SDL_LogCategory;

public partial class EventHandler
{

    const int EventTimeoutMs = 100;

    private Dictionary<EvtType, SortedSet<IEventReceiver>> _eventReceivers = new();
    private uint _userEventIdStart;

    PriorityQueue<Evt, ulong> _internalEventQueue = new();

    public EventHandler() { }

    private void LogEvent(string eventInfo)
        => SDL_LogVerbose((int)SDL_LOG_CATEGORY_APPLICATION, "Event: " + eventInfo);

    public void RegisterEventReceiver(IEventReceiver receiver, IEnumerable<EvtType> evtTypes)
    {
        foreach (var et in evtTypes)
        {
            SortedSet<IEventReceiver> receivers = new() { receiver };
            if (!_eventReceivers.TryAdd(et, receivers))
                if (!_eventReceivers[et].Add(receiver))
                    SDL_LogWarn((int)SDL_LOG_CATEGORY_APPLICATION, "Event receiver added twice for the same event.");
        }
    }

    public void RegisterUserEvents()
    {
        int userEventCount = Enum.GetValues(typeof(EvtType)).GetLength(0);
        _userEventIdStart = SDL_RegisterEvents(userEventCount);
        if (_userEventIdStart == uint.MaxValue)
            throw new SdlException("Unable to register user events");
    }

    public void HandleEvents()
    {
        bool haveInternalEvents = HandleInternalEvents();
        HandleSdlEvents(haveInternalEvents);
        HandleInternalEvents();
    }

    private bool HandleInternalEvents()
    {
        ulong now = SDL_GetTicks();
        bool haveInternalEvents = false;
        Evt? e;
        ulong evtTime;
        while (_internalEventQueue.TryPeek(out e, out evtTime) && evtTime <= now)
        {
            haveInternalEvents = true;
            e = _internalEventQueue.Dequeue();
            LogEvent($"Internal event: " + e.Type);
            DispatchInternalEvent(e);
        }
        return haveInternalEvents;
    }

    private void DispatchInternalEvent(Evt e)
    {
        if (_eventReceivers.TryGetValue(e.Type, out SortedSet<IEventReceiver>? receivers))
        {
            foreach (var receiver in receivers)
                receiver.ReceiveEvent(e);
        }
    }

    public void RaiseEvent(Evt e)
    {
        _internalEventQueue.Enqueue(e, e.TimeDue);
    }

    public void RaiseEventFuture(Evt e, ulong msAfterPresent)
    {
        ulong now = SDL_GetTicks();
        e.TimeDue = now + msAfterPresent;
        RaiseEvent(e);
    }

    private void HandleSdlEvents(bool noWait)
    {
        SDL_Event evt;
        int result = noWait
            ? SDL_PollEvent(out evt)
            : SDL_WaitEventTimeout(out evt, EventTimeoutMs);
        while (result != 0)
        {
            switch (evt.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:
                    switch (evt.window.windowEvent)
                    {
                        case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                        case SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                            RaiseEvent(new Evt(EvtType.EVT_WINDOW_RESIZED));
                            break;
                    }
                    LogEvent($"WindowEvent: " + evt.window.windowEvent.ToString());
                    break;
                case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                case SDL_EventType.SDL_MOUSEBUTTONUP:
                    LogEvent($"MouseButton");
                    break;
                case SDL_EventType.SDL_MOUSEWHEEL:
                    LogEvent($"MouseWheel");
                    break;
                case SDL_EventType.SDL_KEYDOWN:
                case SDL_EventType.SDL_KEYUP:
                    {
                        var k = evt.key.keysym.sym;
                        var m = evt.key.keysym.mod;
                        LogEvent($"Key: " + k);
                        RaiseEvent(new Evt(EvtType.EVT_KEY_DOWN, x: (int)k, y: (int)m));
                    }
                    break;
                case SDL_EventType.SDL_QUIT:
                    LogEvent($"Quit");
                    RaiseEvent(new Evt(EvtType.EVT_QUIT_REQUESTED));
                    break;
            }
            result = SDL_PollEvent(out evt);
        }
    }

    // private void PushUserEvent(EvtType et, int x, int y, uint v, uint w)
    // {
    //     Evt evt = new Evt(et, x, y, v, w);
    //     SDL_Event e = new()
    //     e.type = SDL_EventType.SDL_USEREVENT;
    //     JObject jo = new();
    // }

    // private void DispatchUserEvent(SDL_Event e)
    // {
    //     var et = (EvtType)e.user.code;
    //     if (_eventReceivers.TryGetValue(et, out SortedSet<IEventReceiver>? receivers))
    //     {
    //         string? evtJson = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(e.user.data1);
    //         Evt evt = new Evt(et, evtJson ?? "{}");
    //         foreach (var receiver in receivers)
    //             receiver.ReceiveEvent(evt);
    //     }
    // }

}