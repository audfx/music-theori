namespace theori.Graphics
{
    public sealed class RenderState
    {
        public Transform WorldTransform;
        public Transform ProjectionMatrix;
        public Transform CameraMatrix;

        public (int X, int Y, int Width, int Height) Viewport;

        public float AspectRatio;
        public float TotalTime;
    }
}
