namespace theori.Audio
{
    public interface IAudioSource
    {
        int Read(float[] destBuffer, int offset, int count);
    }
}
