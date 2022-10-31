
using SDL2;
using static SDL2.SDL;
using static SDL2.SDL.SDL_LogCategory;

public class EventHandler
{

    public bool Quitting { get; private set; }

    public EventHandler() { }

    private void LogEvent(string eventInfo)
        => SDL_LogVerbose((int)SDL_LOG_CATEGORY_APPLICATION, "Event: " + eventInfo);

    public void HandleEvents()
    {
        while (true)
        {
            int result = SDL_WaitEventTimeout(out SDL_Event evt, EventTimeoutMs);
            if (result == 0)
                return;
            switch (evt.type)
            {
                case SDL_EventType.SDL_WINDOWEVENT:
                    switch (evt.window.windowEvent)
                    {
                        case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                        case SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                            _video.CalculateWindowSize();
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
                        var newEvent = new SDL_Event();
                        var k = evt.key.keysym.sym;
                        LogEvent($"Key: " + k);
                        if (k == SDL_Keycode.SDLK_q)
                        {
                            newEvent.type = SDL_EventType.SDL_QUIT;
                            newEvent.quit.timestamp = evt.key.timestamp;
                            SDL_PushEvent(ref newEvent);
                        }
                    }
                    break;
                case SDL_EventType.SDL_QUIT:
                    LogEvent($"Quit");
                    // TODO: Confirm
                    Quitting = true;
                    break;
            }
        }
    }

}