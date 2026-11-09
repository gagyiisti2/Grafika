using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dezsa
{
    /// <summary>
    /// Egyetlen deszkalap leirasa az XY sikban
    /// norma : +Z irany
    /// </summary>
    internal class GlStave
    {
        public uint Vao { get; }
        public uint Vertices { get; }
        public uint Colors { get; }
        public uint Indices { get; }
        public uint IndexArrayLength { get; }

        private GL Gl;

        private GlStave(uint vao, uint vertices, uint colors, uint indeces, uint indexArrayLength, GL gl)
        {
            this.Vao = vao;
            this.Vertices = vertices;
            this.Colors = colors;
            this.Indices = indeces;
            this.IndexArrayLength = indexArrayLength;
            this.Gl = gl;
        }

        public static unsafe GlStave CreateStave(GL Gl, float width, float height, float[] color,
            bool smoothNormals, double edgeNormalAngle) // a smoothNormals megadja meroleges legyen vagy zarjon be 10 fokos szoget az elozo norm.
        {

            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            // x es z normal komponense a par. alapjan
            float nx = smoothNormals ? (float)Math.Sin(edgeNormalAngle) : 0f;
            float nz = smoothNormals ? (float)Math.Cos(edgeNormalAngle) : 1f;

            float[] vertexArray = new float[]
            {
                -halfWidth,  halfHeight, 0f,   -nx, 0f, nz,   // bal felso
                -halfWidth, -halfHeight, 0f,   -nx, 0f, nz,   // bal also
                 halfWidth, -halfHeight, 0f,    nx, 0f, nz,   // jobb also
                 halfWidth,  halfHeight, 0f,    nx, 0f, nz,   // jobb felso
            };
            
            List<float> colorList = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                colorList.AddRange(color);
            }
            float[] colorArray = colorList.ToArray();

            uint[] indexArray = new uint[] {
                0, 1, 2,
                0, 2, 3
            };

            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);
            Gl.EnableVertexAttribArray(2);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);

            // release array buffer
            Gl.BindVertexArray(0);
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);

            return new GlStave(vao, vertices, colors, indices, (uint)indexArray.Length, Gl);
        }

        internal void ReleaseGlStave()
        {
            // always unbound the vertex buffer first, so no halfway results are displayed by accident
            Gl.DeleteBuffer(Vertices);
            Gl.DeleteBuffer(Colors);
            Gl.DeleteBuffer(Indices);
            Gl.DeleteVertexArray(Vao);
        }
    }
}
