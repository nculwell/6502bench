using SDL2;
using static SDL2.SDL;
public class KeyboardHandler : IEventReceiver
{

    private EventHandler _eventHandler;
    private ILogger _logger;

    public KeyboardHandler(EventHandler eh, ILogger logger)
    {
        _eventHandler = eh;
        _logger = logger;
    }

    public void RegisterEvents()
    {
        _eventHandler.RegisterEventReceiver(this, new[] { EvtType.EVT_KEY_DOWN, EvtType.EVT_KEY_UP });
    }

    public void ReceiveEvent(Evt e)
    {
        switch (e.Type)
        {
            case EvtType.EVT_KEY_DOWN:
            case EvtType.EVT_KEY_UP:
                HandleKeypressEvent(e);
                break;
        }
    }

    private void HandleKeypressEvent(Evt e)
    {
        SDL_Keycode keysym = (SDL_Keycode)e.X;
        SDL_Keymod modifiers = (SDL_Keymod)e.Y;
        if (keysym == SDL2.SDL.SDL_Keycode.SDLK_q && modifiers == SDL_Keymod.KMOD_NONE)
        {
            _eventHandler.RaiseEvent(new Evt(EvtType.EVT_QUIT_REQUESTED));
        }
        else
        {
            // TODO
        }
    }

}