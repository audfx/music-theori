using System;

using NAudio.Wave;

namespace theori.Audio.NAudio
{
    internal unsafe sealed class NAudioOutputDevice : IAudioOutputDevice, IWaveProvider
    {
        private readonly WaveFormat m_format;
        private IWavePlayer m_player;

        private float[] m_buffer = new float[1024];

        public NAudioOutputDevice(WaveFormat format)
        {
            //WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

            m_format = format;

            m_player = new WaveOutEvent()
            {
                DesiredLatency = 32,
                NumberOfBuffers = 24,
            };
            m_player.Init(this);
        }

        WaveFormat IWaveProvider.WaveFormat => m_format;

        void IAudioOutputDevice.Begin()
        {
            m_player.Play();
        }

        int IWaveProvider.Read(byte[] buffer, int offset, int count)
        {
            if (AudioFactory.TempAudioSource != null)
            {
                EnsureBufferCapacity(count / sizeof(float));

                int floatsRead = AudioFactory.TempAudioSource.Read(m_buffer, 0, count / sizeof(float));
                fixed (byte *ptr = buffer)
                {
                    float* floatBuffer = (float*)(ptr + offset);
                    for (int i = 0; i < floatsRead; i++)
                        floatBuffer[i] = m_buffer[i];
                }

                return floatsRead * sizeof(float);
            }
            else for (int i = 0; i < count; i++)
                buffer[i + offset] = 0;

            return count;
        }

        private void EnsureBufferCapacity(int capacity)
        {
            if (m_buffer.Length < capacity)
                Array.Resize(ref m_buffer, capacity);
        }
    }
}
