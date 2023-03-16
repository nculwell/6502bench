using SDL2;
using static SDL2.SDL;
using static SDL2.SDL.SDL_LogCategory;

public class Main : IEventReceiver
{

    private readonly Video _video;
    private readonly EventHandler _eventHandler;

    private char[,] _display = new char[0, 0];
    private bool _quitting = false;

    public Main(Video video, EventHandler eventHandler)
    {
        _video = video;
        _eventHandler = eventHandler;
        RegisterEvents();
    }

    public void Run()
    {
        try
        {
            // Read config
            // SDL_LogSetOutputFunction(LogCallback, IntPtr.Zero);
            SDL_LogSetPriority((int)SDL_LOG_CATEGORY_APPLICATION, SDL_LogPriority.SDL_LOG_PRIORITY_VERBOSE);
            // Init SDL
            _video.Init("SGText", 800, 600);
            ResizeWindow();
            // Load assets
            // Set up UI
            // Main loop
            MainLoop();
        }
        finally
        {
            _video.Dispose();
        }
    }

    private string LogPriorityText(int priority)
    {
        return LogPriorityText((SDL_LogPriority)priority);
    }

    private string LogPriorityText(SDL_LogPriority priority)
    {
        switch (priority)
        {
            case SDL_LogPriority.SDL_LOG_PRIORITY_CRITICAL: return "CRITICAL";
            case SDL_LogPriority.SDL_LOG_PRIORITY_ERROR: return "ERROR";
            case SDL_LogPriority.SDL_LOG_PRIORITY_WARN: return "WARN";
            case SDL_LogPriority.SDL_LOG_PRIORITY_INFO: return "INFO";
            case SDL_LogPriority.SDL_LOG_PRIORITY_DEBUG: return "DEBUG";
            case SDL_LogPriority.SDL_LOG_PRIORITY_VERBOSE: return "VERBOSE";
            default: throw new Exception("Unexpected priority value: " + priority);
        }
    }

    private void LogCallback(IntPtr userdata, int category, SDL_LogPriority priority, IntPtr message)
    {
        string prio = LogPriorityText(priority);
        string time = DateTime.Now.ToString("yyyy-MM-dd mm:ss.FFF");
        using var f = File.OpenWrite("textui.log");
        using var s = new StreamWriter(f, System.Text.Encoding.UTF8);
        s.WriteLine($"{time} [{prio}] {message}");
    }

    void ResizeWindow()
    {
        _video.CalculateWindowSize();
        var dim = _video.TextDimensions;
        _display = new char[dim.H, dim.W];
        SDL_Log($"Display dimensions: {_display.GetLength(1)} x {_display.GetLength(0)}");
    }

    private void MainLoop()
    {
        uint startTime = SDL_GetTicks();
        while (!_quitting)
        {
            uint frameStartTime = SDL_GetTicks();
            _eventHandler.HandleEvents(); // this waits for an event
            Update();
            Draw();
        }
    }

    private void Update()
    {
        for (int y = 0; y < _display.GetLength(0); y++)
            for (int x = 0; x < _display.GetLength(1); x++)
                _display[y, x] = 'a';
    }

    private void Draw()
    {
        _video.DrawFrame(_display);
    }

    private void RegisterEvents()
    {
        _eventHandler.RegisterEventReceiver(this, new[] {
             EvtType.EVT_QUIT_REQUESTED, EvtType.EVT_QUIT_CONFIRMED, EvtType.EVT_WINDOW_RESIZED,
        });
    }

    public void ReceiveEvent(Evt e)
    {
        switch (e.Type)
        {
            case EvtType.EVT_QUIT_REQUESTED:
                _eventHandler.RaiseEvent(new Evt(EvtType.EVT_QUIT_CONFIRMED));
                break;
            case EvtType.EVT_QUIT_CONFIRMED:
                _quitting = true;
                break;
            case EvtType.EVT_WINDOW_RESIZED:
                ResizeWindow();
                break;
        }
    }

}