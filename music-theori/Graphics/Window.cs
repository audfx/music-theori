using System;
using System.Runtime.InteropServices;
using System.Text;

using theori.Graphics.OpenGL;
using theori.IO;
using theori.Platform;
using static theori.Platform.SDL.SDL;

namespace theori.Graphics
{
    public enum VSyncMode
    {
        Adaptive = -1,
        Off = 0,
        On = 1,
    }

    public static class Window
    {
        public static bool HasFocus { get; private set; }

        internal static IntPtr window, context;
        
        public static int Width { get; private set; }
        public static int Height { get; private set; }
        public static float Aspect => (float)Width / Height;

        public static event Action<int, int>? ClientSizeChanged;

        private static VSyncMode vsync;
        public static VSyncMode VSync
        {
            get => vsync;
            set
            {
                if (SDL_GL_SetSwapInterval((int)value) == -1)
                    vsync = (VSyncMode)SDL_GL_GetSwapInterval();
                else vsync = value;
            }
        }

        private static ClientHost? host;

        public static void Create(ClientHost host)
        {
            using var _ = Profiler.Scope("Window::Create");

            if (window != IntPtr.Zero)
                throw new InvalidOperationException("Only one Window can be created at a time.");

            Window.host = host;

            {
                using var __ = Profiler.Scope("Window::Create - Initialize SDL2");
                if (SDL_Init(SDL_INIT_VIDEO) != 0)
                {
                    string err = SDL_GetError();
                    Logger.Log(err, LogPriority.Error);
                    // can't continue, sorry
                    host.PerformExit(true);
                }
            }
            
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_RED_SIZE, 8);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_GREEN_SIZE, 8);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_BLUE_SIZE, 8);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_ALPHA_SIZE, 8);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_MULTISAMPLEBUFFERS, 1);
            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_MULTISAMPLESAMPLES, 16);

            SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, 2);

            var windowFlags = SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_SHOWN;
            if (Configuration.TheoriConfig.Maximized)
                windowFlags |= SDL_WindowFlags.SDL_WINDOW_MAXIMIZED;

            {
                using var __ = Profiler.Scope("Window::Create - Create SDL2 Window");

                int width = Configuration.TheoriConfig.WindowWidth;
                int height = Configuration.TheoriConfig.WindowHeight;
                // we're pretty sure this takes as long as it does because of the OpenGL flag
                window = SDL_CreateWindow("theori", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, Width = width, Height = height, windowFlags);
            }

            SDL_DisableScreenSaver();

            if (window == IntPtr.Zero)
            {
                string err = SDL_GetError();
                Logger.Log(err, LogPriority.Error);
                // can't continue, sorry
                host.PerformExit(true);
            }

            {
                using var __ = Profiler.Scope("Window::Create - Create OpenGL Context");

                context = SDL_GL_CreateContext(window);
                SDL_GL_MakeCurrent(window, context);
            }

            if (SDL_GL_SetSwapInterval(1) == -1)
            {
                string err = SDL_GetError();
                Logger.Log(err, LogPriority.Error);
            }
            vsync = (VSyncMode)SDL_GL_GetSwapInterval();

		    Logger.Log($"OpenGL Version: { GL.GetString(GL.GL_VERSION) }");
		    Logger.Log($"OpenGL Shading Language Version: { GL.GetString(GL.GL_SHADING_LANGUAGE_VERSION) }");
		    Logger.Log($"OpenGL Renderer: { GL.GetString(GL.GL_RENDERER) }");
		    Logger.Log($"OpenGL Vendor: { GL.GetString(GL.GL_VENDOR) }");

            GL.Enable(GL.GL_MULTISAMPLE);
            GL.Enable(GL.GL_BLEND);

            GL.BlendFunc(BlendingSourceFactor.SourceAlpha, BlendingDestinationFactor.OneMinusSourceAlpha);

            for (int i = 0; i < 2; i++)
            {
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                SwapBuffer();
            }

            SDL_StopTextInput();

            Update();
        }

        internal static void Show()
        {
            SDL_ShowWindow(window);
        }

        internal static void Destroy()
        {
            SDL_EnableScreenSaver();

            SDL_GL_DeleteContext(context);
            SDL_DestroyWindow(window);

            SDL_Quit();
        }

        internal static void SwapBuffer()
        {
            using var _ = Profiler.Scope("Window::SwapBuffer");
            SDL_GL_SwapWindow(window);
        }

        internal static unsafe void Update()
        {
            using var _ = Profiler.Scope("Window::Update");
            SDL_GL_MakeCurrent(window, context);

            while (SDL_PollEvent(out var evt) != 0)
            {
                switch (evt.type)
                {
                    case SDL_EventType.SDL_QUIT: host!.Exit(); break;
                        
                    case SDL_EventType.SDL_KEYDOWN:
                    case SDL_EventType.SDL_KEYUP:
                    {
                        var sym = evt.key.keysym;
                        var info = new KeyInfo()
                        {
                            ScanCode = (ScanCode)sym.scancode,
                            KeyCode = (KeyCode)sym.sym,
                            Mods = (KeyMod)sym.mod,
                        };

                        if (evt.type == SDL_EventType.SDL_KEYDOWN)
                            UserInputService.Keyboard_KeyPress(info);
                        else UserInputService.Keyboard_KeyRelease(info);
                    } break;

                    case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    case SDL_EventType.SDL_MOUSEBUTTONUP:
                    {
                        UserInputService.MouseX = evt.button.x;
                        UserInputService.MouseY = evt.button.y;

                        var info = new MouseButtonInfo()
                        {
                            Button = (MouseButton)evt.button.button,
                            X = evt.button.x,
                            Y = evt.button.y,
                        };

                        if (evt.type == SDL_EventType.SDL_MOUSEBUTTONDOWN)
                            UserInputService.Mouse_MouseButtonPress(info);
                        else UserInputService.Mouse_MouseButtonRelease(info);
                    } break;

                    case SDL_EventType.SDL_MOUSEMOTION:
                    {
                        UserInputService.MouseX = evt.motion.x;
                        UserInputService.MouseY = evt.motion.y;

                        UserInputService.Mouse_Move(evt.motion.xrel, evt.motion.yrel, evt.motion.xrel, evt.motion.yrel);
                    } break;
                    case SDL_EventType.SDL_MOUSEWHEEL:
                    {
                        int y = evt.wheel.y;
                        if (evt.wheel.direction != (uint)SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED)
                            y = -y;
                        UserInputService.Mouse_Scroll(evt.wheel.x, y);
                    } break;
                        
                    case SDL_EventType.SDL_TEXTEDITING:
                    {
                        SDL_Rect rect;
                        SDL_GetWindowPosition(window, out rect.x, out rect.y);
                        SDL_GetWindowSize(window, out rect.w, out rect.h);
                        SDL_SetTextInputRect(ref rect);

                        byte[] bytes = new byte[32];
                        Marshal.Copy(new IntPtr(evt.edit.text), bytes, 0, 32);

                        string composition = Encoding.UTF8.GetString(bytes, 0, Array.IndexOf<byte>(bytes, 0));
                        int cursor = evt.edit.start;
                        int selectionLength = evt.edit.length;

                        UserInputService.SetEditingText(composition, cursor, selectionLength);
                    } break;
                    case SDL_EventType.SDL_TEXTINPUT:
                    {
                        byte[] bytes = new byte[32];
                        Marshal.Copy(new IntPtr(evt.edit.text), bytes, 0, 32);

                        string composition = Encoding.UTF8.GetString(bytes, 0, Array.IndexOf<byte>(bytes, 0));

                        UserInputService.SetInputText(composition);
                    } break;

                    case SDL_EventType.SDL_JOYDEVICEADDED:
                    {
                        Logger.Log($"Joystick Added: Device [{ evt.jdevice.which }] \"{ SDL_JoystickNameForIndex(evt.jdevice.which) }\"", LogPriority.Verbose);

                        var gamepad = new Gamepad(evt.jdevice.which);
                        UserInputService.Gamepad_Connected(gamepad);
                    } break;
                    case SDL_EventType.SDL_JOYDEVICEREMOVED:
                        Logger.Log($"Joystick Removed: Instance [{ evt.jdevice.which }]", LogPriority.Verbose);
                        UserInputService.Gamepad_Disconnected(evt.jdevice.which);
                        break;

                    case SDL_EventType.SDL_JOYAXISMOTION:
                    {
                        UserInputService.Gamepad_AxisMotion(evt.jaxis.which, evt.jaxis.axis, evt.jaxis.axisValue);
                    } break;
                    case SDL_EventType.SDL_JOYBALLMOTION:
                    {
                        UserInputService.Gamepad_BallMotion(evt.jball.which, evt.jball.ball, evt.jball.xrel, evt.jball.yrel);
                    } break;
                    case SDL_EventType.SDL_JOYBUTTONDOWN:
                    {
                        UserInputService.Gamepad_Pressed(evt.jbutton.which, evt.jbutton.button);
                    } break;
                    case SDL_EventType.SDL_JOYBUTTONUP:
                    {
                        UserInputService.Gamepad_Released(evt.jbutton.which, evt.jbutton.button);
                    } break;
                    case SDL_EventType.SDL_JOYHATMOTION:
                    {
                        // TODO(local): look into what values hats need
                        //UserInputService.Gamepad_HatMotion(evt.jhat.which, evt.jhat.hat, evt.jhat.hatValue);
                    } break;

                    case SDL_EventType.SDL_DROPBEGIN: break;
                    case SDL_EventType.SDL_DROPCOMPLETE: break;
                    case SDL_EventType.SDL_DROPFILE: break;
                    case SDL_EventType.SDL_DROPTEXT: break;

                    case SDL_EventType.SDL_AUDIODEVICEADDED: Logger.Log("Audio Device Added: " + evt.adevice.which, LogPriority.Verbose); break;
                    case SDL_EventType.SDL_AUDIODEVICEREMOVED: Logger.Log("Audio Device Removed: " + evt.adevice.which, LogPriority.Verbose); break;

                    case SDL_EventType.SDL_CLIPBOARDUPDATE: break;
                        
                    case SDL_EventType.SDL_WINDOWEVENT:
                        switch (evt.window.windowEvent)
                        {
                            case SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE: host!.RequestExit(); break;
                                
                            case SDL_WindowEventID.SDL_WINDOWEVENT_TAKE_FOCUS: SDL_SetWindowInputFocus(window); break;
                                
                            case SDL_WindowEventID.SDL_WINDOWEVENT_ENTER: break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE: break;
                                
                            case SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                            {
                                HasFocus = true;
                            } break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                            {
                                HasFocus = false;
                            } break;

                            case SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                                Width = evt.window.data1;
                                Height = evt.window.data2;
                                break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                                Width = evt.window.data1;
                                Height = evt.window.data2;
                                GL.Viewport(0, 0, Width, Height);
                                ClientSizeChanged?.Invoke(Width, Height);
                                break;

                            case SDL_WindowEventID.SDL_WINDOWEVENT_MOVED: host!.WindowMoved(evt.window.data1, evt.window.data2); break;

                            case SDL_WindowEventID.SDL_WINDOWEVENT_HIDDEN: break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_SHOWN: break;

                            case SDL_WindowEventID.SDL_WINDOWEVENT_MAXIMIZED: host!.WindowMaximized(); break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_MINIMIZED: host!.WindowMinimized(); break;
                            case SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED: host!.WindowRestored(); break;

                            case SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED: break;
                        }
                        break;
                        
                    case SDL_EventType.SDL_RENDER_DEVICE_RESET: break;
                    case SDL_EventType.SDL_RENDER_TARGETS_RESET: break;
                        
                    case SDL_EventType.SDL_SYSWMEVENT: break;

                    case SDL_EventType.SDL_USEREVENT: break;

                    default: break;
                }
            }
        }
    }
}
