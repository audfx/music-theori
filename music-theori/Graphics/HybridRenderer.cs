using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

using MoonSharp;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using theori.Resources;

namespace theori.Graphics
{
    public class HybridRenderer : Disposable
    {
        enum RenderMode
        {
            NotRendering,
            Unset,

            Orthographic,
            Perspective,
        }

        public class CameraDesc2D
        {
            [MoonSharpHidden] public Vector2 Position;

            public void SetPosition(float x, float y)
            {
                Position = new Vector2(x, y);
            }
        }

        public class CameraDesc3D
        {
            [MoonSharpHidden] public Vector3 Position;
            [MoonSharpHidden] public float FieldOfView;

            public void SetPosition(float x, float y, float z)
            {
                Position = new Vector3(x, y, z);
            }
        }

        private (int X, int Y, int Width, int Height) m_viewport;

        private RenderMode m_state = RenderMode.NotRendering;

        private CameraDesc2D? m_cam2d;
        private CameraDesc3D? m_cam3d;

        private RenderState? m_currentState;
        private BasicCamera? m_currentCamera;

        private RenderQueue? m_queue;

        [MoonSharpVisible(false)] public HybridRenderer()
        {
        }

        #region State Setup

        private static Transform OrthoCamera(float w, float h) => (Transform)Matrix4x4.CreateOrthographicOffCenter(0, w, h, 0, -10, 10);

        private void UpdateRenderState()
        {
            if (m_currentState == null)
                throw new InvalidOperationException();

            m_currentState.Viewport = m_viewport;
            m_currentState.AspectRatio = (float)m_viewport.Width / m_viewport.Height;

            switch (m_state)
            {
                case RenderMode.Orthographic:
                {
                    m_currentState.ProjectionMatrix = OrthoCamera(m_viewport.Width, m_viewport.Height);
                } break;
            }
        }

        [MoonSharpVisible(true)] public void Begin()
        {
            if (m_state != RenderMode.NotRendering)
                throw new InvalidOperationException();

            Debug.Assert(m_queue == null);

            m_state = RenderMode.Unset;
            m_viewport = (0, 0, Window.Width, Window.Height);

            m_currentState = new RenderState();
            m_queue = new RenderQueue(m_currentState!);
        }

        [MoonSharpVisible(true)] public void End()
        {
            if (m_state == RenderMode.NotRendering)
                throw new InvalidOperationException();

            m_queue?.Dispose();
            m_queue = null;

            m_state = RenderMode.NotRendering;
        }

        [MoonSharpVisible(true)] public void Flush()
        {
            if (m_state == RenderMode.NotRendering)
                throw new InvalidOperationException();

            m_queue?.Process(true);
        }
        
        [MoonSharpVisible(true)] public void SetViewport(int x, int y, int w, int h)
        {
            if (m_state > RenderMode.Unset) Flush();

            m_viewport = (x, y, w, h);
        }
        
        [MoonSharpVisible(true)] public void SetCamera2D()
        {
            if (m_state != RenderMode.Orthographic) Flush();

            m_state = RenderMode.Orthographic;
            UpdateRenderState();
        }
        
        [MoonSharpVisible(true)] public void SetCamera3D()
        {
            m_state = RenderMode.Perspective;
            UpdateRenderState();
        }

        #endregion

        #region Queue Geometry Render

        [MoonSharpVisible(true)] public void FillQuad()
        {
        }

        [MoonSharpVisible(true)] public void DrawQuad()
        {
        }

        #endregion
    }
}
