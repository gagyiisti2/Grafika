
using Silk.NET.Maths;

namespace Szeminarium
{
    internal class CameraDescriptor
    {
        public double DistanceToOrigin { get; private set; } = 5.0;

        public double AngleToZYPlane { get; private set; } = Math.PI * 0.7;

        public double AngleToZXPlane { get; private set; } = Math.PI * 1.3;

        const double DistanceScaleFactor = 1.1;

        const double AngleChangeStepSize = Math.PI / 180 * 5;

        private Vector3D<float> target = Vector3D<float>.Zero;

        /// <summary>
        /// Gets the position of the camera.
        /// </summary>
        public Vector3D<float> Position
        {
            get
            {
                return GetPointFromAngles(DistanceToOrigin, AngleToZYPlane, AngleToZXPlane);
            }
        }

        /// <summary>
        /// Gets the up vector of the camera. -> merre van a felfele irany
        /// </summary>
        public Vector3D<float> UpVector
        {
            get
            {
                return Vector3D.Normalize(GetPointFromAngles(DistanceToOrigin, AngleToZYPlane, AngleToZXPlane + Math.PI / 2)); // 90 fokkal elforditja a fuggolegest
            }
        }

        /// <summary>
        /// Gets the target point of the camera view.
        /// </summary>
        public Vector3D<float> Target
        {
            get
            {
                return target;
            }
        }

        public void IncreaseZXAngle()
        {
            AngleToZXPlane += AngleChangeStepSize;
        }

        public void DecreaseZXAngle()
        {
            AngleToZXPlane -= AngleChangeStepSize;
        }

        public void IncreaseZYAngle()
        {
            AngleToZYPlane += AngleChangeStepSize;

        }

        public void DecreaseZYAngle()
        {
            AngleToZYPlane -= AngleChangeStepSize;
        }

        public void IncreaseDistance()
        {
            DistanceToOrigin = DistanceToOrigin * DistanceScaleFactor;
        }

        public void DecreaseDistance()
        {
            DistanceToOrigin = DistanceToOrigin / DistanceScaleFactor;
        }

        public void MoveRight()
        {
            var forward = new Vector3D<float>(target.X - Position.X, target.Y - Position.Y, target.Z - Position.Z);
            forward = Vector3D.Normalize(forward);

            var right = Vector3D.Cross(forward, UpVector);
            right = Vector3D.Normalize(right);

            float speed = 0.5f; 
            target = new Vector3D<float>(target.X + right.X * speed, target.Y + right.Y * speed, target.Z + right.Z * speed);
        }
        public void MoveLeft()
        {
            var forward = new Vector3D<float>(target.X - Position.X, target.Y - Position.Y, target.Z - Position.Z);
            forward = Vector3D.Normalize(forward);

            var right = Vector3D.Cross(forward, UpVector);
            right = Vector3D.Normalize(right);

            float speed = 0.5f;
            target = new Vector3D<float>(target.X - right.X * speed, target.Y - right.Y * speed, target.Z - right.Z * speed);
        }

        private static Vector3D<float> GetPointFromAngles(double distanceToOrigin, double angleToMinZYPlane, double angleToMinZXPlane)
        {
            var x = distanceToOrigin * Math.Cos(angleToMinZXPlane) * Math.Sin(angleToMinZYPlane);
            var z = distanceToOrigin * Math.Cos(angleToMinZXPlane) * Math.Cos(angleToMinZYPlane);
            var y = distanceToOrigin * Math.Sin(angleToMinZXPlane);

            return new Vector3D<float>((float)x, (float)y, (float)z);
        }
    }
}
