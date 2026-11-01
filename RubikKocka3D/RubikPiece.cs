using System;
using System.Collections.Generic;
using Silk.NET.Maths;
using System.Text;

namespace RubikKocka3D
{
    internal class RubikPiece
    {
        public Vector3D<float> OriginalPosition { get; private set; } 
        public Vector3D<float> CurrentPosition { get; set; }
        public Matrix4X4<float> OrientationMatrix { get; set; } = Matrix4X4<float>.Identity; 
        public ModelObjectDescriptor ModelDescriptor { get; private set; }

        public RubikPiece(Vector3D<float> position, ModelObjectDescriptor modelDescriptor)
        {
            OriginalPosition = position;
            CurrentPosition = position;
            ModelDescriptor = modelDescriptor;
        }
    }
}
