using SDL2;
using static SDL2.SDL;

public class Main
{

    const int EventTimeoutMs = 100;

    private readonly Video _video;
    private bool _quitting = false;

    private char[,]? _display;

    public Main(Video video)
    {
        _video = video;
    }

    public void Run()
    {
        // Read config
        // Init SDL
        _video.Init("SGText", 800, 600);
        ResizeWindow();
        // Load assets
        // Set up UI
        // Main loop
        MainLoop();
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
        => SDL_LogDebug((int)SDL_LogCategory.SDL_LOG_CATEGORY_APPLICATION, "Event: " + eventInfo);

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
                    LogEvent($"Key");
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