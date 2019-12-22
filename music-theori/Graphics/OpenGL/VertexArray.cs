using System;

namespace theori.Graphics.OpenGL
{
    public sealed class VertexArray : UIntHandle
    {
        public VertexArray()
            : base(GL.GenVertexArray, GL.DeleteVertexArray)
        {
        }

        public void Bind()
        {
            GL.BindVertexArray(Handle);
        }

        public void SetVertexAttrib(uint index, GpuBuffer buffer, int size, bool normalized = false, int stride = 0, uint offset = 0)
        {
            Bind();
            buffer.Bind();

            GL.VertexAttribPointer(index, size, (uint)buffer.Type, normalized, stride, new IntPtr(offset));
            GL.EnableVertexAttribArray(index);
        }

        public void SetVertexAttrib(uint index, GpuBuffer buffer, int size, DataType type, bool normalized = false, int stride = 0, uint offset = 0)
        {
            Bind();
            buffer.Bind();

            switch (type)
            {
                case DataType.Byte:
                case DataType.UnsignedByte:
                case DataType.Short:
                case DataType.UnsignedShort:
                case DataType.Int:
                case DataType.UnsignedInt:
                    GL.VertexAttribIPointer(index, size, (uint)type, stride, new IntPtr(offset));
                    break;

                default:
                    GL.VertexAttribPointer(index, size, (uint)type, normalized, stride, new IntPtr(offset));
                    break;
            }
            GL.EnableVertexAttribArray(index);
        }
    }
}
