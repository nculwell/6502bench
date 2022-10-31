using System.Runtime.Serialization;
using SDL2;

[Serializable]
public class SdlException : Exception
{

    public SdlException()
        : this("SDL error")
    {
    }

    public SdlException(string? message, Exception? innerException = null)
        : base($"{message}: {SDL.SDL_GetError()}", innerException)
    {
    }

}

[Serializable]
public class SdlTtfException : Exception
{

    public SdlTtfException()
        : this("SDL_ttf error")
    {
    }

    public SdlTtfException(string? message, Exception? innerException = null)
        : base($"{message}: {SDL.SDL_GetError()}", innerException)
    {
        // Note that this just calls SDL_GetError -- apparently that's what TTF_GetError does anyway?
    }

}
