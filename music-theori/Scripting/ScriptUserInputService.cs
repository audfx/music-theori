using theori.IO;

using MoonSharp.Interpreter.Interop;

namespace theori.Scripting
{
    public sealed class ScriptUserInputService : BaseScriptInstance
    {
        public static readonly ScriptUserInputService Instance = new ScriptUserInputService();

        private ScriptUserInputService()
        {
        }

        #region Script API stuff

        [MoonSharpVisible(true)] public void SetInputMode(string mode)
        {
            mode = mode.ToLower();
            if (mode == "any")
                UserInputService.SetInputMode(UserInputService.Modes.Any);
            else
            {
                UserInputService.Modes modes = UserInputService.Modes.None;
                foreach (string m in mode.Split(';'))
                {
                    switch (m)
                    {
                        default: break;

                        // keyboard/mouse inputs only
                        case "desktop": modes |= UserInputService.Modes.Desktop; break;
                        // gamepad inputs only
                        case "gamepad": modes |= UserInputService.Modes.Gamepad; break;
                        // "controller" inputs only
                        case "controller": modes |= UserInputService.Modes.Controller; break;
                    }
                }

                UserInputService.SetInputMode(modes);
            }
        }

        #endregion
    }
}
