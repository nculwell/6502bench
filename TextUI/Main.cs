using SDL2;
using static SDL2.SDL;
using static SDL2.SDL.SDL_LogCategory;

public class Main
{

    const int EventTimeoutMs = 100;

    private readonly Video _video;
    private bool _quitting = false;

    private char[,] _display = new char[0, 0];

    public Main(Video video)
    {
        _video = video;
    }

    public void Run()
    {
        try
        {
            // Read config
            SDL_LogSetOutputFunction(LogCallback, IntPtr.Zero);
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
        var dim = _video.TextDimensions;
        _display = new char[dim.H, dim.W];
    }

    private void MainLoop()
    {
        uint startTime = SDL_GetTicks();
        while (!_quitting)
        {
            uint frameStartTime = SDL_GetTicks();
            ReadInput(); // this waits for an event
            Update();
            Draw();
        }
    }

    private void LogEvent(string eventInfo)
        => SDL_LogDebug((int)SDL_LOG_CATEGORY_APPLICATION, "Event: " + eventInfo);

    private void ReadInput()
    {
        while (true)
        {
            int result = SDL_WaitEventTimeout(out SDL_Event evt, EventTimeoutMs);
            if (result == 0)
                return;
            switch (evt.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:
                    LogEvent($"WindowEvent");
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
                        LogEvent($"Key: " + k);
                        if (k == SDL_Keycode.SDLK_q)
                            _quitting = true;
                    }
                    break;
                case SDL_EventType.SDL_QUIT:
                    LogEvent($"Quit");
                    // TODO: Confirm
                    _quitting = true;
                    break;
            }
        }
    }

    private void Update()
    {
        for (int y = 0; y < _display.GetLength(0); y++)
            for (int x = 0; x < _display.GetLength(0); x++)
                _display[y, x] = '.';
    }

    private void Draw()
    {
        _video.DrawFrame(_display);
    }

}