using static theori.Platform.SDL.SDL;

namespace theori.IO
{
    public enum MouseButton : uint
    {
        Unknown = 0,

        Left = SDL_BUTTON_LEFT,
        Middle = SDL_BUTTON_MIDDLE,
        Right = SDL_BUTTON_RIGHT,
        X1 = SDL_BUTTON_X1,
        X2 = SDL_BUTTON_X2,
    }
}
