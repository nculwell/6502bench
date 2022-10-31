public class KeyboardHandler : IEventReceiver
{

    public KeyboardHandler() { }

    public void RegisterEvents(EventHandler eh)
    {
        eh.RegisterEventReceiver(this, new[] { EvtType.EVT_KEY_DOWN, EvtType.EVT_KEY_UP });
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
        uint keysym = e.V ?? 0;
        uint modifiers = e.W ?? 0;
    }

}