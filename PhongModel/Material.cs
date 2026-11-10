using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace PhongModel
{
    internal record Material(string Name, Vector3 Ambient, Vector3 Diffuse, Vector3 Specular, float Shininess)
    {
        public static readonly Material[] Presets =
        [
             new("Default",
                 new(0.2f, 0.2f, 0.2f), new(0.3f, 0.3f, 0.3f), new(0.5f, 0.5f, 0.5f), 50f),
            new("Brass",
                new(0.329412f, 0.223529f, 0.027451f), new(0.780392f, 0.568627f, 0.113725f), new(0.992157f, 0.941176f, 0.807843f), 27.8974f),
            new("Chrome",
                new(0.25f, 0.25f, 0.25f), new(0.4f, 0.4f, 0.4f), new(0.774597f, 0.774597f, 0.774597f), 76.8f),
            new("Gold",
                new(0.24725f, 0.1995f, 0.0745f), new(0.75164f, 0.60648f, 0.22648f), new(0.628281f, 0.555802f, 0.366065f), 51.2f),
            new("Polished Silver",
                new(0.23125f, 0.23125f, 0.23125f), new(0.2775f, 0.2775f, 0.2775f), new(0.773911f, 0.773911f, 0.773911f), 89.6f),
            new("Copper",
                new(0.19125f, 0.0735f, 0.0225f), new(0.7038f, 0.27048f, 0.0828f), new(0.256777f, 0.137622f, 0.086014f), 12.8f),
            new("Emerald",
                new(0.0215f, 0.1745f, 0.0215f), new(0.07568f, 0.61424f, 0.07568f), new(0.633f, 0.727811f, 0.633f), 76.8f),
            new("Ruby",
                new(0.1745f, 0.01175f, 0.01175f), new(0.61424f, 0.04136f, 0.04136f), new(0.727811f, 0.626959f, 0.626959f), 76.8f),
            new("Pearl",
                new(0.25f, 0.20725f, 0.20725f), new(1.0f, 0.829f, 0.829f), new(0.296648f, 0.296648f, 0.296648f), 11.264f),
            new("Black Rubber",
                new(0.02f, 0.02f, 0.02f), new(0.01f, 0.01f, 0.01f), new(0.4f, 0.4f, 0.4f), 10f),
        ];
    }
}
